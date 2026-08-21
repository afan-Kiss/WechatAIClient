using WechatAIClient.Models;

namespace WechatAIClient.Services.Mock;

public sealed class MockWechatService : IWechatService
{
    public const string AccountAId = "mock-a";
    public const string AccountBId = "mock-b";

    private readonly object _gate = new();
    private readonly List<WechatAccountIdentity> _accounts;
    private readonly Dictionary<string, WechatConnectionState> _accountStates;
    private readonly List<Contact> _contacts;
    private readonly Dictionary<string, List<ChatMessage>> _messages;
    private string? _selectedAccountId;

    public MockWechatService()
    {
        _accounts =
        [
            new WechatAccountIdentity(AccountAId, AccountAId, "Mock 账号 A"),
            new WechatAccountIdentity(AccountBId, AccountBId, "Mock 账号 B")
        ];
        _accountStates = new Dictionary<string, WechatConnectionState>(StringComparer.Ordinal)
        {
            [AccountAId] = WechatConnectionState.Connected,
            [AccountBId] = WechatConnectionState.Connected
        };

        _contacts = BuildSeedContacts();
        _messages = BuildSeedMessages();
    }

    /// <summary>Optional per-contact delay override used by tests.</summary>
    public Func<string, int>? MessageLoadDelayMs { get; set; }

    public int DelayGetMessagesMs { get; set; }
    public int DelaySearchMs { get; set; }
    public bool ThrowOnSend { get; set; }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<WechatConnectionState>? ConnectionStateChanged;
    public event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
#pragma warning disable CS0067
    public event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;
#pragma warning restore CS0067

    public WechatConnectionState ConnectionState
    {
        get
        {
            lock (_gate)
            {
                return _accountStates.Values.Any(s => s == WechatConnectionState.Connected)
                    ? WechatConnectionState.Connected
                    : _accountStates.Values.FirstOrDefault();
            }
        }
    }

    public string? SelectedAccountId
    {
        get
        {
            lock (_gate)
            {
                return _selectedAccountId;
            }
        }
    }

    public IReadOnlyList<WechatAccountIdentity> GetAccounts() => _accounts;

