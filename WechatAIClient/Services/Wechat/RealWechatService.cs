using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services.Weixin;

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
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<WechatConnectionState>? ConnectionStateChanged;

    public WechatConnectionState ConnectionState => _bridge.State;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        await _bridge.StartAsync(cancellationToken);
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
        var recent = await GetRecentChatsAsync(cancellationToken);
        IEnumerable<Contact> contacts = recent;
        if (tabFilter is ContactType filter)
        {
            contacts = contacts.Where(c => c.Type == filter);
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return contacts
                .Select(c => new SearchHit
                {
                    Contact = c,
                    MatchSummary = c.LastMessage,
                    HitKind = c.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
                })
                .ToList();
        }

        return contacts
            .Where(c => c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        c.LastMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Select(c => new SearchHit
            {
                Contact = c,
                MatchSummary = c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    ? c.Name
                    : c.LastMessage,
                HitKind = c.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
            })
            .ToList();
    }

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
            var result = await _bridge.SendImageAsync(contactId, imagePath, clientId, cancellationToken);
            return BuildOutgoingMessage(contactId, "[图片]", MessageType.Image, isFromAi, clientId, result, imagePath, fileName);
        }

        if (type == MessageType.File && !string.IsNullOrWhiteSpace(imagePath ?? fileName))
        {
            var path = imagePath ?? fileName ?? string.Empty;
            var clientId = Guid.NewGuid().ToString("N");
            _pending.Register(clientId, contactId, "[文件]", isFromAi);
            var result = await _bridge.SendFileAsync(contactId, path, clientId, cancellationToken);
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
        _pending.Register(clientId, contactId, text, isFromAi);
        if (isFromAi && _bridge is LocalApiWechatBridgeClient localBridge)
        {
            localBridge.RegisterAiOutgoing(contactId, text, clientId);
        }

        var result = await _bridge.SendTextAsync(contactId, text, clientId, cancellationToken);
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

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(contactId, content, false, false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        // Real path: no-op (simulation stays on Mock / Fake bridge in tests)
        cancellationToken.ThrowIfCancellationRequested();
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
        await _bridge.DisposeAsync();
    }

    private async Task RefreshContactsCacheAsync(CancellationToken cancellationToken)
    {
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
            _logger.LogWarning(ex, "Failed to refresh contacts cache");
        }
    }

    private void OnBridgeStateChanged(object? sender, WechatConnectionState state)
        => ConnectionStateChanged?.Invoke(this, state);

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
                if (_pending.TryMatchEcho(
                        bridgeMsg.ConversationId,
                        bridgeMsg.Content,
                        out var matchSource,
                        out var clientRequestId))
                {
                    // Echo of our own send — update local row if needed, do not raise as remote.
                    lock (_gate)
                    {
                        if (_messages.TryGetValue(bridgeMsg.ConversationId, out var list))
                        {
                            var pending = list.FirstOrDefault(m =>
                                string.Equals(m.ClientRequestId, clientRequestId, StringComparison.Ordinal));
                            if (pending is not null)
                            {
                                pending.Id = bridgeMsg.Id;
                                pending.SendStatus = MessageSendStatus.Sent;
                                pending.Source = matchSource == OutgoingMatchSource.AiGenerated
                                    ? MessageSource.LocalUserAI
                                    : MessageSource.LocalUserManual;
                                pending.IsFromAi = matchSource == OutgoingMatchSource.AiGenerated;
                                return;
                            }
                        }
                    }

                    // Matched but no local row — still skip auto pipeline
                    return;
                }

                // Unmatched self message (manual send from phone) — show as self, skip auto
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
        // raiseForAuto kept for clarity / future filtering.
        _ = raiseForAuto;
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
        {
            ContactId = message.ContactId,
            MessageId = message.Id,
            Message = message,
            Timestamp = message.Timestamp
        });
    }

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
            SendStatus = result.Success ? MessageSendStatus.Sent : MessageSendStatus.Failed
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
            LastMessageTime = c.LastMessageTime ?? DateTime.Now,
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
