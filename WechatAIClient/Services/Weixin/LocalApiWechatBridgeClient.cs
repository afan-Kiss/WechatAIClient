using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services.Wechat;

namespace WechatAIClient.Services.Weixin;

/// <summary>
/// Real WeChat bridge over existing Hook HTTP API (127.0.0.1:19088) + HTTP/TCP callbacks.
/// Does not implement injection; only consumes the black-box Hook service.
/// </summary>
public sealed class LocalApiWechatBridgeClient : IWechatBridgeClient
{
    private readonly ILocalWeixinApiClient _api;
    private readonly IWechatCallbackParser _parser;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LocalApiWechatBridgeClient> _logger;
    private readonly MessageDeduplicator _deduper = new(800);
    private readonly PendingOutgoingTracker _pending = new();
    private readonly PendingAiOutgoingRegistry _aiOutgoing = new();
    private readonly GroupMemberCache _memberCache = new();
    private readonly object _gate = new();
    private readonly List<BridgeContact> _friends = [];
    private readonly List<BridgeContact> _groups = [];
    private readonly Dictionary<string, List<BridgeMessage>> _messages = new(StringComparer.Ordinal);

    private Channel<WechatBridgeEvent>? _channel;
    private WechatHttpCallbackServer? _http;
    private WechatTcpCallbackServer? _tcp;
    private CancellationTokenSource? _cts;
    private Task? _processor;
    private Task? _healthLoop;
    private WechatConnectionState _state = WechatConnectionState.Disconnected;
    private WechatAccountInfo? _account;
    private string? _accountWxid;
    private bool _initialized;
    private bool _disposed;
    private WechatCallbackMode _callbackMode = WechatCallbackMode.Auto;

    public LocalApiWechatBridgeClient(
        ILocalWeixinApiClient api,
        IWechatCallbackParser parser,
        ILoggerFactory loggerFactory)
    {
        _api = api;
        _parser = parser;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LocalApiWechatBridgeClient>();
    }

    public WechatConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<WechatConnectionState>? StateChanged;
    public event EventHandler<BridgeMessageEvent>? MessageReceived;
    public event EventHandler? BridgeCrashed;

