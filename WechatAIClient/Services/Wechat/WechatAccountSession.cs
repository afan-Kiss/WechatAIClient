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
    private readonly object _gate = new();
    private readonly Dictionary<string, List<ChatMessage>> _messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Contact> _contacts = new(StringComparer.Ordinal);
    private readonly bool _ownsBridge;
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
        await _bridge.StartAsync(cancellationToken);
        _started = true;
        await RefreshIdentityAsync(cancellationToken);
        await RefreshContactsCacheAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _bridge.StopAsync(cancellationToken);
        _pending.Clear();
        _started = false;
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _bridge.ReconnectAsync(cancellationToken);
        await RefreshIdentityAsync(cancellationToken);
        await RefreshContactsCacheAsync(cancellationToken);
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
        _bridge.StateChanged -= OnBridgeStateChanged;
        _bridge.MessageReceived -= OnBridgeMessage;
        _bridge.OutgoingAcknowledged -= OnOutgoingAcknowledged;
        _pending.Clear();
        if (_ownsBridge)
        {
            await _bridge.DisposeAsync();
        }
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
        await _bridge.StartAsync(cancellationToken);
        if (_started)
        {
            return;
        }

        _started = true;
        await RefreshIdentityAsync(cancellationToken);
        await RefreshContactsCacheAsync(cancellationToken);
    }

    private async Task RefreshIdentityAsync(CancellationToken cancellationToken)
    {
        var account = await _bridge.GetAccountAsync(cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(account.UserId))
        {
            return;
        }

        ApplyIdentity(account.UserId, account.DisplayName, account.AvatarPath);
    }

    private void ApplyIdentity(string wxid, string displayName, string? avatar)
    {
        string? oldId;
        lock (_gate)
        {
            oldId = _identity?.AccountId;
            if (string.Equals(oldId, wxid, StringComparison.Ordinal) &&
                _identity is not null &&
                string.Equals(_identity.DisplayName, displayName, StringComparison.Ordinal))
            {
                return;
            }

            _identity = new WechatAccountIdentity(wxid, wxid, displayName, avatar);
            AccountId = wxid;
        }

        if (_bridge is LocalApiWechatBridgeClient)
        {
            // AccountId already mirrored from bridge login wxid.
        }

        IdentityChanged?.Invoke(this, new AccountIdentityChangedEventArgs
        {
            ProfileId = Profile.ProfileId,
            OldAccountId = oldId,
            NewAccountId = wxid
        });
    }

    private async Task RefreshContactsCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recent = await _bridge.GetRecentAsync(cancellationToken);
            var friends = await _bridge.GetContactsAsync(cancellationToken);
            var groups = await _bridge.GetGroupsAsync(cancellationToken);
            StoreContacts(recent.Concat(friends).Concat(groups).Select(MapContact));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Contacts refresh failed for profile {ProfileId}", Profile.ProfileId);
        }
    }

    private IReadOnlyList<Contact> StoreContacts(IEnumerable<Contact> contacts)
    {
        lock (_gate)
        {
            foreach (var c in contacts)
            {
                c.AccountId = AccountId;
                c.AccountDisplayName = Identity?.DisplayName ?? Profile.DisplayName;
                _contacts[c.Id] = c;
            }

            return _contacts.Values.ToList();
        }
    }

    private void OnBridgeStateChanged(object? sender, WechatConnectionState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        if (state is WechatConnectionState.Connected or WechatConnectionState.Degraded)
        {
            _ = RefreshIdentityAsync(CancellationToken.None);
        }

        StateChanged?.Invoke(this, state);
    }

    private void OnOutgoingAcknowledged(object? sender, OutgoingAcknowledgedEvent e)
    {
        try
        {
            _pending.TryConsumeByClientRequestId(e.ClientRequestId, out _);
            lock (_gate)
            {
                if (!_messages.TryGetValue(e.ConversationId, out var list))
                {
                    return;
                }

                var pending = list.FirstOrDefault(m =>
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
        var message = new ChatMessage
        {
            Id = result.MessageId ?? Guid.NewGuid().ToString("N"),
            AccountId = AccountId,
            ContactId = conversationId,
            ClientRequestId = clientRequestId,
            SenderName = isFromAi ? "AI 助手" : "我",
            IsSelf = true,
            IsFromAi = isFromAi,
            Source = isFromAi ? MessageSource.LocalUserAI : MessageSource.LocalUserManual,
            SenderAvatarColor = "#7C5CFF",
            SenderInitials = isFromAi ? "AI" : "我",
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
        var content = PlaceholderContent(type, m.Content, m.LocalPath);

        return new ChatMessage
        {
            Id = m.Id,
            AccountId = AccountId,
            ContactId = m.ConversationId,
            SenderName = senderName,
            SenderId = m.SenderId,
            SenderAvatarColor = m.IsFromMe ? "#7C5CFF" : "#00B894",
            SenderInitials = Initials(senderName),
            IsSelf = m.IsFromMe,
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
            ImageUrl = type == MessageType.Image ? m.LocalPath : null,
            EmojiUrl = type == MessageType.Emoji ? m.LocalPath : null,
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

        _ = Task.Run(async () =>
        {
            try
            {
                message.MediaLoadState = MediaLoadState.Loading;
                string? path = null;
                if (message.Type == MessageType.Image)
                {
                    path = await _mediaCache.GetOrFetchImageAsync(
                        AccountId,
                        message.Id,
                        message.LocalPath ?? message.ImageUrl,
                        downloadFactory: null);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        message.LocalPath = path;
                        message.ImageUrl = path;
                    }
                }
                else
                {
                    path = await _mediaCache.GetOrFetchEmojiAsync(
                        AccountId,
                        message.Id,
                        message.EmojiUrl ?? message.LocalPath);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        message.EmojiUrl = path;
                        message.LocalPath = path;
                    }
                }

                message.MediaLoadState = string.IsNullOrWhiteSpace(path)
                    ? MediaLoadState.Failed
                    : MediaLoadState.Loaded;
                if (string.IsNullOrWhiteSpace(path) && message.Type == MessageType.Emoji)
                {
                    message.Content = "【表情消息】";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Media load failed for {MessageId}", message.Id);
                message.MediaLoadState = MediaLoadState.Failed;
                message.MediaError = ex.Message;
            }
        });
    }

    internal static string PlaceholderContent(MessageType type, string? raw, string? localPath)
    {
        return type switch
        {
            MessageType.File => "【文件消息】",
            MessageType.Video => "【视频消息】",
            MessageType.Voice => "【语音消息】",
            MessageType.Unknown => "【暂不支持的消息】",
            MessageType.Emoji when string.IsNullOrWhiteSpace(localPath) && string.IsNullOrWhiteSpace(raw)
                => "【表情消息】",
            MessageType.Emoji when string.IsNullOrWhiteSpace(localPath)
                => string.IsNullOrWhiteSpace(raw) ? "【表情消息】" : raw,
            MessageType.Image => string.IsNullOrWhiteSpace(raw) ? "[图片]" : raw,
            _ => raw ?? string.Empty
        };
    }

    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        return name.Trim()[..1];
    }
}
