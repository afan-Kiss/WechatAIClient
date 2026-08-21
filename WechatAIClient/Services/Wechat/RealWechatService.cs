using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.Wechat;

public sealed class RealWechatService : IWechatService, IAsyncDisposable
{
    private readonly IWechatBridgeClient _bridge;
    private readonly MessageDeduplicator _deduper = new();
    private readonly PendingOutgoingTracker _pending = new();
    private readonly ILogger<RealWechatService> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, List<ChatMessage>> _messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Contact> _contacts = new(StringComparer.Ordinal);
    private bool _started;
    private bool _disposed;

    public RealWechatService(IWechatBridgeClient bridge, ILogger<RealWechatService> logger)
    {
        _bridge = bridge;
        _logger = logger;
        _bridge.StateChanged += OnBridgeStateChanged;
        _bridge.MessageReceived += OnBridgeMessage;
        _bridge.OutgoingAcknowledged += OnOutgoingAcknowledged;
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<WechatConnectionState>? ConnectionStateChanged;
    public event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
#pragma warning disable CS0067
    public event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;
#pragma warning restore CS0067

    public WechatConnectionState ConnectionState => _bridge.State;

    public string? SelectedAccountId { get; private set; }

    public IReadOnlyList<WechatAccountIdentity> GetAccounts()
    {
        var account = _bridge.GetAccountAsync().GetAwaiter().GetResult();
        if (account is null)
        {
            return Array.Empty<WechatAccountIdentity>();
        }

        return [new WechatAccountIdentity(account.UserId, account.UserId, account.DisplayName, account.AvatarPath)];
    }

    public WechatConnectionState GetAccountConnectionState(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return WechatConnectionState.Disconnected;
        }

        var accounts = GetAccounts();
        if (accounts.Count == 0)
        {
            return ConnectionState;
        }

        return accounts.Any(a => string.Equals(a.AccountId, accountId, StringComparison.Ordinal))
            ? ConnectionState
            : WechatConnectionState.Disconnected;
    }

    public bool CanSend(ConversationKey key) => CanManualSend(key);

    public bool CanManualSend(ConversationKey key)
    {
        var state = GetAccountConnectionState(key.AccountId);
        return state is WechatConnectionState.Connected or WechatConnectionState.Degraded;
    }

    public bool CanAutoReply(ConversationKey key)
        => GetAccountConnectionState(key.AccountId) == WechatConnectionState.Connected;

