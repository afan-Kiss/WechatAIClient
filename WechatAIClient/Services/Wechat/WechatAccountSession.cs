using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services.Media;
using WechatAIClient.Services.Weixin;

namespace WechatAIClient.Services.Wechat;

/// <summary>One WeChat account: own API client, bridge, contacts and message caches.</summary>
public sealed class WechatAccountSession : IAsyncDisposable
{
    private readonly IWechatBridgeClient _bridge;
    private readonly IMediaCacheService? _mediaCache;
    private readonly ILogger<WechatAccountSession> _logger;
    private readonly MessageDeduplicator _deduper = new();
    private readonly PendingOutgoingTracker _pending = new();
    private readonly GroupMemberCache _memberCache = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _identityRefreshGate = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<string, List<ChatMessage>> _messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Contact> _contacts = new(StringComparer.Ordinal);
    private readonly bool _ownsBridge;
    private CancellationTokenSource? _sessionCts = new();
    private WechatAccountIdentity? _identity;
    private WechatConnectionState _state = WechatConnectionState.Disconnected;
    private bool _started;
    private bool _disposed;

    public WechatAccountSession(
        WechatAccountConnectionProfile profile,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IWechatCallbackParser parser,
        IMediaCacheService? mediaCache = null,
        WechatCallbackMode callbackMode = WechatCallbackMode.Auto)
    {
        Profile = profile;
        _mediaCache = mediaCache;
        _logger = loggerFactory.CreateLogger<WechatAccountSession>();
        _ownsBridge = true;

        var api = new LocalWeixinApiClient(
            httpClientFactory,
            loggerFactory.CreateLogger<LocalWeixinApiClient>());
        api.BaseUrl = profile.BaseUrl;

        var bridge = new LocalApiWechatBridgeClient(api, parser, loggerFactory);
        bridge.Configure(profile.BaseUrl, callbackMode, profile.HttpCallbackPort, profile.TcpCallbackPort);
        _bridge = bridge;

        WireBridge();
        AccountId = profile.ExpectedAccountWxid ?? profile.ProfileId;
    }

    /// <summary>Test / mock path — inject an existing bridge (e.g. FakeWechatBridgeClient).</summary>
    public WechatAccountSession(
        WechatAccountConnectionProfile profile,
        IWechatBridgeClient bridge,
        ILogger<WechatAccountSession> logger,
        IMediaCacheService? mediaCache = null,
        bool ownsBridge = false)
    {
        Profile = profile;
        _bridge = bridge;
        _logger = logger;
        _mediaCache = mediaCache;
        _ownsBridge = ownsBridge;
        WireBridge();
        AccountId = profile.ExpectedAccountWxid ?? profile.ProfileId;
    }

    public WechatAccountConnectionProfile Profile { get; }
    public WechatAccountIdentity? Identity
    {
        get
        {
            lock (_gate)
            {
                return _identity;
            }
        }
    }

    public string AccountId { get; private set; }

    public bool IsStarted
    {
        get
        {
            lock (_gate)
            {
                return _started;
            }
        }
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

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<WechatConnectionState>? StateChanged;
    public event EventHandler<OutgoingAcknowledgedEvent>? OutgoingAcknowledged;
    public event EventHandler<AccountIdentityChangedEventArgs>? IdentityChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            EnsureSessionCts();
            await _bridge.StartAsync(cancellationToken);
            try
            {
                await RefreshIdentityAsync(cancellationToken);
                await RefreshContactsCacheAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Post-start refresh degraded for profile {ProfileId}", Profile.ProfileId);
                lock (_gate)
                {
                    if (_state is WechatConnectionState.Connected)
                    {
                        _state = WechatConnectionState.Degraded;
                    }
                }
            }

            lock (_gate)
            {
                _started = true;
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
            CancelSessionCts();
            _pending.Clear();
            await _bridge.StopAsync(cancellationToken);
            lock (_gate)
            {
                _started = false;
            }

            EnsureSessionCts();
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
            EnsureSessionCts();
            await _bridge.ReconnectAsync(cancellationToken);
            await RefreshIdentityAsync(cancellationToken);
            await RefreshContactsCacheAsync(cancellationToken);
            lock (_gate)
            {
                _started = true;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task<WechatAccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default)
        => _bridge.GetAccountAsync(cancellationToken);

    public async Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var list = await _bridge.GetContactsAsync(cancellationToken);
        return StoreContacts(list.Select(MapContact));
    }

    public async Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var list = await _bridge.GetGroupsAsync(cancellationToken);
        return StoreContacts(list.Select(MapContact));
    }

