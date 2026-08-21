using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace WechatAIClient.Services.Weixin;

public enum WechatCallbackMode
{
    Auto,
    Http,
    Tcp,
    Both
}

public enum WechatBridgeEventKind
{
    IncomingPrivateMessage,
    IncomingGroupMessage,
    SelfTextMessage,
    SelfImageMessage,
    SelfFileMessage,
    ConversationChanged,
    GroupMemberChanged,
    Unknown
}

public sealed class WechatIncomingMessage
{
    public string MessageId { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string? SenderId { get; set; }
    public string? SenderDisplayName { get; set; }
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string MessageType { get; set; } = "Text";
    public bool IsFromMe { get; set; }
    public bool IsGroup { get; set; }
    public string? GroupId { get; set; }
    public bool MentionsMe { get; set; }
    public bool IsReplyToMe { get; set; }
    public string? QuoteMessageId { get; set; }
    public string? QuoteContent { get; set; }
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public string? ImagePath { get; set; }
    public string? FilePath { get; set; }
    public string? RawType { get; set; }
}

public sealed class WechatBridgeEvent
{
    public WechatBridgeEventKind Kind { get; set; } = WechatBridgeEventKind.Unknown;
    public WechatIncomingMessage? Message { get; set; }
    public string? RawFingerprint { get; set; }
}

public sealed class JsApiEnvelope
{
    [JsonPropertyName("JsApiResponse")]
    public JsApiResponseBlock? JsApiResponse { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class JsApiResponseBlock
{
    [JsonPropertyName("RespJson")]
    public string? RespJson { get; set; }
}

public interface IWechatCallbackParser
{
    IReadOnlyList<WechatBridgeEvent> Parse(ReadOnlySpan<byte> utf8Json);
    IReadOnlyList<WechatBridgeEvent> Parse(string json);
}

public sealed class WechatCallbackParser : IWechatCallbackParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<WechatBridgeEvent> Parse(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            return Parse(Encoding.UTF8.GetString(utf8Json));
        }
        catch
        {
            return Array.Empty<WechatBridgeEvent>();
        }
    }

    public IReadOnlyList<WechatBridgeEvent> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<WechatBridgeEvent>();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var list = new List<WechatBridgeEvent>();

        // Double-layer JsApiResponse.RespJson
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("JsApiResponse", out var js) &&
            js.ValueKind == JsonValueKind.Object &&
            js.TryGetProperty("RespJson", out var resp) &&
            resp.ValueKind == JsonValueKind.String)
        {
            var inner = resp.GetString();
            if (!string.IsNullOrWhiteSpace(inner))
            {
                list.AddRange(ParseInner(inner));
            }
        }

