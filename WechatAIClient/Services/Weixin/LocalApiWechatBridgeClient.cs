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
    private readonly GroupMemberCache _memberCache = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
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
    private bool _isApiReachable;
    private WechatCallbackMode _callbackMode = WechatCallbackMode.Auto;
    private int _httpCallbackPort = 5000;
    private int _tcpCallbackPort = 61108;

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

    public bool HttpCallbackRunning => _http?.IsRunning == true;
    public bool TcpCallbackRunning => _tcp?.IsRunning == true;
    public bool CallbackAvailable => HttpCallbackRunning || TcpCallbackRunning;
    public bool IsApiReachable
    {
        get
        {
            lock (_gate)
            {
                return _isApiReachable;
            }
        }
    }

    /// <summary>Logged-in account wxid once known.</summary>
    public string AccountId
    {
        get
        {
            lock (_gate)
            {
                return _accountWxid ?? string.Empty;
            }
        }
    }

    public event EventHandler<WechatConnectionState>? StateChanged;
    public event EventHandler<BridgeMessageEvent>? MessageReceived;
    public event EventHandler<OutgoingAcknowledgedEvent>? OutgoingAcknowledged;
#pragma warning disable CS0067 // May fire on fatal bridge failures in future paths.
    public event EventHandler? BridgeCrashed;
