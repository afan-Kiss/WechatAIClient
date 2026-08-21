using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
    /// <summary>May be provisional until Bridge normalizes with account wxid.</summary>
    public string ConversationId { get; set; } = "";
    public string? FromWxid { get; set; }
    public string? ToWxid { get; set; }
    public string? RoomId { get; set; }
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

public static class JsonElementExtensions
{
    public static bool TryGetPropertyIgnoreCase(this JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        return false;
    }
}

public sealed class WechatCallbackParser : IWechatCallbackParser
{
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

        // Double-layer JsApiResponse.RespJson — when inner yields events, do not re-parse envelope.
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetPropertyIgnoreCase("JsApiResponse", out var js) &&
            js.ValueKind == JsonValueKind.Object &&
            js.TryGetPropertyIgnoreCase("RespJson", out var resp) &&
            resp.ValueKind == JsonValueKind.String)
        {
            var inner = resp.GetString();
            if (!string.IsNullOrWhiteSpace(inner) && TryParseInner(inner, out var innerEvents))
            {
                if (innerEvents.Count > 0)
                {
                    return innerEvents;
                }

                // Valid empty inner (e.g. msg_list:[]) — still skip envelope to avoid Unknown.
                return innerEvents;
            }
        }

        return ParseInnerElement(root);
    }

    private static bool TryParseInner(string json, out IReadOnlyList<WechatBridgeEvent> events)
    {
        events = Array.Empty<WechatBridgeEvent>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            events = ParseInnerElement(doc.RootElement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<WechatBridgeEvent> ParseInnerElement(JsonElement root)
    {
        var results = new List<WechatBridgeEvent>();
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetPropertyIgnoreCase("msg_list", out var msgList) &&
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

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetPropertyIgnoreCase("data", out var data) && data.ValueKind == JsonValueKind.Object)
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
                    RawFingerprint = ComputeStableFingerprint(
                        null, null, null, null, null, root.GetRawText())
                });
            }
        }

        return results;
    }

    private static WechatBridgeEvent? MapMessageObject(JsonElement item)
    {
        var content = GetString(item, "content", "msg", "text") ?? "";
        var nickname = GetString(item, "nickname", "nick_name", "nickName", "sender_name", "from_name");
        var fromWxid = GetString(item, "from_wxid", "fromUsr", "from_user", "FromUserName", "sender", "talker");
        var toWxid = GetString(item, "to_wxid", "toUsr", "to_user", "ToUserName", "to");
        var roomId = GetString(item, "room_id", "roomId", "roomid", "chatroom", "RoomId");
        var senderId = fromWxid ?? GetString(item, "wxid");
        var rawType = GetString(item, "type", "msg_type", "msgType", "MsgType") ?? "1";
        var timestampRaw = GetString(item, "timestamp", "createTime", "create_time", "time", "CreateTime");
        var timestamp = ParseTimestampValue(timestampRaw) ?? DateTime.Now;

        var isGroup = IsChatroomId(roomId);
        if (!isGroup && IsChatroomId(toWxid))
        {
            roomId = toWxid;
            isGroup = true;
        }

        if (!isGroup && IsChatroomId(fromWxid))
        {
            roomId = fromWxid;
            isGroup = true;
        }

        // Provisional conversation id — Bridge re-normalizes with account wxid for private chats.
        var provisionalConversation = isGroup
            ? roomId!
            : (fromWxid ?? senderId ?? toWxid ?? "");

        var fingerprint = ComputeStableFingerprint(
            provisionalConversation,
            senderId,
            timestampRaw ?? timestamp.ToString("O"),
            rawType,
            content,
            item.GetRawText());

        var msgId = GetString(item, "msgid", "msg_id", "msgId", "MsgId", "newmsgid", "newMsgId", "id")
                    ?? fingerprint;

        if (string.IsNullOrWhiteSpace(provisionalConversation) &&
            string.IsNullOrWhiteSpace(content) &&
            string.IsNullOrWhiteSpace(nickname) &&
            string.IsNullOrWhiteSpace(fromWxid) &&
            string.IsNullOrWhiteSpace(toWxid))
        {
            return null;
        }

        var isSelf = GetBool(item, "is_self", "isSelf", "from_me") ||
                     string.Equals(GetString(item, "type", "msg_type", "msgType"), "self", StringComparison.OrdinalIgnoreCase);

        var mappedType = WechatMessageTypeMapper.Map(rawType, content);
        var kind = ResolveKind(isSelf, isGroup, mappedType);

        var message = new WechatIncomingMessage
        {
            MessageId = msgId,
            ConversationId = provisionalConversation,
            FromWxid = fromWxid,
            ToWxid = toWxid,
            RoomId = isGroup ? roomId : null,
            SenderId = senderId,
            SenderDisplayName = nickname,
            Content = content,
            Timestamp = timestamp,
            MessageType = mappedType,
            IsFromMe = isSelf,
            IsGroup = isGroup,
            GroupId = isGroup ? roomId : null,
            MentionsMe = GetBool(item, "is_at", "is_at_me", "isAtMe") ||
                         (content.Contains('@', StringComparison.Ordinal) && GetBool(item, "at_me")),
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
            RawFingerprint = fingerprint
        };
    }

    private static bool IsChatroomId(string? id)
        => !string.IsNullOrWhiteSpace(id) &&
           id.Contains("@chatroom", StringComparison.OrdinalIgnoreCase);

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
            if (!el.TryGetPropertyIgnoreCase(name, out var p))
            {
                continue;
            }

            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static bool GetBool(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetPropertyIgnoreCase(name, out var p))
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

    private static DateTime? ParseTimestampValue(string? raw)
    {
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

    /// <summary>
    /// Deterministic SHA256 hex fingerprint (first 32 hex chars). Not GetHashCode.
    /// Material: conversation|sender|timestamp|type|content|raw
    /// </summary>
    public static string ComputeStableFingerprint(
        string? conversation,
        string? sender,
        string? timestamp,
        string? type,
        string? content,
        string? raw)
    {
        var material = string.Join('|',
            conversation ?? "",
            sender ?? "",
            timestamp ?? "",
            type ?? "",
            content ?? "",
            raw ?? "");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
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

/// <summary>Copies while rejecting bodies over maxBytes (safe for ContentLength=-1 / chunked).</summary>
internal sealed class LimitedCopyStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _copied;

    public LimitedCopyStream(Stream inner, long maxBytes)
    {
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public bool ExceededLimit { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadCore(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) => ReadCore(buffer);

    private int ReadCore(Span<byte> buffer)
    {
        if (ExceededLimit || buffer.Length == 0)
        {
            return 0;
        }

        if (_copied >= _maxBytes)
        {
            // Probe one more byte to detect overflow without writing it.
            Span<byte> probe = stackalloc byte[1];
            var peek = _inner.Read(probe);
            if (peek > 0)
            {
                ExceededLimit = true;
            }

            return 0;
        }

        var allowed = (int)Math.Min(buffer.Length, _maxBytes - _copied);
        var read = _inner.Read(buffer[..allowed]);
        if (read > 0)
        {
            _copied += read;
        }

        if (_copied >= _maxBytes)
        {
            Span<byte> probe = stackalloc byte[1];
            var peek = _inner.Read(probe);
            if (peek > 0)
            {
                ExceededLimit = true;
            }
        }

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (ExceededLimit || buffer.Length == 0)
        {
            return 0;
        }

        if (_copied >= _maxBytes)
        {
            var probe = new byte[1];
            var peek = await _inner.ReadAsync(probe.AsMemory(0, 1), cancellationToken);
            if (peek > 0)
            {
                ExceededLimit = true;
            }

            return 0;
        }

        var allowed = (int)Math.Min(buffer.Length, _maxBytes - _copied);
        var read = await _inner.ReadAsync(buffer[..allowed], cancellationToken);
        if (read > 0)
        {
            _copied += read;
        }

        if (_copied >= _maxBytes)
        {
            var probe = new byte[1];
            var peek = await _inner.ReadAsync(probe.AsMemory(0, 1), cancellationToken);
            if (peek > 0)
            {
                ExceededLimit = true;
            }
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

public sealed class WechatHttpCallbackServer : IAsyncDisposable
{
    private readonly IWechatCallbackParser _parser;
    private readonly ChannelWriter<WechatBridgeEvent> _writer;
    private readonly ILogger<WechatHttpCallbackServer> _logger;
    private readonly int _port;
    private readonly long _maxBodyBytes;
    private readonly SemaphoreSlim _handlerGate = new(8, 8);
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

            _ = Task.Run(() => HandleWithGateAsync(ctx, token), CancellationToken.None);
        }
    }

    private async Task HandleWithGateAsync(HttpListenerContext ctx, CancellationToken token)
    {
        try
        {
            await _handlerGate.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                ctx.Response.StatusCode = 503;
                ctx.Response.Close();
            }
            catch
            {
                // ignore
            }

            return;
        }

        try
        {
            await HandleAsync(ctx, token);
        }
        finally
        {
            _handlerGate.Release();
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

            // ContentLength=-1 (chunked) is safe: only reject when known length already over max.
            if (ctx.Request.ContentLength64 >= 0 && ctx.Request.ContentLength64 > _maxBodyBytes)
            {
                ctx.Response.StatusCode = 413;
                await WriteJsonAsync(ctx, """{"error":"body too large"}""");
                return;
            }

            await using var ms = new MemoryStream();
            await using (var limited = new LimitedCopyStream(ctx.Request.InputStream, _maxBodyBytes))
            {
                await limited.CopyToAsync(ms, token);
                if (limited.ExceededLimit)
                {
                    ctx.Response.StatusCode = 413;
                    await WriteJsonAsync(ctx, """{"error":"body too large"}""");
                    return;
                }
            }

            string json;
            try
            {
                json = Encoding.UTF8.GetString(ms.ToArray());
                using var _ = JsonDocument.Parse(json);
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                await WriteJsonAsync(ctx, """{"error":"No JSON data received"}""");
                return;
            }

            // Respond 200 quickly, then backpressure via WriteAsync (Wait mode).
            ctx.Response.StatusCode = 200;
            await WriteJsonAsync(ctx, """{"status":"success","message":"Data received"}""");

            foreach (var ev in _parser.Parse(json))
            {
                try
                {
                    await _writer.WriteAsync(ev, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP callback handle failed");
            try
            {
                if (ctx.Response.OutputStream.CanWrite)
                {
                    ctx.Response.StatusCode = 500;
                    await WriteJsonAsync(ctx, """{"error":"internal"}""");
                }
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
                        try
                        {
                            await _writer.WriteAsync(ev, token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (ChannelClosedException)
                        {
                            return;
                        }
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

    public static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
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