        list.AddRange(ParseInnerElement(root));
        return list;
    }

    private static IReadOnlyList<WechatBridgeEvent> ParseInner(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseInnerElement(doc.RootElement);
        }
        catch
        {
            return Array.Empty<WechatBridgeEvent>();
        }
    }

    private static IReadOnlyList<WechatBridgeEvent> ParseInnerElement(JsonElement root)
    {
        var results = new List<WechatBridgeEvent>();
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("msg_list", out var msgList) &&
            msgList.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in msgList.EnumerateArray())
            {
                var ev = MapMessageObject(item);
                if (ev is not null)
                {
                    results.Add(ev);
                }
            }

            return results;
        }

        // Single message object / callback wrappers
        if (root.ValueKind == JsonValueKind.Object)
        {
            // Common wrappers: data / message / msg
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                var ev = MapMessageObject(data) ?? MapMessageObject(root);
                if (ev is not null)
                {
                    results.Add(ev);
                }

                return results;
            }

            var single = MapMessageObject(root);
            if (single is not null)
            {
                results.Add(single);
            }
            else
            {
                results.Add(new WechatBridgeEvent
                {
                    Kind = WechatBridgeEventKind.Unknown,
                    RawFingerprint = StableHash(root.GetRawText())
                });
            }
        }

        return results;
    }

    private static WechatBridgeEvent? MapMessageObject(JsonElement item)
    {
        var content = GetString(item, "content", "msg", "text") ?? "";
        var nickname = GetString(item, "nickname", "nick_name", "nickName", "sender_name", "from_name");
        var senderId = GetString(item, "sender", "from_wxid", "fromUsr", "from_user", "talker", "wxid");
        var roomId = GetString(item, "room_id", "roomId", "roomid", "chatroom", "to_wxid");
        var msgId = GetString(item, "msgid", "msg_id", "msgId", "newmsgid", "newMsgId", "id")
                    ?? StableHash(item.GetRawText());
        var isGroup = !string.IsNullOrWhiteSpace(roomId) && roomId.Contains("@chatroom", StringComparison.OrdinalIgnoreCase);
        // Some payloads put conversation in to_user / toUsr
        var toUser = GetString(item, "to_user", "toUsr", "to_wxid");
        if (!isGroup && !string.IsNullOrWhiteSpace(toUser) && toUser.Contains("@chatroom", StringComparison.OrdinalIgnoreCase))
        {
            roomId = toUser;
            isGroup = true;
        }

        var conversationId = isGroup
            ? roomId!
            : (GetString(item, "from_wxid", "talker", "wxid", "fromUsr") ?? senderId ?? "");

        if (string.IsNullOrWhiteSpace(conversationId) && string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(nickname))
        {
            return null;
        }

        var isSelf = GetBool(item, "is_self", "isSelf", "from_me") ||
                     string.Equals(GetString(item, "type", "msg_type", "msgType"), "self", StringComparison.OrdinalIgnoreCase);

        var rawType = GetString(item, "type", "msg_type", "msgType", "MsgType") ?? "1";
        var mappedType = WechatMessageTypeMapper.Map(rawType, content);
        var kind = ResolveKind(isSelf, isGroup, mappedType);

        var message = new WechatIncomingMessage
        {
            MessageId = msgId,
            ConversationId = conversationId,
            SenderId = senderId,
            SenderDisplayName = nickname,
            Content = content,
            Timestamp = ParseTimestamp(item) ?? DateTime.Now,
            MessageType = mappedType,
            IsFromMe = isSelf,
            IsGroup = isGroup,
            GroupId = isGroup ? roomId : null,
            MentionsMe = GetBool(item, "is_at", "is_at_me", "isAtMe") ||
                         (content?.Contains("@", StringComparison.Ordinal) ?? false) && GetBool(item, "at_me"),
            QuoteMessageId = GetString(item, "refer_msgid", "referMsgId", "quote_msgid"),
            QuoteContent = GetString(item, "refer_content", "referContent", "quote_content"),
            FileName = GetString(item, "file_name", "filename", "title"),
            FilePath = GetString(item, "file_path", "filepath", "path"),
            ImagePath = GetString(item, "image_path", "img_path", "path"),
            RawType = rawType
        };

        return new WechatBridgeEvent
        {
            Kind = kind,
            Message = message,
            RawFingerprint = msgId
        };
    }

    private static WechatBridgeEventKind ResolveKind(bool isSelf, bool isGroup, string mappedType)
    {
        if (isSelf)
        {
            return mappedType switch
            {
                "Image" => WechatBridgeEventKind.SelfImageMessage,
                "File" => WechatBridgeEventKind.SelfFileMessage,
                _ => WechatBridgeEventKind.SelfTextMessage
            };
        }

        return isGroup
            ? WechatBridgeEventKind.IncomingGroupMessage
            : WechatBridgeEventKind.IncomingPrivateMessage;
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var p))
            {
                return p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Number => p.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }
        }

        return null;
    }

    private static bool GetBool(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
            {
                return n != 0;
            }

            if (p.ValueKind == JsonValueKind.String &&
                (p.GetString() is "1" or "true" or "True"))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime? ParseTimestamp(JsonElement el)
    {
        var raw = GetString(el, "timestamp", "createTime", "create_time", "time");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (long.TryParse(raw, out var unix))
        {
            if (unix > 10_000_000_000)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unix).LocalDateTime;
            }

            return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
        }

        return DateTime.TryParse(raw, out var dt) ? dt : null;
    }

    private static string StableHash(string text)
    {
        var hash = text.GetHashCode(StringComparison.Ordinal);
        return $"h{unchecked((uint)hash):x8}";
    }
}

