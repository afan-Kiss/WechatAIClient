using WechatAIClient.Models;

namespace WechatAIClient.Services.Mock;

public sealed class MockWechatService : IWechatService
{
    private readonly object _gate = new();
    private readonly List<Contact> _contacts;
    private readonly Dictionary<string, List<ChatMessage>> _messages;

    public MockWechatService()
    {
        _contacts =
        [
            new Contact
            {
                Id = "g1",
                Name = "产品设计交流组",
                Type = ContactType.Group,
                AvatarColor = "#6C5CE7",
                AvatarInitials = "产",
                LastSender = "张晓",
                LastMessage = "这个方案不错",
                LastMessageTime = DateTime.Today.AddHours(10).AddMinutes(24),
                UnreadCount = 3,
                MemberCount = 8,
                IsOnline = true
            },
            new Contact
            {
                Id = "f1",
                Name = "李明远",
                Type = ContactType.Friend,
                AvatarColor = "#00B894",
                AvatarInitials = "李",
                LastMessage = "晚上一起评审一下原型？",
                LastMessageTime = DateTime.Today.AddHours(9).AddMinutes(48),
                UnreadCount = 1,
                IsOnline = true
            },
            new Contact
            {
                Id = "f2",
                Name = "王强",
                Type = ContactType.Friend,
                AvatarColor = "#0984E3",
                AvatarInitials = "王",
                LastMessage = "[图片]",
                LastMessageTime = DateTime.Today.AddHours(9).AddMinutes(12),
                UnreadCount = 0,
                IsOnline = false
            },
            new Contact
            {
                Id = "g2",
                Name = "前端研发小队",
                Type = ContactType.Group,
                AvatarColor = "#E17055",
                AvatarInitials = "前",
                LastSender = "陈可",
                LastMessage = "Avalonia 动画已经合入",
                LastMessageTime = DateTime.Today.AddHours(8).AddMinutes(55),
                UnreadCount = 12,
                MemberCount = 15,
                IsOnline = true
            },
            new Contact
            {
                Id = "f3",
                Name = "张晓彤",
                Type = ContactType.Friend,
                AvatarColor = "#FD79A8",
                AvatarInitials = "张",
                LastMessage = "引用了你的消息",
                LastMessageTime = DateTime.Today.AddHours(8).AddMinutes(30),
                UnreadCount = 0,
                IsOnline = true
            },
            new Contact
            {
                Id = "g3",
                Name = "AI助手体验群",
                Type = ContactType.Group,
                AvatarColor = "#A29BFE",
                AvatarInitials = "AI",
                LastSender = "系统",
                LastMessage = "DeepSeek 连接正常",
                LastMessageTime = DateTime.Today.AddHours(7).AddMinutes(40),
                UnreadCount = 0,
                MemberCount = 26,
                IsOnline = true
            },
            new Contact
            {
                Id = "f4",
                Name = "赵晨",
                Type = ContactType.Friend,
                AvatarColor = "#55EFC4",
                AvatarInitials = "赵",
                LastMessage = "文件已发送：设计参考资料合集.zip",
                LastMessageTime = DateTime.Today.AddDays(-1).AddHours(21),
                UnreadCount = 0,
                IsOnline = false
            },
            new Contact
            {
                Id = "f5",
                Name = "周雨萱",
                Type = ContactType.Friend,
                AvatarColor = "#FDCB6E",
                AvatarInitials = "周",
                LastMessage = "好的，我稍后回复",
                LastMessageTime = DateTime.Today.AddDays(-1).AddHours(18).AddMinutes(20),
                UnreadCount = 2,
                IsOnline = true
            }
        ];

        _messages = new Dictionary<string, List<ChatMessage>>
        {
            ["g1"] =
            [
                new ChatMessage
                {
                    ContactId = "g1",
                    SenderName = "王强",
                    SenderAvatarColor = "#0984E3",
                    SenderInitials = "王",
                    Content = "大家看下这个视觉方向，深色玻璃拟态会不会太重？",
                    Timestamp = DateTime.Today.AddHours(10).AddMinutes(5),
                    ShowTimeSeparator = true,
                    TimeSeparatorText = "今天 10:05"
                },
                new ChatMessage
                {
                    ContactId = "g1",
                    SenderName = "张晓彤",
                    SenderAvatarColor = "#FD79A8",
                    SenderInitials = "张",
                    Type = MessageType.Quote,
                    Content = "我觉得层次感很好，关键反馈再加强一点就行。",
                    QuoteSender = "王强",
                    QuoteContent = "深色玻璃拟态会不会太重？",
                    Timestamp = DateTime.Today.AddHours(10).AddMinutes(8)
                },
                new ChatMessage
                {
                    ContactId = "g1",
                    SenderName = "王强",
                    SenderAvatarColor = "#0984E3",
                    SenderInitials = "王",
                    Type = MessageType.Image,
                    Content = "[图片]",
                    ImageUrl = "mock-image",
                    Timestamp = DateTime.Today.AddHours(10).AddMinutes(12),
                    ShowTimeSeparator = true,
                    TimeSeparatorText = "今天 10:12"
                },
                new ChatMessage
                {
                    ContactId = "g1",
                    SenderName = "我",
                    IsSelf = true,
                    SenderAvatarColor = "#7C5CFF",
                    SenderInitials = "我",
                    Content = "这个方向可以继续，Hover 和消息入场动画也一起加上。",
                    Timestamp = DateTime.Today.AddHours(10).AddMinutes(15),
                    ShowTimeSeparator = true,
                    TimeSeparatorText = "今天 10:15"
                },
                new ChatMessage
                {
                    ContactId = "g1",
                    SenderName = "张晓",
                    SenderAvatarColor = "#00CEC9",
                    SenderInitials = "张",
                    Type = MessageType.File,
                    Content = "[文件]",
                    FileName = "设计参考资料合集.zip",
                    FileSize = "28.6 MB",
                    Timestamp = DateTime.Today.AddHours(10).AddMinutes(20)
                },
                new ChatMessage
                {
                    ContactId = "g1",
                    SenderName = "张晓",
                    SenderAvatarColor = "#00CEC9",
                    SenderInitials = "张",
                    Content = "这个方案不错",
                    Timestamp = DateTime.Today.AddHours(10).AddMinutes(24)
                }
            ],
            ["f1"] =
            [
                new ChatMessage
                {
                    ContactId = "f1",
                    SenderName = "李明远",
                    SenderAvatarColor = "#00B894",
                    SenderInitials = "李",
                    Content = "Avalonia 的主题切换已经做好了吗？",
                    Timestamp = DateTime.Today.AddHours(9).AddMinutes(30),
                    ShowTimeSeparator = true,
                    TimeSeparatorText = "今天 09:30"
                },
                new ChatMessage
                {
                    ContactId = "f1",
                    SenderName = "我",
                    IsSelf = true,
                    Content = "做好了，深色/浅色/跟随系统都可以实时切换。",
                    Timestamp = DateTime.Today.AddHours(9).AddMinutes(35)
                },
                new ChatMessage
                {
                    ContactId = "f1",
                    SenderName = "李明远",
                    SenderAvatarColor = "#00B894",
                    SenderInitials = "李",
                    Content = "晚上一起评审一下原型？",
                    Timestamp = DateTime.Today.AddHours(9).AddMinutes(48)
                }
            ]
        };
    }