    public void Configure(string? baseUrl, WechatCallbackMode mode)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl) && _api is LocalWeixinApiClient concrete)
        {
            concrete.BaseUrl = baseUrl;
        }

        _callbackMode = mode;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<WechatBridgeEvent>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _http = new WechatHttpCallbackServer(
            _parser,
            _channel.Writer,
            _loggerFactory.CreateLogger<WechatHttpCallbackServer>());
        _tcp = new WechatTcpCallbackServer(
            _parser,
            _channel.Writer,
            _loggerFactory.CreateLogger<WechatTcpCallbackServer>());

        await StartCallbacksAsync(_callbackMode, cancellationToken);
        _processor = Task.Run(() => ProcessEventsAsync(_cts.Token), CancellationToken.None);
        _healthLoop = Task.Run(() => HealthLoopAsync(_cts.Token), CancellationToken.None);
        SetState(WechatConnectionState.Connecting);
        await RefreshConnectionAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        if (_http is not null)
        {
            await _http.StopAsync();
        }

        if (_tcp is not null)
        {
            await _tcp.StopAsync();
        }

        _channel?.Writer.TryComplete();
        SetState(WechatConnectionState.Disconnected);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _initialized = false;
        SetState(WechatConnectionState.Connecting);
        await RefreshConnectionAsync(cancellationToken);
    }

    public Task<WechatAccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_state == WechatConnectionState.Connected ? _account : null);
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<BridgeContact>>(_friends.ToList());
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<BridgeContact>>(_groups.ToList());
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var recent = _friends.Concat(_groups)
                .Where(c => c.LastMessageTime is not null)
                .OrderByDescending(c => c.LastMessageTime)
                .ToList();
            if (recent.Count == 0)
            {
                recent = _friends.Concat(_groups).Take(50).ToList();
            }

            return Task.FromResult<IReadOnlyList<BridgeContact>>(recent);
        }
    }

    public Task<IReadOnlyList<BridgeMessage>> GetMessagesAsync(
        string conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_messages.TryGetValue(conversationId, out var list))
            {
                return Task.FromResult<IReadOnlyList<BridgeMessage>>(Array.Empty<BridgeMessage>());
            }

            return Task.FromResult<IReadOnlyList<BridgeMessage>>(
                list.TakeLast(Math.Max(1, limit)).ToList());
        }
    }

    public async Task<SendMessageResult> SendTextAsync(
        string conversationId,
        string text,
        string clientRequestId,
        CancellationToken cancellationToken = default)
    {
        if (State != WechatConnectionState.Connected)
        {
            return Fail(clientRequestId, "NotConnected", "微信 Hook 未连接");
        }

        _pending.Register(clientRequestId, conversationId, text, isFromAi: false);
        var result = await _api.SendTextAsync(conversationId, text, cancellationToken);
        if (!result.Success)
        {
            _pending.TryConsumeByClientRequestId(clientRequestId, out _);
            return Fail(clientRequestId, result.ExceptionType ?? "ApiRejected", result.ErrorMessage ?? "发送失败");
        }

        return new SendMessageResult(true, clientRequestId, clientRequestId, DateTime.Now, null, null);
    }

    public async Task<SendMessageResult> SendImageAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        CancellationToken cancellationToken = default)
    {
        if (State != WechatConnectionState.Connected)
        {
            return Fail(clientRequestId, "NotConnected", "微信 Hook 未连接");
        }

        if (!File.Exists(localPath))
        {
            return Fail(clientRequestId, "FileNotFound", "图片文件不存在");
        }

        _pending.Register(clientRequestId, conversationId, "[图片]", isFromAi: false);
        var result = await _api.SendImageAsync(conversationId, localPath, cancellationToken);
        if (!result.Success)
        {
            _pending.TryConsumeByClientRequestId(clientRequestId, out _);
            return Fail(clientRequestId, result.ExceptionType ?? "ApiRejected", result.ErrorMessage ?? "图片发送失败");
        }

        return new SendMessageResult(true, clientRequestId, clientRequestId, DateTime.Now, null, null);
    }

    public async Task<SendMessageResult> SendFileAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        CancellationToken cancellationToken = default)
    {
        if (State != WechatConnectionState.Connected)
        {
            return Fail(clientRequestId, "NotConnected", "微信 Hook 未连接");
        }

        if (!File.Exists(localPath))
        {
            return Fail(clientRequestId, "FileNotFound", "文件不存在");
        }

        _pending.Register(clientRequestId, conversationId, "[文件]", isFromAi: false);
        var result = await _api.SendFileAsync(conversationId, localPath, cancellationToken);
        if (!result.Success)
        {
            _pending.TryConsumeByClientRequestId(clientRequestId, out _);
            return Fail(clientRequestId, result.ExceptionType ?? "ApiRejected", result.ErrorMessage ?? "文件发送失败");
        }

        return new SendMessageResult(true, clientRequestId, clientRequestId, DateTime.Now, null, null);
    }

    public Task<WechatVersionInfo> DetectVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new WechatVersionInfo(
            "4.1.8.27",
            "hook-api",
            true,
            "Weixin Hook API @ 19088"));
    }

    public void RegisterAiOutgoing(string conversationId, string content, string generationId)
        => _aiOutgoing.Register(conversationId, content, generationId);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync();
        if (_http is not null)
        {
            await _http.DisposeAsync();
        }

        if (_tcp is not null)
        {
            await _tcp.DisposeAsync();
        }
    }

    private async Task StartCallbacksAsync(WechatCallbackMode mode, CancellationToken ct)
    {
        var startHttp = mode is WechatCallbackMode.Auto or WechatCallbackMode.Http or WechatCallbackMode.Both;
        var startTcp = mode is WechatCallbackMode.Auto or WechatCallbackMode.Tcp or WechatCallbackMode.Both;
        if (startHttp && _http is not null)
        {
            try
            {
                await _http.StartAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HTTP callback failed to start");
                if (mode == WechatCallbackMode.Http)
                {
                    throw;
                }
            }
        }

        if (startTcp && _tcp is not null)
        {
            try
            {
                await _tcp.StartAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TCP callback failed to start");
                if (mode == WechatCallbackMode.Tcp)
                {
                    throw;
                }
            }
        }
    }

    private async Task HealthLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RefreshConnectionAsync(token);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "health loop tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshConnectionAsync(CancellationToken token)
    {
        var login = await _api.CheckLoginAsync(token);
        if (login.ExceptionType == "HookApiOffline" ||
            string.Equals(login.ExceptionType, "HookApiOffline", StringComparison.Ordinal))
        {
            SetState(WechatConnectionState.WechatNotRunning); // HookApiOffline mapped for UI
            return;
        }

        if (!login.Success)
        {
            SetState(WechatConnectionState.WaitingForLogin);
            return;
        }

        var wxid = login.Data?.AccountWxid
                   ?? login.Data?.Wxid
                   ?? login.Data?.Data?.Wxid;
        var nick = login.Data?.NickName
                   ?? login.Data?.Nickname
                   ?? login.Data?.Data?.NickName
                   ?? login.Data?.Data?.NickNameAlt
                   ?? "微信用户";
        if (!string.IsNullOrWhiteSpace(wxid))
        {
            _accountWxid = wxid;
            _account = new WechatAccountInfo(wxid, nick, null);
        }

        if (!_initialized)
        {
            SetState(WechatConnectionState.Connecting);
            await _api.WechatInitAsync(token);
            await _api.InitRoomsAsync(token);
            await LoadContactsAsync(token);
            _initialized = true;
        }

        SetState(WechatConnectionState.Connected);
    }

    private async Task LoadContactsAsync(CancellationToken token)
    {
        var friends = await _api.GetContactList2Async(token);
        var rooms = await _api.GetChatroomListAsync(token);
        lock (_gate)
        {
            _friends.Clear();
            _groups.Clear();
            if (friends.Data?.FriendList is { } list)
            {
                foreach (var f in list)
                {
                    if (string.IsNullOrWhiteSpace(f.Wxid))
                    {
                        continue;
                    }

                    var name = FirstNonEmpty(f.Remark, f.NickName, f.Alias, f.Wxid)!;
                    _friends.Add(new BridgeContact(
                        f.Wxid,
                        name,
                        false,
                        f.SmallHeadUrl ?? f.BigHeadUrl,
                        null,
                        null));
                }
            }

            if (rooms.Data?.Data is { } groups)
            {
                foreach (var g in groups)
                {
                    if (string.IsNullOrWhiteSpace(g.Username))
                    {
                        continue;
                    }

                    var name = FirstNonEmpty(g.Remark, g.NickName, g.Username)!;
                    _groups.Add(new BridgeContact(
                        g.Username,
                        name,
                        true,
                        g.SmallHeadUrl ?? g.BigHeadUrl,
                        null,
                        null));
                }
            }
        }

        _logger.LogInformation(
            "Loaded contacts friends={Friends} groups={Groups}",
            _friends.Count,
            _groups.Count);
    }

    private async Task ProcessEventsAsync(CancellationToken token)
    {
        if (_channel is null)
        {
            return;
        }

        await foreach (var ev in _channel.Reader.ReadAllAsync(token))
        {
            try
            {
                HandleEvent(ev);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "bridge event processing failed");
            }
        }
    }

    private void HandleEvent(WechatBridgeEvent ev)
    {
        if (ev.Message is null)
        {
            return;
        }

        var msg = ev.Message;
        // Mark IsFromMe via account wxid when possible
        if (!msg.IsFromMe &&
            !string.IsNullOrWhiteSpace(_accountWxid) &&
            string.Equals(msg.SenderId, _accountWxid, StringComparison.Ordinal))
        {
            msg.IsFromMe = true;
            if (ev.Kind is WechatBridgeEventKind.IncomingPrivateMessage or WechatBridgeEventKind.IncomingGroupMessage)
            {
                ev.Kind = WechatBridgeEventKind.SelfTextMessage;
            }
        }

        if (!_deduper.TryAdd(msg.ConversationId, msg.MessageId))
        {
            return;
        }

        if (msg.IsFromMe || ev.Kind is WechatBridgeEventKind.SelfTextMessage
                or WechatBridgeEventKind.SelfImageMessage or WechatBridgeEventKind.SelfFileMessage)
        {
            if (_aiOutgoing.TryMatch(msg.ConversationId, msg.Content, out _))
            {
                // AI echo — reconcile only, do not raise as remote
                StoreLocal(ToBridgeMessage(msg, isFromMe: true));
                return;
            }

            if (_pending.TryMatchEcho(msg.ConversationId, msg.Content, out _, out _))
            {
                StoreLocal(ToBridgeMessage(msg, isFromMe: true));
                return;
            }

            // Manual self message from phone/other client
            var self = ToBridgeMessage(msg, isFromMe: true);
            StoreLocal(self);
            MessageReceived?.Invoke(this, new BridgeMessageEvent { Message = self });
            return;
        }

        var bridge = ToBridgeMessage(msg, isFromMe: false);
        StoreLocal(bridge);
        MessageReceived?.Invoke(this, new BridgeMessageEvent { Message = bridge });
    }

    private void StoreLocal(BridgeMessage message)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(message.ConversationId, out var list))
            {
                list = [];
                _messages[message.ConversationId] = list;
            }

            if (list.Any(m => m.Id == message.Id))
            {
                return;
            }

            list.Add(message);
            UpdateContactPreview(message);
        }
    }

    private void UpdateContactPreview(BridgeMessage message)
    {
        var all = _friends.Concat(_groups).ToList();
        var idx = all.FindIndex(c => c.Id == message.ConversationId);
        if (idx < 0)
        {
            var contact = new BridgeContact(
                message.ConversationId,
                message.SenderDisplayName ?? message.ConversationId,
                message.IsGroup,
                null,
                message.Content,
                message.Timestamp);
            if (message.IsGroup)
            {
                _groups.Add(contact);
            }
            else
            {
                _friends.Add(contact);
            }

            return;
        }

        var old = all[idx];
        var updated = old with { LastMessage = message.Content, LastMessageTime = message.Timestamp };
        if (old.IsGroup)
        {
            _groups.RemoveAll(c => c.Id == old.Id);
            _groups.Add(updated);
        }
        else
        {
            _friends.RemoveAll(c => c.Id == old.Id);
            _friends.Add(updated);
        }
    }

    private static BridgeMessage ToBridgeMessage(WechatIncomingMessage msg, bool isFromMe)
        => new(
            msg.MessageId,
            msg.ConversationId,
            msg.Content,
            isFromMe,
            msg.IsGroup,
            msg.SenderId,
            msg.SenderDisplayName,
            msg.Timestamp,
            msg.MessageType switch
            {
                "Image" => BridgeMessageKind.Image,
                "File" => BridgeMessageKind.File,
                "System" => BridgeMessageKind.System,
                _ => BridgeMessageKind.Text
            },
            msg.MentionsMe,
            msg.IsReplyToMe,
            msg.QuoteMessageId,
            msg.ImagePath ?? msg.FilePath,
            msg.FileName,
            msg.FileSize);

    private void SetState(WechatConnectionState state)
    {
        WechatConnectionState previous;
        lock (_gate)
        {
            previous = _state;
            if (previous == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }

    private static SendMessageResult Fail(string clientRequestId, string code, string message)
        => new(false, null, clientRequestId, DateTime.Now, code, message);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

public sealed class PendingAiOutgoingRegistry
{
    private readonly List<(string ConversationId, string Content, string GenerationId, DateTime At)> _items = [];
    private readonly object _gate = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(3);

    public void Register(string conversationId, string content, string generationId)
    {
        lock (_gate)
        {
            Prune();
            _items.Add((conversationId, content ?? "", generationId, DateTime.UtcNow));
        }
    }

    public bool TryMatch(string conversationId, string content, out string? generationId)
    {
        generationId = null;
        lock (_gate)
        {
            Prune();
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(item.Content, content, StringComparison.Ordinal))
                {
                    continue;
                }

                generationId = item.GenerationId;
                _items.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private void Prune()
    {
        var now = DateTime.UtcNow;
        _items.RemoveAll(i => now - i.At > _ttl);
    }
}

public sealed class GroupMemberCache
{
    private readonly Dictionary<string, (string Nick, DateTime At)> _cache = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public bool TryGet(string roomId, string memberWxid, out string? nick)
    {
        nick = null;
        var key = $"{roomId}|{memberWxid}";
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var item) && DateTime.UtcNow - item.At < TimeSpan.FromHours(6))
            {
                nick = item.Nick;
                return true;
            }
        }

        return false;
    }

    public void Set(string roomId, string memberWxid, string nick)
    {
        var key = $"{roomId}|{memberWxid}";
        lock (_gate)
        {
            _cache[key] = (nick, DateTime.UtcNow);
        }
    }
}