public static class WechatMessageTypeMapper
{
    public static string Map(string? rawType, string? content)
    {
        var t = (rawType ?? "").Trim().ToLowerInvariant();
        if (t is "1" or "text" or "txt")
        {
            return "Text";
        }

        if (t is "3" or "image" or "img" or "pic")
        {
            return "Image";
        }

        var isAppMsg = (t is "49" or "file" or "app") ||
                       (content?.Contains("<appmsg", StringComparison.OrdinalIgnoreCase) ?? false);
        if (isAppMsg)
        {
            if (content?.Contains("<type>6</type>", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "File";
            }

            if (content?.Contains("refermsg", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "Quote";
            }

            return "App";
        }

        if (t is "34" or "voice" or "audio")
        {
            return "Voice";
        }

        if (t is "43" or "video")
        {
            return "Video";
        }

        if (t is "47" or "emotion" or "emoji")
        {
            return "Emotion";
        }

        if (t is "10000" or "system")
        {
            return "System";
        }

        return "Unknown";
    }
}

public sealed class WechatHttpCallbackServer : IAsyncDisposable
{
    private readonly IWechatCallbackParser _parser;
    private readonly ChannelWriter<WechatBridgeEvent> _writer;
    private readonly ILogger<WechatHttpCallbackServer> _logger;
    private readonly int _port;
    private readonly long _maxBodyBytes;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public WechatHttpCallbackServer(
        IWechatCallbackParser parser,
        ChannelWriter<WechatBridgeEvent> writer,
        ILogger<WechatHttpCallbackServer> logger,
        int port = 5000,
        long maxBodyBytes = 2 * 1024 * 1024)
    {
        _parser = parser;
        _writer = writer;
        _logger = logger;
        _port = port;
        _maxBodyBytes = maxBodyBytes;
    }

    public bool IsRunning => _listener?.IsListening == true;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            return;
        }

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP callback port {Port} unavailable", _port);
            throw new InvalidOperationException($"HTTP 回调端口 {_port} 被占用", ex);
        }

        _listener = listener;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // ignore
        }

        _listener = null;
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore
            }
        }

        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(token);
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "HTTP callback accept ended");
                break;
            }

            _ = Task.Run(() => HandleAsync(ctx, token), CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken token)
    {
        try
        {
            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) ||
                !(ctx.Request.Url?.AbsolutePath.Equals("/api/recvMsg", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                ctx.Response.StatusCode = 404;
                await WriteJsonAsync(ctx, """{"error":"not found"}""");
                return;
            }

            if (ctx.Request.ContentLength64 > _maxBodyBytes)
            {
                ctx.Response.StatusCode = 413;
                await WriteJsonAsync(ctx, """{"error":"body too large"}""");
                return;
            }

            await using var ms = new MemoryStream();
            await ctx.Request.InputStream.CopyToAsync(ms, token);
            if (ms.Length > _maxBodyBytes)
            {
                ctx.Response.StatusCode = 413;
                await WriteJsonAsync(ctx, """{"error":"body too large"}""");
                return;
            }

            string json;
            try
            {
                json = Encoding.UTF8.GetString(ms.ToArray());
                // validate JSON
                using var _ = JsonDocument.Parse(json);
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                await WriteJsonAsync(ctx, """{"error":"No JSON data received"}""");
                return;
            }

            // Respond immediately
            ctx.Response.StatusCode = 200;
            await WriteJsonAsync(ctx, """{"status":"success","message":"Data received"}""");

            foreach (var ev in _parser.Parse(json))
            {
                _writer.TryWrite(ev);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP callback handle failed");
            try
            {
                ctx.Response.StatusCode = 500;
                await WriteJsonAsync(ctx, """{"error":"internal"}""");
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.OutputStream.Close();
    }
}

public sealed class WechatTcpCallbackServer : IAsyncDisposable
{
    public const int DefaultMaxFrameSize = 10 * 1024 * 1024;

    private readonly IWechatCallbackParser _parser;
    private readonly ChannelWriter<WechatBridgeEvent> _writer;
    private readonly ILogger<WechatTcpCallbackServer> _logger;
    private readonly int _port;
    private readonly int _maxFrameSize;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public WechatTcpCallbackServer(
        IWechatCallbackParser parser,
        ChannelWriter<WechatBridgeEvent> writer,
        ILogger<WechatTcpCallbackServer> logger,
        int port = 61108,
        int maxFrameSize = DefaultMaxFrameSize)
    {
        _parser = parser;
        _writer = writer;
        _logger = logger;
        _port = port;
        _maxFrameSize = maxFrameSize;
    }

    public bool IsRunning => _listener is not null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            return Task.CompletedTask;
        }

        var listener = new TcpListener(IPAddress.Loopback, _port);
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP callback port {Port} unavailable", _port);
            throw new InvalidOperationException($"TCP 回调端口 {_port} 被占用", ex);
        }

        _listener = listener;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // ignore
        }

        _listener = null;
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore
            }
        }

        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token);
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TCP accept ended");
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, token), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                var header = new byte[4];
                while (!token.IsCancellationRequested)
                {
                    await ReadExactlyAsync(stream, header, token);
                    var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                    if (length == 0 || length > _maxFrameSize)
                    {
                        _logger.LogWarning("TCP frame rejected length={Length}", length);
                        break;
                    }

                    var payload = new byte[length];
                    await ReadExactlyAsync(stream, payload, token);
                    var json = Encoding.UTF8.GetString(payload);
                    foreach (var ev in _parser.Parse(json))
                    {
                        _writer.TryWrite(ev);
                    }
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TCP client ended");
            }
        }
    }

    internal static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