    /// <summary>Optional per-contact delay override used by tests.</summary>
    public Func<string, int>? MessageLoadDelayMs { get; set; }

    public int DelayGetMessagesMs { get; set; }
    public int DelaySearchMs { get; set; }
    public bool ThrowOnSend { get; set; }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Contact>>(
                _contacts.Where(c => c.Type == ContactType.Friend).ToList());
        }
    }

    public Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Contact>>(
                _contacts.Where(c => c.Type == ContactType.Group).ToList());
        }
    }

    public Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Contact>>(
                _contacts.OrderByDescending(c => c.LastMessageTime).ToList());
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string contactId,
        CancellationToken cancellationToken = default)
    {
        var delay = MessageLoadDelayMs?.Invoke(contactId) ?? DelayGetMessagesMs;
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_messages.TryGetValue(contactId, out var list))
            {
                return list.ToList();
            }

            var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
            var fallback = new List<ChatMessage>
            {
                new()
                {
                    ContactId = contactId,
                    SenderName = contact?.Name ?? "对方",
                    SenderAvatarColor = contact?.AvatarColor ?? "#7C5CFF",
                    SenderInitials = contact?.AvatarInitials ?? "?",
                    Content = contact?.LastMessage ?? "你好",
                    Timestamp = contact?.LastMessageTime ?? DateTime.Now,
                    ShowTimeSeparator = true,
                    TimeSeparatorText = "今天"
                }
            };
            _messages[contactId] = fallback;
            return fallback.ToList();
        }
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
            IEnumerable<Contact> contacts = _contacts;
            if (tabFilter is ContactType filter)
            {
                contacts = contacts.Where(c => c.Type == filter);
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return contacts
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
                    // already matched by name; keep message summary if richer
                    continue;
                }

                hits.Add(new SearchHit
                {
                    Contact = contact,
                    MatchSummary = match.Content,
                    HitKind = SearchHitKind.Message
                });
            }

            // also match last message preview when no transcript loaded
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
                ContactId = contactId,
                SenderName = isFromAi ? "AI 助手" : "我",
                IsSelf = true,
                IsFromAi = isFromAi,
                SenderAvatarColor = "#7C5CFF",
                SenderInitials = isFromAi ? "AI" : "我",
                Type = type,
                Content = content,
                FileName = fileName,
                FileSize = fileSize,
                ImageUrl = imagePath,
                Timestamp = DateTime.Now
            };

            if (!_messages.TryGetValue(contactId, out var list))
            {
                list = [];
                _messages[contactId] = list;
            }

            list.Add(message);
            UpdateContactPreview(contactId, content, message.SenderName, message.Timestamp);
        }

        return Task.FromResult(message);
    }

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ChatMessage message;
        lock (_gate)
        {
            var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
            message = new ChatMessage
            {
                ContactId = contactId,
                SenderName = contact?.Name ?? "对方",
                SenderAvatarColor = contact?.AvatarColor ?? "#7C5CFF",
                SenderInitials = contact?.AvatarInitials ?? "?",
                Content = content,
                Timestamp = DateTime.Now,
                IsSelf = false,
                IsFromAi = false
            };

            if (!_messages.TryGetValue(contactId, out var list))
            {
                list = [];
                _messages[contactId] = list;
            }

            list.Add(message);
            UpdateContactPreview(contactId, content, message.SenderName, message.Timestamp);
        }

        MessageReceived?.Invoke(this, new MessageReceivedEventArgs
        {
            ContactId = contactId,
            MessageId = message.Id,
            Message = message,
            Timestamp = message.Timestamp
        });

        return Task.CompletedTask;
    }

    private void UpdateContactPreview(string contactId, string content, string sender, DateTime timestamp)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact is null)
        {
            return;
        }

        contact.LastMessage = content;
        contact.LastSender = sender;
        contact.LastMessageTime = timestamp;
    }
}