    public Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedAccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId;
        return Task.CompletedTask;
    }

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Always call StartAsync so Stop/Start of the bridge can recover.
        await _bridge.StartAsync(cancellationToken);
        if (_started)
        {
            return;
        }

        _started = true;
        await RefreshContactsCacheAsync(cancellationToken);
    }

    public Task<WechatConnectionState> GetConnectionStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_bridge.State);
    }

    public async Task<WechatAccountInfo?> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        return await _bridge.GetAccountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var list = await _bridge.GetContactsAsync(cancellationToken);
        var mapped = list.Select(MapContact).ToList();
        lock (_gate)
        {
            foreach (var c in mapped)
            {
                _contacts[c.Id] = c;
            }
        }

        return mapped;
    }

    public async Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var list = await _bridge.GetGroupsAsync(cancellationToken);
        var mapped = list.Select(MapContact).ToList();
        lock (_gate)
        {
            foreach (var c in mapped)
            {
                _contacts[c.Id] = c;
            }
        }

        return mapped;
    }

    public async Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var list = await _bridge.GetRecentAsync(cancellationToken);
        var mapped = list.Select(MapContact).ToList();
        lock (_gate)
        {
            foreach (var c in mapped)
            {
                _contacts[c.Id] = c;
            }
        }

        return mapped;
    }

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        ConversationKey key,
        CancellationToken cancellationToken = default)
        => GetMessagesAsync(key.ConversationId, cancellationToken);

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string contactId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var bridgeMessages = await _bridge.GetMessagesAsync(contactId, 200, cancellationToken);
        var mapped = bridgeMessages.Select(m => MapMessage(m, raiseSourceFromPending: false)).ToList();
        lock (_gate)
        {
            _messages[contactId] = mapped.ToList();
        }

        return mapped;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword,
        ContactType? tabFilter,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);

        var contacts = new List<Contact>();
        if (tabFilter is null or ContactType.Friend)
        {
            contacts.AddRange(await GetContactsAsync(cancellationToken));
        }

        if (tabFilter is null or ContactType.Group)
        {
            contacts.AddRange(await GetGroupsAsync(cancellationToken));
        }

        // Dedupe by id (Friend+Group union when tabFilter is null).
        var byId = new Dictionary<string, Contact>(StringComparer.Ordinal);
        foreach (var c in contacts)
        {
            byId[c.Id] = c;
        }

        contacts = byId.Values.ToList();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return contacts
                .OrderByDescending(c => c.HasLastActivity)
                .ThenByDescending(c => c.LastMessageTime)
                .Select(c => new SearchHit
                {
                    Contact = c,
                    MatchSummary = c.LastMessage,
                    HitKind = c.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
                })
                .ToList();
        }

        var hits = new List<SearchHit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contact in contacts)
        {
            if (contact.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(new SearchHit
                {
                    Contact = contact,
                    MatchSummary = contact.Name,
                    HitKind = contact.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
                });
                seen.Add(contact.Id);
            }
        }

        lock (_gate)
        {
            foreach (var contact in contacts)
            {
                if (!_messages.TryGetValue(contact.Id, out var messages))
                {
                    continue;
                }

                var match = messages.LastOrDefault(m =>
                    m.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    continue;
                }

                if (!seen.Add(contact.Id))
                {
                    continue;
                }

                hits.Add(new SearchHit
                {
                    Contact = contact,
                    MatchSummary = match.Content,
                    HitKind = SearchHitKind.Message
                });
            }
        }

        foreach (var contact in contacts)
        {
            if (seen.Contains(contact.Id))
            {
                continue;
            }

            if (contact.LastMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(new SearchHit
                {
                    Contact = contact,
                    MatchSummary = contact.LastMessage,
                    HitKind = SearchHitKind.Message
                });
            }
        }

        return hits;
    }

    public Task<ChatMessage> SendMessageAsync(
        ConversationKey key,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
        => SendMessageAsync(key.ConversationId, content, type, fileName, fileSize, imagePath, isFromAi, cancellationToken);

    public async Task<ChatMessage> SendMessageAsync(
        string contactId,
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
            _pending.Register(clientId, contactId, "[图片]", isFromAi);
            var result = await _bridge.SendImageAsync(contactId, imagePath, clientId, isFromAi, cancellationToken);
            return BuildOutgoingMessage(contactId, "[图片]", MessageType.Image, isFromAi, clientId, result, imagePath, fileName);
        }

        if (type == MessageType.File && !string.IsNullOrWhiteSpace(imagePath ?? fileName))
        {
            var path = imagePath ?? fileName ?? string.Empty;
            var clientId = Guid.NewGuid().ToString("N");
            _pending.Register(clientId, contactId, "[文件]", isFromAi);
            var result = await _bridge.SendFileAsync(contactId, path, clientId, isFromAi, cancellationToken);
            return BuildOutgoingMessage(contactId, "[文件]", MessageType.File, isFromAi, clientId, result, path, fileName, fileSize);
        }

        var send = await SendTextMessageAsync(contactId, content, isFromAi, null, cancellationToken);
        lock (_gate)
        {
            if (_messages.TryGetValue(contactId, out var list))
            {
                var existing = list.FirstOrDefault(m =>
                    string.Equals(m.ClientRequestId, send.ClientRequestId, StringComparison.Ordinal));
                if (existing is not null)
                {
                    return existing;
                }
            }
        }

        return BuildOutgoingMessage(
            contactId,
            content,
            MessageType.Text,
            isFromAi,
            send.ClientRequestId,
            send);
    }

    public Task<SendMessageResult> SendTextMessageAsync(
        ConversationKey key,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
        => SendTextMessageAsync(key.ConversationId, content, isFromAi, clientRequestId, cancellationToken);

    public async Task<SendMessageResult> SendTextMessageAsync(
        string contactId,
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
        // Local pending only for UI-row lookup on Ack; bridge owns echo match.
        _pending.Register(clientId, contactId, text, isFromAi);
        var result = await _bridge.SendTextAsync(contactId, text, clientId, isFromAi, cancellationToken);
        var message = BuildOutgoingMessage(contactId, text, MessageType.Text, isFromAi, clientId, result);
        lock (_gate)
        {
            if (!_messages.TryGetValue(contactId, out var list))
            {
                list = [];
                _messages[contactId] = list;
            }

            list.Add(message);
            UpdateContactPreview(contactId, text, message.SenderName, message.Timestamp);
        }

        if (!result.Success)
        {
            _pending.TryConsumeByClientRequestId(clientId, out _);
        }

        return result with { MessageId = message.Id };
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await _bridge.ReconnectAsync(cancellationToken);
        await RefreshContactsCacheAsync(cancellationToken);
    }

    public Task ReconnectAsync(string accountId, CancellationToken cancellationToken = default)
    {
        _ = accountId;
        return ReconnectAsync(cancellationToken);
    }

    public Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(key.ConversationId, content, false, false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(contactId, content, false, false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(key.ConversationId, content, mentionsMe, quotesMe, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        // Real path: no-op (simulation stays on Mock / Fake bridge in tests)
        cancellationToken.ThrowIfCancellationRequested();
        _ = (contactId, content, mentionsMe, quotesMe);
        return Task.CompletedTask;
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
        await _bridge.DisposeAsync();
    }

    private async Task RefreshContactsCacheAsync(CancellationToken cancellationToken)
    {
        var stateBefore = _bridge.State;
        try
        {
            var recent = await _bridge.GetRecentAsync(cancellationToken);
            var friends = await _bridge.GetContactsAsync(cancellationToken);
            var groups = await _bridge.GetGroupsAsync(cancellationToken);
            lock (_gate)
            {
                foreach (var c in recent.Concat(friends).Concat(groups).Select(MapContact))
                {
                    _contacts[c.Id] = c;
                }
            }
        }
        catch (Exception ex)
        {
            // Leave connection state to the bridge — never paint green on refresh failure.
            if (stateBefore == WechatConnectionState.Connected ||
                _bridge.State == WechatConnectionState.Connected)
            {
                _logger.LogWarning(ex, "Failed to refresh contacts cache while connected");
            }
            else if (_bridge.State == WechatConnectionState.WechatNotRunning)
            {
                _logger.LogDebug(ex, "Contacts refresh skipped; Hook API offline");
            }
            else
            {
                _logger.LogWarning(ex, "Failed to refresh contacts cache (state={State})", _bridge.State);
            }
        }
    }

    private void OnBridgeStateChanged(object? sender, WechatConnectionState state)
    {
        ConnectionStateChanged?.Invoke(this, state);
        var accountId = SelectedAccountId
                        ?? _bridge.GetAccountAsync().GetAwaiter().GetResult()?.UserId
                        ?? "legacy";
        AccountConnectionStateChanged?.Invoke(this, new AccountConnectionStateChangedEventArgs
        {
            AccountId = accountId,
            State = state
        });
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
                pending.SendStatus = MessageSendStatus.Sent;
                pending.IsFromAi = e.IsFromAi;
                pending.Source = e.IsFromAi
                    ? MessageSource.LocalUserAI
                    : MessageSource.LocalUserManual;
                // Do NOT raise MessageReceived — Ack is reconcile-only (no Auto).
            }
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
                // Matched echoes are delivered via OutgoingAcknowledged.
                // Unmatched self (phone / other client) — show as self, skip auto.
                var selfMsg = MapMessage(bridgeMsg, raiseSourceFromPending: false);
                selfMsg.IsSelf = true;
                selfMsg.Source = MessageSource.LocalUserManual;
                StoreAndRaise(selfMsg, raiseForAuto: false);
                return;
            }

            var chat = MapMessage(bridgeMsg, raiseSourceFromPending: false);
            StoreAndRaise(chat, raiseForAuto: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling bridge message");
        }
    }

    private void StoreAndRaise(ChatMessage message, bool raiseForAuto)
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

        // Always raise MessageReceived for UI; MainWindowViewModel skips self/AI for auto.
        _ = raiseForAuto;
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
        {
            AccountId = message.AccountId,
            ContactId = message.ContactId,
            MessageId = message.Id,
            Message = message,
            Timestamp = message.Timestamp
        });
    }

    private string ResolveAccountId()
        => SelectedAccountId
           ?? _bridge.GetAccountAsync().GetAwaiter().GetResult()?.UserId
           ?? "legacy";

    private ChatMessage MapMessage(BridgeMessage m, bool raiseSourceFromPending)
    {
        _ = raiseSourceFromPending;
        var senderName = m.IsFromMe
            ? "我"
            : (m.SenderDisplayName ?? m.SenderId ?? "对方");
        var content = m.Content ?? string.Empty;
        // Keep raw Content + SenderName; AIContextBuilder prefixes for groups.
        return new ChatMessage
        {
            Id = m.Id,
            AccountId = ResolveAccountId(),
            ContactId = m.ConversationId,
            SenderName = senderName,
            SenderId = m.SenderId,
            SenderAvatarColor = m.IsFromMe ? "#7C5CFF" : "#00B894",
            SenderInitials = Initials(senderName),
            IsSelf = m.IsFromMe,
            Source = m.IsFromMe ? MessageSource.LocalUserManual : MessageSource.RemoteUser,
            Type = m.Kind switch
            {
                BridgeMessageKind.Image => MessageType.Image,
                BridgeMessageKind.File => MessageType.File,
                BridgeMessageKind.Emoji => MessageType.Emoji,
                BridgeMessageKind.Video => MessageType.Video,
                BridgeMessageKind.Voice => MessageType.Voice,
                BridgeMessageKind.System => MessageType.System,
                _ => MessageType.Text
            },
            Content = content,
            Timestamp = m.Timestamp,
            MentionsMe = m.MentionsMe,
            QuotesMe = m.QuotesMe,
            ReplyToMessageId = m.ReplyToMessageId,
            LocalPath = m.LocalPath,
            FileName = m.FileName,
            FileSize = m.FileSize,
            ImageUrl = m.Kind == BridgeMessageKind.Image ? m.LocalPath : null,
            SendStatus = m.IsFromMe ? MessageSendStatus.Sent : MessageSendStatus.None
        };
    }

    private ChatMessage BuildOutgoingMessage(
        string contactId,
        string content,
        MessageType type,
        bool isFromAi,
        string clientRequestId,
        SendMessageResult result,
        string? localPath = null,
        string? fileName = null,
        string? fileSize = null)
    {
        return new ChatMessage
        {
            Id = result.MessageId ?? Guid.NewGuid().ToString("N"),
            AccountId = ResolveAccountId(),
            ContactId = contactId,
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
            // Optimistic Pending until bridge OutgoingAcknowledged.
            SendStatus = result.Success ? MessageSendStatus.Pending : MessageSendStatus.Failed
        };
    }

    private static Contact MapContact(BridgeContact c)
    {
        var name = string.IsNullOrWhiteSpace(c.DisplayName) ? c.Id : c.DisplayName;
        return new Contact
        {
            Id = c.Id,
            Name = name,
            Type = c.IsGroup ? ContactType.Group : ContactType.Friend,
            AvatarColor = c.IsGroup ? "#6C5CE7" : "#00B894",
            AvatarInitials = Initials(name),
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
