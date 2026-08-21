using WechatAIClient.Models;

namespace WechatAIClient.Services.Wechat;

/// <summary>
/// Full in-memory bridge for tests — no real WeChat process required.
/// </summary>
public sealed class FakeWechatBridgeClient : IWechatBridgeClient
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<BridgeMessage>> _messages = new(StringComparer.Ordinal);
    private readonly List<BridgeContact> _contacts = [];
    private readonly List<BridgeContact> _groups = [];
    private readonly PendingOutgoingTracker _pending = new();
    private WechatConnectionState _state = WechatConnectionState.Disconnected;
    private WechatAccountInfo? _account;
    private WechatVersionInfo _version = new("0.0.0.0", string.Empty, false, "未检测");
    private bool _disposed;
    // StartAsync marks lifecycle; exposed for tests/diagnostics.
    public bool IsStarted { get; private set; }
    private bool _forceSendFail;

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
    public event EventHandler<OutgoingAcknowledgedEvent>? OutgoingAcknowledged;
    public event EventHandler? BridgeCrashed;

    public bool ForceSendFail
    {
        get
        {
            lock (_gate)
            {
                return _forceSendFail;
            }
        }
        set
        {
            lock (_gate)
            {
                _forceSendFail = value;
            }
        }
    }

    public void SeedContact(BridgeContact contact)
    {
        lock (_gate)
        {
            if (contact.IsGroup)
            {
                _groups.RemoveAll(c => c.Id == contact.Id);
                _groups.Add(contact);
            }
            else
            {
                _contacts.RemoveAll(c => c.Id == contact.Id);
                _contacts.Add(contact);
            }
        }
    }

    public void SetAccount(WechatAccountInfo account)
    {
        lock (_gate)
        {
            _account = account;
        }
    }

    public void SetVersion(WechatVersionInfo version)
    {
        lock (_gate)
        {
            _version = version;
        }
    }

    public void SetState(WechatConnectionState state) => SetStateInternal(state);

    public void InjectMessage(BridgeMessage message, bool raiseEvent = true)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(message.ConversationId, out var list))
            {
                list = [];
                _messages[message.ConversationId] = list;
            }

            // Reconcile IsFromMe echo with prior send of same content (avoid duplicate rows).
            if (message.IsFromMe)
            {
                var existing = list.LastOrDefault(m =>
                    m.IsFromMe &&
                    string.Equals(m.Content, message.Content, StringComparison.Ordinal));
                if (existing is not null)
                {
                    list.Remove(existing);
                    list.Add(message);
                }
                else if (list.All(m => m.Id != message.Id))
                {
                    list.Add(message);
                }
            }
            else if (list.All(m => m.Id != message.Id))
            {
                list.Add(message);
            }
        }

        if (!raiseEvent)
        {
            return;
        }

        if (message.IsFromMe &&
            _pending.TryMatchEcho(
                message.ConversationId,
                message.Content,
                out var matchSource,
                out var clientRequestId) &&
            !string.IsNullOrWhiteSpace(clientRequestId))
        {
            OutgoingAcknowledged?.Invoke(this, new OutgoingAcknowledgedEvent
            {
                ClientRequestId = clientRequestId,
                RealMessageId = message.Id,
                ConversationId = message.ConversationId,
                IsFromAi = matchSource == OutgoingMatchSource.AiGenerated,
                Message = message
            });
            return;
        }

        MessageReceived?.Invoke(this, new BridgeMessageEvent { Message = message });
    }

    public void TriggerCrash()
    {
        SetStateInternal(WechatConnectionState.BridgeError);
        BridgeCrashed?.Invoke(this, EventArgs.Empty);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IsStarted = true;
            // Only auto-connect from Disconnected. Explicit WechatNotRunning / VersionUnsupported stay put.
            if (_state == WechatConnectionState.Disconnected)
            {
                if (_version.IsSupported || string.Equals(_version.ProductVersion, "fake", StringComparison.OrdinalIgnoreCase))
                {
                    _account ??= new WechatAccountInfo("fake-user", "测试账号", null);
                    _state = WechatConnectionState.Connected;
                }
                else if (!_version.IsSupported && !string.IsNullOrWhiteSpace(_version.ProductVersion) &&
                         _version.ProductVersion != "0.0.0.0")
                {
                    _state = WechatConnectionState.VersionUnsupported;
                }
                else
                {
                    _account ??= new WechatAccountInfo("fake-user", "测试账号", null);
                    _version = new WechatVersionInfo("fake", "fake", true, null);
                    _state = WechatConnectionState.Connected;
                }
            }
        }

        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IsStarted = false;
            _state = WechatConnectionState.Disconnected;
        }

        StateChanged?.Invoke(this, WechatConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetStateInternal(WechatConnectionState.Connecting);
        lock (_gate)
        {
            if (_version.IsSupported ||
                string.Equals(_version.ProductVersion, "fake", StringComparison.OrdinalIgnoreCase) ||
                _version.ProductVersion == "0.0.0.0")
            {
                _account ??= new WechatAccountInfo("fake-user", "测试账号", null);
                if (_version.ProductVersion == "0.0.0.0")
                {
                    _version = new WechatVersionInfo("fake", "fake", true, null);
                }

                _state = WechatConnectionState.Connected;
            }
            else
            {
                _state = WechatConnectionState.VersionUnsupported;
            }
        }

        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public Task<WechatAccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_account);
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_state is WechatConnectionState.VersionUnsupported or WechatConnectionState.WechatNotRunning
                or WechatConnectionState.Disconnected or WechatConnectionState.BridgeError)
            {
                return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
            }

            return Task.FromResult<IReadOnlyList<BridgeContact>>(_contacts.ToList());
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_state is not WechatConnectionState.Connected and not WechatConnectionState.WaitingForLogin
                and not WechatConnectionState.Connecting)
            {
                if (_state is WechatConnectionState.VersionUnsupported or WechatConnectionState.WechatNotRunning)
                {
                    return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
                }
            }

            if (_state is WechatConnectionState.VersionUnsupported or WechatConnectionState.WechatNotRunning
                or WechatConnectionState.Disconnected or WechatConnectionState.BridgeError)
            {
                return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
            }

            var all = _contacts.Concat(_groups)
                .Where(c => c.LastMessageTime is not null)
                .OrderByDescending(c => c.LastMessageTime)
                .ToList();
            return Task.FromResult<IReadOnlyList<BridgeContact>>(all);
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_state is WechatConnectionState.VersionUnsupported or WechatConnectionState.WechatNotRunning
                or WechatConnectionState.Disconnected or WechatConnectionState.BridgeError)
            {
                return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
            }

            return Task.FromResult<IReadOnlyList<BridgeContact>>(_groups.ToList());
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

            var take = Math.Max(1, limit);
            return Task.FromResult<IReadOnlyList<BridgeMessage>>(list.TakeLast(take).ToList());
        }
    }

    public Task<SendMessageResult> SendTextAsync(
        string conversationId,
        string text,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.Now;
        lock (_gate)
        {
            if (_state != WechatConnectionState.Connected)
            {
                return Task.FromResult(new SendMessageResult(
                    false, null, clientRequestId, now, "NotConnected", "微信未连接"));
            }

            if (_forceSendFail)
            {
                return Task.FromResult(new SendMessageResult(
                    false, null, clientRequestId, now, "SendFailed", "模拟发送失败"));
            }

            _pending.Register(clientRequestId, conversationId, text, isFromAi);
            var id = Guid.NewGuid().ToString("N");
            var msg = new BridgeMessage(
                id,
                conversationId,
                text,
                IsFromMe: true,
                IsGroup: _groups.Any(g => g.Id == conversationId),
                SenderId: _account?.UserId,
                SenderDisplayName: _account?.DisplayName ?? "我",
                Timestamp: now);

            if (!_messages.TryGetValue(conversationId, out var list))
            {
                list = [];
                _messages[conversationId] = list;
            }

            list.Add(msg);
            return Task.FromResult(new SendMessageResult(true, id, clientRequestId, now, null, null));
        }
    }

    public Task<SendMessageResult> SendImageAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isFromAi;
        return Task.FromResult(new SendMessageResult(
            false, null, clientRequestId, DateTime.Now, "NotSupported", "图片发送暂未实现"));
    }

    public Task<SendMessageResult> SendFileAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isFromAi;
        return Task.FromResult(new SendMessageResult(
            false, null, clientRequestId, DateTime.Now, "NotSupported", "文件发送暂未实现"));
    }

    public Task<WechatVersionInfo> DetectVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_version);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        lock (_gate)
        {
            IsStarted = false;
            _state = WechatConnectionState.Disconnected;
        }

        return ValueTask.CompletedTask;
    }

    private void SetStateInternal(WechatConnectionState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }
}