#pragma warning restore CS0067

    public void Configure(
        string? baseUrl,
        WechatCallbackMode mode,
        int httpPort = 5000,
        int tcpPort = 61108)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl) && _api is LocalWeixinApiClient concrete)
        {
            concrete.BaseUrl = baseUrl;
        }

        _callbackMode = mode;
        _httpCallbackPort = httpPort > 0 ? httpPort : 5000;
        _tcpCallbackPort = tcpPort > 0 ? tcpPort : 61108;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cts is not null)
            {
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                _channel = Channel.CreateBounded<WechatBridgeEvent>(new BoundedChannelOptions(2000)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                });

                _http = new WechatHttpCallbackServer(
                    _parser,
                    _channel.Writer,
                    _loggerFactory.CreateLogger<WechatHttpCallbackServer>(),
                    _httpCallbackPort);
                _tcp = new WechatTcpCallbackServer(
                    _parser,
                    _channel.Writer,
                    _loggerFactory.CreateLogger<WechatTcpCallbackServer>(),
                    _tcpCallbackPort);

                await StartCallbacksAsync(_callbackMode, cancellationToken);
                SetState(WechatConnectionState.Connecting);
                await RefreshConnectionAsync(cancellationToken);

                // Health + processor only after first refresh (no race with init).
                _processor = Task.Run(() => ProcessEventsAsync(_cts.Token), CancellationToken.None);
                _healthLoop = Task.Run(() => HealthLoopAsync(_cts.Token), CancellationToken.None);
            }
            catch
            {
                await RollbackStartupAsync();
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await StopCoreAsync();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _initialized = false;
            SetState(WechatConnectionState.Connecting);
            await RefreshConnectionAsync(cancellationToken);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task<WechatAccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var ok = _state is WechatConnectionState.Connected or WechatConnectionState.Degraded;
            return Task.FromResult(ok ? _account : null);
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
            // Only contacts with real activity — never fake first 50.
            var recent = _friends.Concat(_groups)
                .Where(c => c.LastMessageTime is not null)
                .OrderByDescending(c => c.LastMessageTime)
                .ToList();
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
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSendReady())
        {
            return Fail(clientRequestId, "NotConnected", "微信 Hook 未连接");
        }

        _pending.Register(clientRequestId, conversationId, text, isFromAi);
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
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSendReady())
        {
            return Fail(clientRequestId, "NotConnected", "微信 Hook 未连接");
        }

        if (!File.Exists(localPath))
        {
            return Fail(clientRequestId, "FileNotFound", "图片文件不存在");
        }

        _pending.Register(clientRequestId, conversationId, "[图片]", isFromAi);
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
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSendReady())
        {
            return Fail(clientRequestId, "NotConnected", "微信 Hook 未连接");
        }

        if (!File.Exists(localPath))
        {
            return Fail(clientRequestId, "FileNotFound", "文件不存在");
        }

        _pending.Register(clientRequestId, conversationId, "[文件]", isFromAi);
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _lifecycleGate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await StopCoreAsync();
        }
        finally
        {
            _lifecycleGate.Release();
        }

        _refreshGate.Dispose();
        _lifecycleGate.Dispose();
    }

    private bool IsSendReady()
        => State is WechatConnectionState.Connected or WechatConnectionState.Degraded;

    private async Task StopCoreAsync()
    {
        var cts = _cts;
        var processor = _processor;
        var health = _healthLoop;
        var http = _http;
        var tcp = _tcp;
        var channel = _channel;

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        if (http is not null)
        {
            try
            {
                await http.StopAsync();
            }
            catch
            {
                // ignore
            }
        }

        if (tcp is not null)
        {
            try
            {
                await tcp.StopAsync();
            }
            catch
            {
                // ignore
            }
        }

        channel?.Writer.TryComplete();

        await WaitTaskAsync(processor, TimeSpan.FromSeconds(3));
        await WaitTaskAsync(health, TimeSpan.FromSeconds(3));

        if (cts is not null)
        {
            try
            {
                cts.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        _cts = null;
        _processor = null;
        _healthLoop = null;
        _channel = null;
        _http = null;
        _tcp = null;
        _initialized = false;
        _pending.Clear();
        SetState(WechatConnectionState.Disconnected);
    }

    private async Task RollbackStartupAsync()
    {
        try
        {
            await StopCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RollbackStartupAsync cleanup failed");
        }

        _cts = null;
        _processor = null;
        _healthLoop = null;
        _channel = null;
        _http = null;
        _tcp = null;
        _initialized = false;
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

        if ((mode is WechatCallbackMode.Auto or WechatCallbackMode.Both) &&
            !CallbackAvailable)
        {
            _logger.LogWarning("No callback transport available (HTTP/TCP both failed)");
        }
    }

    private async Task HealthLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await RefreshConnectionAsync(token);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "health loop tick failed");
            }
        }
    }

    private async Task RefreshConnectionAsync(CancellationToken token)
    {
        await _refreshGate.WaitAsync(token);
        try
        {
            await RefreshConnectionCoreAsync(token);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshConnectionCoreAsync(CancellationToken token)
    {
        var reachable = false;
        try
        {
            reachable = await _api.IsApiReachableAsync(token);
        }
        catch
        {
            reachable = false;
        }

        lock (_gate)
        {
            _isApiReachable = reachable;
        }

        var login = await _api.CheckLoginAsync(token);
        if (login.ExceptionType == "HookApiOffline" ||
            string.Equals(login.ExceptionType, "HookApiOffline", StringComparison.Ordinal))
        {
            SetState(WechatConnectionState.WechatNotRunning);
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
            if (!string.IsNullOrWhiteSpace(_accountWxid) &&
                !string.Equals(_accountWxid, wxid, StringComparison.Ordinal))
            {
                _logger.LogWarning("Account wxid changed {Old} → {New}; clearing caches", _accountWxid, wxid);
                ClearAccountCaches();
                _initialized = false;
            }

            _accountWxid = wxid;
            _account = new WechatAccountInfo(wxid, nick, null);
        }

        if (!_initialized)
        {
            SetState(WechatConnectionState.Connecting);

            var init = await _api.WechatInitAsync(token);
            if (!init.Success)
            {
                _logger.LogWarning("wechat_init failed: {Error}", init.ErrorMessage);
                SetState(WechatConnectionState.BridgeError);
                return;
            }

            var rooms = await _api.InitRoomsAsync(token);
            if (!rooms.Success)
            {
                _logger.LogWarning("init_rooms failed: {Error}", rooms.ErrorMessage);
                SetState(WechatConnectionState.BridgeError);
                return;
            }

            var load = await LoadContactsAsync(token);
            if (load == ContactLoadResult.BothFailed)
            {
                _logger.LogWarning("Both contact and group list failed");
                SetState(WechatConnectionState.BridgeError);
                return;
            }

            _initialized = true;

            if (!CallbackAvailable || load == ContactLoadResult.Partial)
            {
                SetState(WechatConnectionState.Degraded);
                return;
            }

            SetState(WechatConnectionState.Connected);
            return;
        }

        // Already initialized — refresh capability state.
        if (!CallbackAvailable)
        {
            SetState(WechatConnectionState.Degraded);
            return;
        }

        SetState(WechatConnectionState.Connected);
    }

    private enum ContactLoadResult
    {
        Ok,
        Partial,
        BothFailed
    }

    private async Task<ContactLoadResult> LoadContactsAsync(CancellationToken token)
    {
        var friends = await _api.GetContactList2Async(token);
        var rooms = await _api.GetChatroomListAsync(token);
        var friendsOk = friends.Success;
        var roomsOk = rooms.Success;

        lock (_gate)
        {
            if (friendsOk)
            {
                _friends.Clear();
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
            }

            if (roomsOk)
            {
                _groups.Clear();
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
        }

        _logger.LogInformation(
            "Loaded contacts friendsOk={FriendsOk} count={Friends} groupsOk={GroupsOk} count={Groups}",
            friendsOk, _friends.Count, roomsOk, _groups.Count);

        if (!friendsOk && !roomsOk)
        {
            return ContactLoadResult.BothFailed;
        }

        if (!friendsOk || !roomsOk)
        {
            return ContactLoadResult.Partial;
        }

        return ContactLoadResult.Ok;
    }

    private void ClearAccountCaches()
    {
        lock (_gate)
        {
            _friends.Clear();
            _groups.Clear();
            _messages.Clear();
        }

        _deduper.Clear();
        _memberCache.Clear();
        _pending.Clear();
    }

    private async Task ProcessEventsAsync(CancellationToken token)
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
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
        catch (OperationCanceledException)
        {
            // expected on stop
        }
    }

    private void HandleEvent(WechatBridgeEvent ev)
    {
        if (ev.Message is null)
        {
            return;
        }

        var msg = ev.Message;
        NormalizeConversation(msg);

        if (!msg.IsFromMe &&
            !string.IsNullOrWhiteSpace(_accountWxid) &&
            string.Equals(msg.FromWxid ?? msg.SenderId, _accountWxid, StringComparison.Ordinal))
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
            var normalizedContent = PendingOutgoingTracker.NormalizeContent(msg.Content);
            if (_pending.TryMatchEcho(msg.ConversationId, normalizedContent, out var matchSource, out var clientRequestId) &&
                !string.IsNullOrWhiteSpace(clientRequestId))
            {
                var ack = ToBridgeMessage(msg, isFromMe: true);
                StoreLocal(ack);
                OutgoingAcknowledged?.Invoke(this, new OutgoingAcknowledgedEvent
                {
                    AccountId = AccountId,
                    ClientRequestId = clientRequestId,
                    RealMessageId = msg.MessageId,
                    ConversationId = msg.ConversationId,
                    IsFromAi = matchSource == OutgoingMatchSource.AiGenerated,
                    Message = ack
                });
                return;
            }

            // Unmatched self (phone / other client) — raise as MessageReceived with IsFromMe.
            var self = ToBridgeMessage(msg, isFromMe: true);
            StoreLocal(self);
            MessageReceived?.Invoke(this, new BridgeMessageEvent { Message = self });
            return;
        }

        var bridge = ToBridgeMessage(msg, isFromMe: false);
        StoreLocal(bridge);
        MessageReceived?.Invoke(this, new BridgeMessageEvent { Message = bridge });
    }

    /// <summary>
    /// group → room; private incoming (from != account) → from; private outgoing (from == account) → to.
    /// </summary>
    private void NormalizeConversation(WechatIncomingMessage msg)
        => WechatConversationNormalizer.Apply(msg, _accountWxid);

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
            msg.SenderId ?? msg.FromWxid,
            msg.SenderDisplayName,
            msg.Timestamp,
            msg.MessageType switch
            {
                "Image" => BridgeMessageKind.Image,
                "File" => BridgeMessageKind.File,
                "Emoji" => BridgeMessageKind.Emoji,
                "Video" => BridgeMessageKind.Video,
                "Voice" => BridgeMessageKind.Voice,
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

            // Leaving Connected/Degraded → offline/waiting: require re-init next time.
            if (previous is WechatConnectionState.Connected or WechatConnectionState.Degraded &&
                state is not WechatConnectionState.Connected and not WechatConnectionState.Degraded
                    and not WechatConnectionState.Connecting)
            {
                _initialized = false;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }

    private static async Task WaitTaskAsync(Task? task, TimeSpan timeout)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.WaitAsync(timeout);
        }
        catch
        {
            // timeout / cancel — ignore
        }
    }

    private static SendMessageResult Fail(string clientRequestId, string code, string message)
        => new(false, null, clientRequestId, DateTime.Now, code, message);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
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

    public void Clear()
    {
        lock (_gate)
        {
            _cache.Clear();
        }
    }
}