    public async Task<IReadOnlyList<Contact>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var list = await _bridge.GetRecentAsync(cancellationToken);
        return StoreContacts(list.Select(MapContact));
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var bridgeMessages = await _bridge.GetMessagesAsync(conversationId, 200, cancellationToken);
        var mapped = bridgeMessages.Select(MapMessage).ToList();
        lock (_gate)
        {
            _messages[conversationId] = mapped.ToList();
        }

        foreach (var m in mapped)
        {
            ScheduleMediaLoad(m);
            ScheduleSenderAvatar(m);
        }

        return mapped;
    }

    public bool TryGetContact(string conversationId, out Contact? contact)
    {
        lock (_gate)
        {
            return _contacts.TryGetValue(conversationId, out contact);
        }
    }

    public IReadOnlyList<Contact> SnapshotContacts()
    {
        lock (_gate)
        {
            return _contacts.Values.ToList();
        }
    }

    public IReadOnlyList<ChatMessage> SnapshotMessages(string conversationId)
    {
        lock (_gate)
        {
            return _messages.TryGetValue(conversationId, out var list)
                ? list.ToList()
                : Array.Empty<ChatMessage>();
        }
    }

    public void ClearCaches()
    {
        lock (_gate)
        {
            ClearAccountCachesLocked();
        }
    }

    public async Task<ChatMessage> SendMessageAsync(
        string conversationId,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        if (type == MessageType.Image && !string.IsNullOrWhiteSpace(imagePath))
        {
            var clientId = Guid.NewGuid().ToString("N");
            _pending.Register(clientId, conversationId, "[图片]", isFromAi);
            var result = await _bridge.SendImageAsync(conversationId, imagePath, clientId, isFromAi, cancellationToken);
            return StoreOutgoing(conversationId, "[图片]", MessageType.Image, isFromAi, clientId, result, imagePath, fileName);
        }

        if (type == MessageType.File && !string.IsNullOrWhiteSpace(imagePath ?? fileName))
        {
            var path = imagePath ?? fileName ?? string.Empty;
            var clientId = Guid.NewGuid().ToString("N");
            _pending.Register(clientId, conversationId, "[文件]", isFromAi);
            var result = await _bridge.SendFileAsync(conversationId, path, clientId, isFromAi, cancellationToken);
            return StoreOutgoing(conversationId, "【文件消息】", MessageType.File, isFromAi, clientId, result, path, fileName, fileSize);
        }

        var send = await SendTextMessageAsync(conversationId, content, isFromAi, null, cancellationToken);
        lock (_gate)
        {
            if (_messages.TryGetValue(conversationId, out var list))
            {
                var existing = list.FirstOrDefault(m =>
                    string.Equals(m.ClientRequestId, send.ClientRequestId, StringComparison.Ordinal));
                if (existing is not null)
                {
                    return existing;
                }
            }
        }

        return StoreOutgoing(conversationId, content, MessageType.Text, isFromAi, send.ClientRequestId, send);
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        string conversationId,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var clientId = string.IsNullOrWhiteSpace(clientRequestId)
            ? Guid.NewGuid().ToString("N")
            : clientRequestId;
        var text = content ?? string.Empty;
        _pending.Register(clientId, conversationId, text, isFromAi);
        var result = await _bridge.SendTextAsync(conversationId, text, clientId, isFromAi, cancellationToken);
        var message = StoreOutgoing(conversationId, text, MessageType.Text, isFromAi, clientId, result);
        if (!result.Success)
        {
            _pending.TryConsumeByClientRequestId(clientId, out _);
        }

        return result with { MessageId = message.Id };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelSessionCts();
        _bridge.StateChanged -= OnBridgeStateChanged;
        _bridge.MessageReceived -= OnBridgeMessage;
        _bridge.OutgoingAcknowledged -= OnOutgoingAcknowledged;
        _pending.Clear();
        if (_ownsBridge)
        {
            await _bridge.DisposeAsync();
        }

        _lifecycleGate.Dispose();
        _identityRefreshGate.Dispose();
        _sessionCts?.Dispose();
    }

    private void WireBridge()
    {
        _bridge.StateChanged += OnBridgeStateChanged;
        _bridge.MessageReceived += OnBridgeMessage;
        _bridge.OutgoingAcknowledged += OnOutgoingAcknowledged;
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            EnsureSessionCts();
            await _bridge.StartAsync(cancellationToken);
            try
            {
                await RefreshIdentityAsync(cancellationToken);
                await RefreshContactsCacheAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EnsureStarted refresh degraded for {ProfileId}", Profile.ProfileId);
            }

            lock (_gate)
            {
                _started = true;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task RefreshIdentityAsync(CancellationToken cancellationToken)
    {
        await _identityRefreshGate.WaitAsync(cancellationToken);
        try
        {
            var account = await _bridge.GetAccountAsync(cancellationToken);
            if (account is null || string.IsNullOrWhiteSpace(account.UserId))
            {
                return;
            }

            ApplyIdentity(account.UserId, account.DisplayName, account.AvatarPath);
        }
        finally
        {
            _identityRefreshGate.Release();
        }
    }

    private void ApplyIdentity(string wxid, string displayName, string? avatar)
    {
        string? oldId;
        var accountChanged = false;
        lock (_gate)
        {
            // Prefer live identity; fall back to provisional AccountId (ExpectedWxid/ProfileId).
            oldId = _identity?.AccountId;
            if (string.IsNullOrWhiteSpace(oldId) &&
                !string.Equals(AccountId, Profile.ProfileId, StringComparison.Ordinal))
            {
                oldId = AccountId;
            }

            if (string.Equals(_identity?.AccountId, wxid, StringComparison.Ordinal) &&
                _identity is not null &&
                string.Equals(_identity.DisplayName, displayName, StringComparison.Ordinal) &&
                string.Equals(_identity.AvatarUrl, avatar, StringComparison.Ordinal))
            {
                return;
            }

            accountChanged = !string.IsNullOrWhiteSpace(oldId) &&
                             !string.Equals(oldId, wxid, StringComparison.Ordinal);
            if (accountChanged || _identity is null)
            {
                if (accountChanged)
                {
                    ClearAccountCachesLocked();
                }
            }

            _identity = new WechatAccountIdentity(wxid, wxid, displayName, avatar);
            AccountId = wxid;
        }

        IdentityChanged?.Invoke(this, new AccountIdentityChangedEventArgs
        {
            ProfileId = Profile.ProfileId,
            OldAccountId = string.Equals(oldId, wxid, StringComparison.Ordinal) ? null : oldId,
            NewAccountId = wxid
        });
    }

    private void ClearAccountCachesLocked()
    {
        _contacts.Clear();
        _messages.Clear();
        _deduper.Clear();
        _pending.Clear();
        _memberCache.Clear();
        CancelSessionCts();
        EnsureSessionCts();
    }

    private async Task RefreshContactsCacheAsync(CancellationToken cancellationToken)
    {
        Exception? firstError = null;
        var okCount = 0;
        async Task<IReadOnlyList<BridgeContact>> LoadOne(Func<CancellationToken, Task<IReadOnlyList<BridgeContact>>> loader)
        {
            try
            {
                var list = await loader(cancellationToken);
                Interlocked.Increment(ref okCount);
                return list;
            }
            catch (Exception ex)
            {
                firstError ??= ex;
                _logger.LogDebug(ex, "Partial contacts refresh failed for {ProfileId}", Profile.ProfileId);
                return Array.Empty<BridgeContact>();
            }
        }

        var recentTask = LoadOne(_bridge.GetRecentAsync);
        var friendsTask = LoadOne(_bridge.GetContactsAsync);
        var groupsTask = LoadOne(_bridge.GetGroupsAsync);
        await Task.WhenAll(recentTask, friendsTask, groupsTask);
        StoreContacts(recentTask.Result.Concat(friendsTask.Result).Concat(groupsTask.Result).Select(MapContact));

        if (okCount == 0 && firstError is not null)
        {
            lock (_gate)
            {
                if (_state is WechatConnectionState.Connected)
                {
                    _state = WechatConnectionState.Degraded;
                }
            }

            StateChanged?.Invoke(this, State);
            throw firstError;
        }

        if (okCount is > 0 and < 3)
        {
            lock (_gate)
            {
                if (_state is WechatConnectionState.Connected)
                {
                    _state = WechatConnectionState.Degraded;
                }
            }

            StateChanged?.Invoke(this, State);
        }
    }

    private IReadOnlyList<Contact> StoreContacts(IEnumerable<Contact> contacts)
    {
        lock (_gate)
        {
            foreach (var incoming in contacts)
            {
                incoming.AccountDisplayName = Identity?.DisplayName ?? Profile.DisplayName;
                if (_contacts.TryGetValue(incoming.Id, out var existing))
                {
                    var urlChanged = !string.Equals(existing.AvatarUrl, incoming.AvatarUrl, StringComparison.Ordinal);
                    var needsAvatar = urlChanged ||
                                      string.IsNullOrWhiteSpace(existing.AvatarLocalPath) ||
                                      existing.AvatarLoadState is AvatarLoadState.None or AvatarLoadState.Failed;
                    MergeContact(existing, incoming);
                    if (needsAvatar)
                    {
                        ScheduleAvatarLoad(existing);
                    }
                }
                else
                {
                    _contacts[incoming.Id] = incoming;
                    ScheduleAvatarLoad(incoming);
                }
            }

            return _contacts.Values.ToList();
        }
    }

    private static void MergeContact(Contact existing, Contact incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming.Name))
        {
            existing.Name = incoming.Name;
        }

        existing.Type = incoming.Type;
        existing.AvatarColor = incoming.AvatarColor;
        existing.AvatarInitials = incoming.AvatarInitials;
        if (!string.IsNullOrWhiteSpace(incoming.AvatarUrl))
        {
            existing.AvatarUrl = incoming.AvatarUrl;
        }

        if (incoming.HasLastActivity)
        {
            existing.LastMessage = incoming.LastMessage;
            existing.LastSender = incoming.LastSender;
            existing.LastMessageTime = incoming.LastMessageTime;
            existing.HasLastActivity = true;
        }

        existing.MemberCount = incoming.MemberCount;
        existing.IsOnline = incoming.IsOnline;
        existing.AccountDisplayName = incoming.AccountDisplayName;
    }

    private void OnBridgeStateChanged(object? sender, WechatConnectionState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        if (state is WechatConnectionState.Connected or WechatConnectionState.Degraded)
        {
            var token = _sessionCts?.Token ?? CancellationToken.None;
            SafeFireAndForget(ct => RefreshIdentityAsync(ct), token);
        }

        StateChanged?.Invoke(this, state);
    }

    private void OnOutgoingAcknowledged(object? sender, OutgoingAcknowledgedEvent e)
    {
        try
        {
            _pending.TryConsumeByClientRequestId(e.ClientRequestId, out _);
            ChatMessage? pending = null;
            lock (_gate)
            {
                if (!_messages.TryGetValue(e.ConversationId, out var list))
                {
                    return;
                }

                pending = list.FirstOrDefault(m =>
                    string.Equals(m.ClientRequestId, e.ClientRequestId, StringComparison.Ordinal));
                if (pending is null)
                {
                    return;
                }

                pending.Id = e.RealMessageId;
                pending.AccountId = AccountId;
                pending.SendStatus = MessageSendStatus.Sent;
                pending.IsFromAi = e.IsFromAi;
                pending.Source = e.IsFromAi
                    ? MessageSource.LocalUserAI
                    : MessageSource.LocalUserManual;
            }

            OutgoingAcknowledged?.Invoke(this, new OutgoingAcknowledgedEvent
            {
                AccountId = AccountId,
                ClientRequestId = e.ClientRequestId,
                RealMessageId = e.RealMessageId,
                ConversationId = e.ConversationId,
                IsFromAi = e.IsFromAi,
                Message = e.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling outgoing ack");
        }
    }

    private void OnBridgeMessage(object? sender, BridgeMessageEvent e)
    {
        try
        {
            var bridgeMsg = e.Message;
            if (!_deduper.TryAdd(bridgeMsg.ConversationId, bridgeMsg.Id))
            {
                return;
            }

            if (bridgeMsg.IsFromMe)
            {
                var selfMsg = MapMessage(bridgeMsg);
                selfMsg.IsSelf = true;
                selfMsg.Source = MessageSource.LocalUserManual;
                StoreAndRaise(selfMsg);
                return;
            }

            StoreAndRaise(MapMessage(bridgeMsg));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling bridge message");
        }
    }

    private void StoreAndRaise(ChatMessage message)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(message.ContactId, out var list))
            {
                list = [];
                _messages[message.ContactId] = list;
            }

            if (list.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
            {
                return;
            }

            list.Add(message);
            UpdateContactPreview(message.ContactId, message.Content, message.SenderName, message.Timestamp);
        }

        ScheduleMediaLoad(message);
        ScheduleSenderAvatar(message);
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
        {
            AccountId = AccountId,
            ContactId = message.ContactId,
            MessageId = message.Id,
            Message = message,
            Timestamp = message.Timestamp
        });
    }

    private ChatMessage StoreOutgoing(
        string conversationId,
        string content,
        MessageType type,
        bool isFromAi,
        string clientRequestId,
        SendMessageResult result,
        string? localPath = null,
        string? fileName = null,
        string? fileSize = null)
    {
        var avatar = Identity?.AvatarUrl;
        var message = new ChatMessage
        {
            Id = result.MessageId ?? Guid.NewGuid().ToString("N"),
            AccountId = AccountId,
            ContactId = conversationId,
            ClientRequestId = clientRequestId,
            SenderName = "我",
            IsSelf = true,
            IsFromAi = isFromAi,
            Source = isFromAi ? MessageSource.LocalUserAI : MessageSource.LocalUserManual,
            SenderAvatarColor = "#7C5CFF",
            SenderInitials = "我",
            SenderAvatarUrl = avatar,
            Type = type,
            Content = content,
            LocalPath = localPath,
            FileName = fileName,
            FileSize = fileSize,
            ImageUrl = type == MessageType.Image ? localPath : null,
            Timestamp = result.Timestamp == default ? DateTime.Now : result.Timestamp,
            SendStatus = result.Success ? MessageSendStatus.Pending : MessageSendStatus.Failed
        };

        lock (_gate)
        {
            if (!_messages.TryGetValue(conversationId, out var list))
            {
                list = [];
                _messages[conversationId] = list;
            }

            list.Add(message);
            UpdateContactPreview(conversationId, content, message.SenderName, message.Timestamp);
        }

        return message;
    }

    private ChatMessage MapMessage(BridgeMessage m)
    {
        var senderName = m.IsFromMe
            ? "我"
            : (m.SenderDisplayName ?? m.SenderId ?? "对方");
        var type = m.Kind switch
        {
            BridgeMessageKind.Image => MessageType.Image,
            BridgeMessageKind.File => MessageType.File,
            BridgeMessageKind.Emoji => MessageType.Emoji,
            BridgeMessageKind.Video => MessageType.Video,
            BridgeMessageKind.Voice => MessageType.Voice,
            BridgeMessageKind.System => MessageType.System,
            _ => MessageType.Text
        };
        var content = PlaceholderContent(type, m.Content, m.LocalPath ?? m.EmojiUrl);

        string? senderAvatarUrl = null;
        if (!m.IsFromMe && m.IsGroup && !string.IsNullOrWhiteSpace(m.SenderId) &&
            _memberCache.TryGet(AccountId, m.ConversationId, m.SenderId, out var nick, out var avatarUrl, out var avatarPath))
        {
            if (!string.IsNullOrWhiteSpace(nick))
            {
                senderName = nick;
            }

            senderAvatarUrl = avatarUrl;
            _ = avatarPath;
        }
        else if (!m.IsFromMe &&
                 _contacts.TryGetValue(m.ConversationId, out var contact) &&
                 !string.IsNullOrWhiteSpace(contact.AvatarUrl))
        {
            senderAvatarUrl = contact.AvatarUrl;
        }

        return new ChatMessage
        {
            Id = m.Id,
            AccountId = AccountId,
            ContactId = m.ConversationId,
            SenderName = senderName,
            SenderId = m.SenderId,
            SenderAvatarColor = m.IsFromMe ? "#7C5CFF" : "#00B894",
            SenderInitials = Initials(senderName),
            SenderAvatarUrl = senderAvatarUrl,
            IsSelf = m.IsFromMe,
            IsGroup = m.IsGroup,
            Source = m.IsFromMe ? MessageSource.LocalUserManual : MessageSource.RemoteUser,
            Type = type,
            Content = content,
            Timestamp = m.Timestamp,
            MentionsMe = m.MentionsMe,
            QuotesMe = m.QuotesMe,
            ReplyToMessageId = m.ReplyToMessageId,
            LocalPath = m.LocalPath,
            FileName = m.FileName,
            FileSize = m.FileSize,
            ImageUrl = type == MessageType.Image ? FirstNonEmpty(m.LocalPath, m.CdnUrl, m.ThumbUrl) : null,
            EmojiUrl = type == MessageType.Emoji ? FirstNonEmpty(m.EmojiUrl, m.CdnUrl, m.ThumbUrl, m.LocalPath) : null,
            CdnUrl = m.CdnUrl,
            ThumbUrl = m.ThumbUrl,
            Md5 = m.Md5,
            RawXml = m.RawXml,
            FromUserName = m.FromUserName,
            ToUserName = m.ToUserName,
            TotalLen = m.TotalLen,
            CompressType = m.CompressType,
            AttachId = m.AttachId,
            MediaMsgId = m.MediaMsgId ?? m.Id,
            SendStatus = m.IsFromMe ? MessageSendStatus.Sent : MessageSendStatus.None
        };
    }

    private Contact MapContact(BridgeContact c)
    {
        var name = string.IsNullOrWhiteSpace(c.DisplayName) ? c.Id : c.DisplayName;
        return new Contact
        {
            Id = c.Id,
            AccountId = AccountId,
            AccountDisplayName = Identity?.DisplayName ?? Profile.DisplayName,
            Name = name,
            Type = c.IsGroup ? ContactType.Group : ContactType.Friend,
            AvatarColor = c.IsGroup ? "#6C5CE7" : "#00B894",
            AvatarInitials = Initials(name),
            AvatarUrl = c.AvatarHint,
            LastMessage = c.LastMessage ?? string.Empty,
            LastMessageTime = c.LastMessageTime ?? DateTime.MinValue,
            HasLastActivity = c.LastMessageTime.HasValue,
            MemberCount = c.MemberCount,
            IsOnline = true
        };
    }

    private void UpdateContactPreview(string contactId, string content, string sender, DateTime timestamp)
    {
        if (_contacts.TryGetValue(contactId, out var contact))
        {
            contact.LastMessage = content;
            contact.LastSender = sender;
            contact.LastMessageTime = timestamp;
            contact.HasLastActivity = true;
        }
        else
        {
            _contacts[contactId] = new Contact
            {
                Id = contactId,
                AccountId = AccountId,
                AccountDisplayName = Identity?.DisplayName ?? Profile.DisplayName,
                Name = contactId,
                LastMessage = content,
                LastSender = sender,
                LastMessageTime = timestamp,
                HasLastActivity = true,
                AvatarInitials = Initials(contactId),
                AvatarColor = "#00B894"
            };
        }
    }

    private void ScheduleAvatarLoad(Contact contact)
    {
        if (_mediaCache is null || string.IsNullOrWhiteSpace(contact.AvatarUrl))
        {
            return;
        }

        var accountId = AccountId;
        var contactId = contact.Id;
        var url = contact.AvatarUrl;
        var token = _sessionCts?.Token ?? CancellationToken.None;
        SafeFireAndForget(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
            PostUi(() => contact.AvatarLoadState = AvatarLoadState.Loading);
            var path = await _mediaCache.GetOrFetchAvatarAsync(accountId, contactId, url, linked.Token);
            if (!string.Equals(AccountId, accountId, StringComparison.Ordinal))
            {
                return;
            }

            PostUi(() =>
            {
                if (!string.Equals(AccountId, accountId, StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    contact.AvatarLocalPath = path;
                    contact.AvatarLoadState = AvatarLoadState.Loaded;
                }
                else
                {
                    contact.AvatarLoadState = AvatarLoadState.Failed;
                }
            });
        }, token);
    }

    private void ScheduleSenderAvatar(ChatMessage message)
    {
        if (_mediaCache is null || message.IsSelf || string.IsNullOrWhiteSpace(message.SenderAvatarUrl))
        {
            return;
        }

        var accountId = AccountId;
        var senderKey = message.SenderId ?? message.ContactId;
        var url = message.SenderAvatarUrl;
        var token = _sessionCts?.Token ?? CancellationToken.None;
        SafeFireAndForget(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
            var path = await _mediaCache.GetOrFetchAvatarAsync(accountId, senderKey, url, linked.Token);
            if (!string.Equals(AccountId, accountId, StringComparison.Ordinal))
            {
                return;
            }

            PostUi(() =>
            {
                if (!string.Equals(AccountId, accountId, StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    message.SenderAvatarPath = path;
                }
            });
        }, token);
    }

    private void ScheduleMediaLoad(ChatMessage message)
    {
        if (_mediaCache is null)
        {
            return;
        }

        if (message.Type is not (MessageType.Image or MessageType.Emoji))
        {
            return;
        }

        var accountId = AccountId;
        var messageKey = message.Key;
        var token = _sessionCts?.Token ?? CancellationToken.None;
        SafeFireAndForget(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
            try
            {
                PostUi(() => message.MediaLoadState = MediaLoadState.Loading);
                string? path = null;
                if (message.Type == MessageType.Image)
                {
                    var descriptor = new BridgeMediaDescriptor(
                        message.ContactId,
                        message.MediaMsgId ?? message.Id,
                        message.FromUserName,
                        message.ToUserName,
                        message.TotalLen,
                        message.CompressType,
                        message.AttachId,
                        message.LocalPath,
                        message.CdnUrl,
                        message.RawXml);

                    path = await _mediaCache.GetOrFetchImageAsync(
                        messageKey,
                        FirstNonEmpty(message.LocalPath, message.ImageUrl, message.CdnUrl),
                        async (targetPath, downloadCt) =>
                            await _bridge.DownloadImageAsync(descriptor, targetPath, downloadCt),
                        linked.Token);

                    if (!string.IsNullOrWhiteSpace(path) &&
                        string.Equals(AccountId, accountId, StringComparison.Ordinal))
                    {
                        PostUi(() =>
                        {
                            message.LocalPath = path;
                            message.ImageUrl = path;
                        });
                    }
                }
                else
                {
                    var emojiSrc = FirstNonEmpty(message.EmojiUrl, message.CdnUrl, message.ThumbUrl, message.LocalPath);
                    path = await _mediaCache.GetOrFetchEmojiAsync(messageKey, emojiSrc, linked.Token);
                    if (!string.IsNullOrWhiteSpace(path) &&
                        string.Equals(AccountId, accountId, StringComparison.Ordinal))
                    {
                        PostUi(() =>
                        {
                            message.EmojiUrl = path;
                            message.LocalPath = path;
                            message.Content = "【表情消息】";
                        });
                    }
                }

                if (!string.Equals(AccountId, accountId, StringComparison.Ordinal))
                {
                    return;
                }

                PostUi(() =>
                {
                    if (!string.Equals(AccountId, accountId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    message.MediaLoadState = string.IsNullOrWhiteSpace(path)
                        ? MediaLoadState.Failed
                        : MediaLoadState.Loaded;
                    if (string.IsNullOrWhiteSpace(path) && message.Type == MessageType.Emoji)
                    {
                        message.Content = "【表情消息】";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // session stopped / identity rotated
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Media load failed for {MessageId}", message.Id);
                PostUi(() =>
                {
                    message.MediaLoadState = MediaLoadState.Failed;
                    message.MediaError = ex.Message;
                    if (message.Type == MessageType.Emoji)
                    {
                        message.Content = "【表情消息】";
                    }
                });
            }
        }, token);
    }

    private void EnsureSessionCts()
    {
        if (_sessionCts is { IsCancellationRequested: false })
        {
            return;
        }

        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();
    }

    private void CancelSessionCts()
    {
        try
        {
            _sessionCts?.Cancel();
        }
        catch
        {
            // ignore
        }
    }

    private void SafeFireAndForget(Func<CancellationToken, Task> work, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await work(token);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Background session work failed for {ProfileId}", Profile.ProfileId);
            }
        }, CancellationToken.None);
    }

    private static void PostUi(Action action)
    {
        try
        {
            var dispatcher = Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Post(action);
        }
        catch (Exception)
        {
            // Never fall back to background-thread UI mutation.
        }
    }

    internal static string PlaceholderContent(MessageType type, string? raw, string? localPath)
    {
        return type switch
        {
            MessageType.File => "【文件消息】",
            MessageType.Video => "【视频消息】",
            MessageType.Voice => "【语音消息】",
            MessageType.Unknown => "【暂不支持的消息】",
            MessageType.Emoji => "【表情消息】",
            MessageType.Image => string.IsNullOrWhiteSpace(raw) || LooksLikeXml(raw) ? "[图片]" : raw,
            _ => LooksLikeXml(raw) ? string.Empty : (raw ?? string.Empty)
        };
    }

    private static bool LooksLikeXml(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains('<', StringComparison.Ordinal) &&
           (value.Contains("<msg", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("<emoji", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("<appmsg", StringComparison.OrdinalIgnoreCase));

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        return name.Trim()[..1];
    }
}