    public Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _selectedAccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId;
        }

        return Task.CompletedTask;
    }

    public Task<WechatConnectionState> GetConnectionStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ConnectionState);
    }

    public Task<WechatAccountInfo?> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selected = SelectedAccountId ?? AccountAId;
        var identity = _accounts.FirstOrDefault(a => a.AccountId == selected) ?? _accounts[0];
        return Task.FromResult<WechatAccountInfo?>(
            new WechatAccountInfo(identity.AccountId, identity.DisplayName, identity.AvatarUrl));
    }

    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            foreach (var id in _accounts.Select(a => a.AccountId))
            {
                _accountStates[id] = WechatConnectionState.Connected;
                AccountConnectionStateChanged?.Invoke(this, new AccountConnectionStateChangedEventArgs
                {
                    AccountId = id,
                    State = WechatConnectionState.Connected
                });
            }
        }

        ConnectionStateChanged?.Invoke(this, WechatConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task ReconnectAsync(string accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _accountStates[accountId] = WechatConnectionState.Connected;
        }

        AccountConnectionStateChanged?.Invoke(this, new AccountConnectionStateChangedEventArgs
        {
            AccountId = accountId,
            State = WechatConnectionState.Connected
        });
        ConnectionStateChanged?.Invoke(this, ConnectionState);
        return Task.CompletedTask;
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        ConversationKey key,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var clientId = string.IsNullOrWhiteSpace(clientRequestId)
            ? Guid.NewGuid().ToString("N")
            : clientRequestId;
        try
        {
            var msg = await SendMessageAsync(
                key, content, MessageType.Text, isFromAi: isFromAi, cancellationToken: cancellationToken);
            msg.ClientRequestId = clientId;
            msg.SendStatus = MessageSendStatus.Sent;
            return new SendMessageResult(true, msg.Id, clientId, msg.Timestamp, null, null);
        }
        catch (Exception ex)
        {
            return new SendMessageResult(false, null, clientId, DateTime.Now, "SendFailed", ex.Message);
        }
    }

    public Task<SendMessageResult> SendTextMessageAsync(
        string contactId,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var accountId = ResolveAccountIdForContact(contactId);
        return SendTextMessageAsync(
            new ConversationKey(accountId, contactId), content, isFromAi, clientRequestId, cancellationToken);
    }

    public Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Contact>>(
                FilterBySelected(_contacts.Where(c => c.Type == ContactType.Friend)).ToList());
        }
    }

    public Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Contact>>(
                FilterBySelected(_contacts.Where(c => c.Type == ContactType.Group)).ToList());
        }
    }

    public Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Contact>>(
                FilterBySelected(_contacts).OrderByDescending(c => c.LastMessageTime).ToList());
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        ConversationKey key,
        CancellationToken cancellationToken = default)
    {
        var delay = MessageLoadDelayMs?.Invoke(key.ConversationId) ?? DelayGetMessagesMs;
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var storageKey = key.StableKey;

        lock (_gate)
        {
            if (_messages.TryGetValue(storageKey, out var list))
            {
                return list.ToList();
            }

            var contact = _contacts.FirstOrDefault(c =>
                c.AccountId == key.AccountId && c.Id == key.ConversationId);
            var fallback = new List<ChatMessage>
            {
                new()
                {
                    AccountId = key.AccountId,
                    ContactId = key.ConversationId,
                    SenderName = contact?.Name ?? "对方",
                    SenderAvatarColor = contact?.AvatarColor ?? "#7C5CFF",
                    SenderInitials = contact?.AvatarInitials ?? "?",
                    Source = MessageSource.RemoteUser,
                    Content = contact?.LastMessage ?? "你好",
                    Timestamp = contact?.LastMessageTime ?? DateTime.Now,
                    ShowTimeSeparator = true,
                    TimeSeparatorText = "今天"
                }
            };
            _messages[storageKey] = fallback;
            return fallback.ToList();
        }
    }

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string contactId,
        CancellationToken cancellationToken = default)
    {
        var accountId = ResolveAccountIdForContact(contactId);
        return GetMessagesAsync(new ConversationKey(accountId, contactId), cancellationToken);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword,
        ContactType? tabFilter,
        CancellationToken cancellationToken = default)
    {
        if (DelaySearchMs > 0)
        {
            await Task.Delay(DelaySearchMs, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IEnumerable<Contact> contacts = FilterBySelected(_contacts);
            if (tabFilter is ContactType filter)
            {
                contacts = contacts.Where(c => c.Type == filter);
            }

            var contactList = contacts.ToList();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return contactList
                    .OrderByDescending(c => c.LastMessageTime)
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

            foreach (var contact in contactList)
            {
                if (contact.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(new SearchHit
                    {
                        Contact = contact,
                        MatchSummary = contact.Name,
                        HitKind = contact.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
                    });
                    seen.Add(contact.Key.StableKey);
                }
            }

            foreach (var contact in contactList)
            {
                if (!_messages.TryGetValue(contact.Key.StableKey, out var messages))
                {
                    continue;
                }

                var match = messages.LastOrDefault(m =>
                    m.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                if (match is null || !seen.Add(contact.Key.StableKey))
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

            foreach (var contact in contactList)
            {
                if (seen.Contains(contact.Key.StableKey))
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
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Mock send failure");
        }

        ChatMessage message;
        lock (_gate)
        {
            message = new ChatMessage
            {
                AccountId = key.AccountId,
                ContactId = key.ConversationId,
                SenderName = isFromAi ? "AI 助手" : "我",
                IsSelf = true,
                IsFromAi = isFromAi,
                Source = isFromAi ? MessageSource.LocalUserAI : MessageSource.LocalUserManual,
                SenderAvatarColor = "#7C5CFF",
                SenderInitials = isFromAi ? "AI" : "我",
                Type = type,
                Content = content,
                FileName = fileName,
                FileSize = fileSize,
                ImageUrl = imagePath,
                Timestamp = DateTime.Now
            };

            var storageKey = key.StableKey;
            if (!_messages.TryGetValue(storageKey, out var list))
            {
                list = [];
                _messages[storageKey] = list;
            }

            list.Add(message);
            UpdateContactPreview(key.AccountId, key.ConversationId, content, message.SenderName, message.Timestamp);
        }

        return Task.FromResult(message);
    }

    public Task<ChatMessage> SendMessageAsync(
        string contactId,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        var accountId = ResolveAccountIdForContact(contactId);
        return SendMessageAsync(
            new ConversationKey(accountId, contactId),
            content,
            type,
            fileName,
            fileSize,
            imagePath,
            isFromAi,
            cancellationToken);
    }

    public Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(key, content, mentionsMe: false, quotesMe: false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(contactId, content, mentionsMe: false, quotesMe: false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ChatMessage message;
        lock (_gate)
        {
            var contact = _contacts.FirstOrDefault(c =>
                c.AccountId == key.AccountId && c.Id == key.ConversationId);
            message = new ChatMessage
            {
                AccountId = key.AccountId,
                ContactId = key.ConversationId,
                SenderName = contact?.Name ?? "对方",
                SenderAvatarColor = contact?.AvatarColor ?? "#7C5CFF",
                SenderInitials = contact?.AvatarInitials ?? "?",
                Content = content,
                Timestamp = DateTime.Now,
                IsSelf = false,
                IsFromAi = false,
                Source = MessageSource.RemoteUser,
                MentionsMe = mentionsMe,
                QuotesMe = quotesMe
            };

            var storageKey = key.StableKey;
            if (!_messages.TryGetValue(storageKey, out var list))
            {
                list = [];
                _messages[storageKey] = list;
            }

            list.Add(message);
            UpdateContactPreview(key.AccountId, key.ConversationId, content, message.SenderName, message.Timestamp);
        }

        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
        {
            AccountId = key.AccountId,
            ContactId = key.ConversationId,
            MessageId = message.Id,
            Message = message,
            Timestamp = message.Timestamp
        });

        return Task.CompletedTask;
    }

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        var accountId = ResolveAccountIdForContact(contactId);
        return SimulateIncomingMessageAsync(
            new ConversationKey(accountId, contactId), content, mentionsMe, quotesMe, cancellationToken);
    }

    private IEnumerable<Contact> FilterBySelected(IEnumerable<Contact> contacts)
    {
        var selected = _selectedAccountId;
        if (string.IsNullOrWhiteSpace(selected))
        {
            return contacts;
        }

        return contacts.Where(c => string.Equals(c.AccountId, selected, StringComparison.Ordinal));
    }

    private string ResolveAccountIdForContact(string contactId)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(_selectedAccountId))
            {
                return _selectedAccountId;
            }

            var match = _contacts.FirstOrDefault(c => c.Id == contactId);
            return match?.AccountId ?? AccountAId;
        }
    }

    private void UpdateContactPreview(
        string accountId,
        string contactId,
        string content,
        string sender,
        DateTime timestamp)
    {
        var contact = _contacts.FirstOrDefault(c =>
            c.AccountId == accountId && c.Id == contactId);
        if (contact is null)
        {
            return;
        }

        contact.LastMessage = content;
        contact.LastSender = sender;
        contact.LastMessageTime = timestamp;
        contact.HasLastActivity = true;
    }

    private static List<Contact> BuildSeedContacts()
    {
        var list = new List<Contact>();
        list.AddRange(BuildAccountContacts(AccountAId, "A"));
        list.AddRange(BuildAccountContacts(AccountBId, "B"));
        return list;
    }

    private static IEnumerable<Contact> BuildAccountContacts(string accountId, string tag)
    {
        yield return new Contact
        {
            Id = "filehelper",
            AccountId = accountId,
            AccountDisplayName = "Mock 账号 " + tag,
            Name = "文件传输助手 (" + tag + ")",
            Type = ContactType.Friend,
            AvatarColor = "#07C160",
            AvatarInitials = "文",
            LastMessage = "测试消息 " + tag,
            LastMessageTime = DateTime.Today.AddHours(11),
            HasLastActivity = true,
            IsOnline = true
        };
        yield return new Contact
        {
            Id = "shared-id",
            AccountId = accountId,
            AccountDisplayName = "Mock 账号 " + tag,
            Name = "共享联系人 (" + tag + ")",
            Type = ContactType.Friend,
            AvatarColor = "#0984E3",
            AvatarInitials = "共",
            LastMessage = "同一 ContactId，不同账号 " + tag,
            LastMessageTime = DateTime.Today.AddHours(10).AddMinutes(30),
            HasLastActivity = true,
            IsOnline = true
        };
        yield return new Contact
        {
            Id = "g1",
            AccountId = accountId,
            AccountDisplayName = "Mock 账号 " + tag,
            Name = "产品设计交流组 (" + tag + ")",
            Type = ContactType.Group,
            AvatarColor = "#6C5CE7",
            AvatarInitials = "产",
            LastSender = "张晓",
            LastMessage = "这个方案不错",
            LastMessageTime = DateTime.Today.AddHours(10).AddMinutes(24),
            HasLastActivity = true,
            UnreadCount = tag == "A" ? 3 : 1,
            MemberCount = 8,
            IsOnline = true
        };
        yield return new Contact
        {
            Id = "f1",
            AccountId = accountId,
            AccountDisplayName = "Mock 账号 " + tag,
            Name = "李明远 (" + tag + ")",
            Type = ContactType.Friend,
            AvatarColor = "#00B894",
            AvatarInitials = "李",
            LastMessage = "晚上一起评审一下原型？",
            LastMessageTime = DateTime.Today.AddHours(9).AddMinutes(48),
            HasLastActivity = true,
            UnreadCount = 1,
            IsOnline = true
        };
        if (tag == "A")
        {
            yield return new Contact
            {
                Id = "f2",
                AccountId = accountId,
                AccountDisplayName = "Mock 账号 A",
                Name = "王强",
                Type = ContactType.Friend,
                AvatarColor = "#0984E3",
                AvatarInitials = "王",
                LastMessage = "[图片]",
                LastMessageTime = DateTime.Today.AddHours(9).AddMinutes(12),
                HasLastActivity = true,
                IsOnline = false
            };
            yield return new Contact
            {
                Id = "g2",
                AccountId = accountId,
                AccountDisplayName = "Mock 账号 A",
                Name = "前端研发小队",
                Type = ContactType.Group,
                AvatarColor = "#E17055",
                AvatarInitials = "前",
                LastSender = "陈可",
                LastMessage = "Avalonia 动画已经合入",
                LastMessageTime = DateTime.Today.AddHours(8).AddMinutes(55),
                HasLastActivity = true,
                UnreadCount = 12,
                MemberCount = 15,
                IsOnline = true
            };
        }
    }

    private static Dictionary<string, List<ChatMessage>> BuildSeedMessages()
    {
        var map = new Dictionary<string, List<ChatMessage>>(StringComparer.Ordinal);
        SeedThread(map, AccountAId, "g1",
        [
            Msg(AccountAId, "g1", "王强", "#0984E3", "王", "大家看下这个视觉方向，深色玻璃拟态会不会太重？",
                DateTime.Today.AddHours(10).AddMinutes(5), showSep: true, sep: "今天 10:05"),
            Msg(AccountAId, "g1", "我", "#7C5CFF", "我", "这个方向可以继续，Hover 和消息入场动画也一起加上。",
                DateTime.Today.AddHours(10).AddMinutes(15), isSelf: true, showSep: true, sep: "今天 10:15"),
            Msg(AccountAId, "g1", "张晓", "#00CEC9", "张", "这个方案不错",
                DateTime.Today.AddHours(10).AddMinutes(24))
        ]);
        SeedThread(map, AccountAId, "f1",
        [
            Msg(AccountAId, "f1", "李明远", "#00B894", "李", "Avalonia 的主题切换已经做好了吗？",
                DateTime.Today.AddHours(9).AddMinutes(30), showSep: true, sep: "今天 09:30"),
            Msg(AccountAId, "f1", "我", "#7C5CFF", "我", "做好了，深色/浅色/跟随系统都可以实时切换。",
                DateTime.Today.AddHours(9).AddMinutes(35), isSelf: true),
            Msg(AccountAId, "f1", "李明远", "#00B894", "李", "晚上一起评审一下原型？",
                DateTime.Today.AddHours(9).AddMinutes(48))
        ]);
        SeedThread(map, AccountAId, "filehelper",
        [
            Msg(AccountAId, "filehelper", "文件传输助手", "#07C160", "文", "账号 A 文件助手就绪",
                DateTime.Today.AddHours(11))
        ]);
        SeedThread(map, AccountBId, "filehelper",
        [
            Msg(AccountBId, "filehelper", "文件传输助手", "#07C160", "文", "账号 B 文件助手就绪",
                DateTime.Today.AddHours(11).AddMinutes(1))
        ]);
        SeedThread(map, AccountAId, "shared-id",
        [
            Msg(AccountAId, "shared-id", "共享联系人", "#0984E3", "共", "来自账号 A",
                DateTime.Today.AddHours(10).AddMinutes(30))
        ]);
        SeedThread(map, AccountBId, "shared-id",
        [
            Msg(AccountBId, "shared-id", "共享联系人", "#0984E3", "共", "来自账号 B",
                DateTime.Today.AddHours(10).AddMinutes(31))
        ]);
        return map;
    }

    private static void SeedThread(
        Dictionary<string, List<ChatMessage>> map,
        string accountId,
        string contactId,
        List<ChatMessage> messages)
        => map[new ConversationKey(accountId, contactId).StableKey] = messages;

    private static ChatMessage Msg(
        string accountId,
        string contactId,
        string sender,
        string color,
        string initials,
        string content,
        DateTime timestamp,
        bool isSelf = false,
        bool showSep = false,
        string? sep = null)
        => new()
        {
            AccountId = accountId,
            ContactId = contactId,
            SenderName = sender,
            SenderAvatarColor = color,
            SenderInitials = initials,
            IsSelf = isSelf,
            Source = isSelf ? MessageSource.LocalUserManual : MessageSource.RemoteUser,
            Content = content,
            Timestamp = timestamp,
            ShowTimeSeparator = showSep,
            TimeSeparatorText = sep
        };
}

