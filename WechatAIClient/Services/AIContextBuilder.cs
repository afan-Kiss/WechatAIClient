using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class AIContextBuilder : IAIContextBuilder
{
    private const int SystemReserveTokens = 400;

    public AIContextBuildResult Build(AIContextBuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var contactId = input.ContactId ?? string.Empty;
        var requested = Math.Max(0, input.ContextCount);
        var snapshot = (input.Messages ?? Array.Empty<ChatMessage>())
            .Where(m => string.Equals(m.ContactId, contactId, StringComparison.Ordinal))
            .ToList();

        var excluded = input.TemporarilyExcludedMessageIds;
        var pinnedIdSet = new HashSet<string>(
            (input.PinnedMessageIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);

        // Step 2–5: normalize + filter
        var filteredOut = 0;
        var candidates = new List<(ChatMessage Original, string Content, MessageSource Source)>();

        foreach (var msg in snapshot)
        {
            if (!TryNormalize(msg, out var content, out var source))
            {
                filteredOut++;
                continue;
            }

            if (!PassesIncludeOwnFilter(source, msg.IsSelf, input.IncludeOwnMessages))
            {
                filteredOut++;
                continue;
            }

            if (excluded is not null && excluded.Contains(msg.Id))
            {
                filteredOut++;
                continue;
            }

            candidates.Add((msg, content, source));
        }

        // Step 6: take last ContextCount from ordinary (non-pinned-priority) pool
        // Ordinary = messages that are NOT in the pinned set for selection purposes
        var ordinaryPool = candidates
            .Where(c => !pinnedIdSet.Contains(c.Original.Id))
            .OrderBy(c => c.Original.Timestamp)
            .ThenBy(c => c.Original.Id, StringComparer.Ordinal)
            .ToList();

        var selectedOrdinary = ordinaryPool
            .TakeLast(requested)
            .ToList();

        // Step 7: merge pinned from original snapshot (must belong to contact), dedupe
        var byId = new Dictionary<string, (ChatMessage Original, string Content, MessageSource Source, bool IsPinned)>(
            StringComparer.Ordinal);

        foreach (var item in selectedOrdinary)
        {
            byId[item.Original.Id] = (item.Original, item.Content, item.Source, false);
        }

        var pinnedCount = 0;
        foreach (var pinId in pinnedIdSet)
        {
            var original = snapshot.FirstOrDefault(m => string.Equals(m.Id, pinId, StringComparison.Ordinal));
            if (original is null)
            {
                continue;
            }

            if (!TryNormalize(original, out var pinContent, out var pinSource))
            {
                continue;
            }

            // Pinned can be outside N; still apply IncludeOwn filter for consistency
            if (!PassesIncludeOwnFilter(pinSource, original.IsSelf, input.IncludeOwnMessages))
            {
                continue;
            }

            if (excluded is not null && excluded.Contains(original.Id))
            {
                continue;
            }

            byId[original.Id] = (original, pinContent, pinSource, true);
            pinnedCount++;
        }

        // Step 8: sort by timestamp ascending
        var ordered = byId.Values
            .OrderBy(x => x.Original.Timestamp)
            .ThenBy(x => x.Original.Id, StringComparer.Ordinal)
            .Select(x => new WorkingMessage
            {
                MessageId = x.Original.Id,
                Content = x.Content,
                Source = x.Source,
                ContactId = x.Original.ContactId,
                IsPinned = x.IsPinned,
                Timestamp = x.Original.Timestamp
            })
            .ToList();

        var selectedOrdinaryCount = selectedOrdinary.Count;

        // Step 9: token budget — reserve ~400 for system/style/temp
        var budget = Math.Max(1, input.TokenBudget);
        var historyBudget = Math.Max(1, budget - SystemReserveTokens);
        var trimmedByBudget = TrimByBudget(ordered, historyBudget);

        // Step 10: build system messages (not counted in ContextCount)
        var systemMessages = BuildSystemMessages(input);
        var historyMessages = ordered.Select(ToContextMessage).ToList();
        var allMessages = systemMessages.Concat(historyMessages).ToList();

        var remoteCount = historyMessages.Count(m => m.Source is MessageSource.RemoteUser or MessageSource.AttachmentPlaceholder);
        var ownCount = historyMessages.Count(m =>
            m.Source is MessageSource.LocalUserManual or MessageSource.LocalUserAI);

        var estimated = allMessages.Sum(m => EstimateTokens(m.Content));
        var keptHistory = historyMessages.Count;
        var summary = BuildSummaryText(
            includeOwn: input.IncludeOwnMessages,
            kept: keptHistory,
            remote: remoteCount,
            own: ownCount,
            pinned: historyMessages.Count(m => m.IsPinned),
            trimmed: trimmedByBudget > 0);

        return new AIContextBuildResult
        {
            Messages = allMessages,
            RequestedCount = requested,
            SelectedOrdinaryCount = selectedOrdinaryCount,
            RemoteCount = remoteCount,
            OwnCount = ownCount,
            PinnedCount = historyMessages.Count(m => m.IsPinned),
            FilteredOutCount = filteredOut,
            TrimmedByBudgetCount = trimmedByBudget,
            IncludeOwnMessages = input.IncludeOwnMessages,
            SummaryText = summary,
            TemporaryInstruction = input.TemporaryInstruction,
            ReplyStyle = input.ReplyStyle,
            ReplyLength = input.ReplyLength,
            EstimatedTokens = estimated
        };
    }

    public static int EstimateTokens(string? content)
        => Math.Max(1, (content?.Length ?? 0) / 4);

    private static bool TryNormalize(ChatMessage msg, out string content, out MessageSource source)
    {
        content = string.Empty;
        source = msg.Source;

        if (msg.Type == MessageType.System)
        {
            return false;
        }

        // Infer Source for legacy messages that never set it
        if (source == MessageSource.RemoteUser && msg.IsSelf)
        {
            source = msg.IsFromAi ? MessageSource.LocalUserAI : MessageSource.LocalUserManual;
        }

        switch (msg.Type)
        {
            case MessageType.Image:
                content = "[图片]";
                source = MessageSource.AttachmentPlaceholder;
                return true;
            case MessageType.File:
                content = $"[文件：{msg.FileName ?? "未知"}，{msg.FileSize ?? "?"}]";
                source = MessageSource.AttachmentPlaceholder;
                return true;
            case MessageType.Emoji:
                content = string.IsNullOrWhiteSpace(msg.Content) ? "[表情]" : msg.Content;
                return !string.IsNullOrWhiteSpace(content);
            case MessageType.Quote:
            case MessageType.Text:
            default:
                content = msg.Content ?? string.Empty;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return false;
                }

                return true;
        }
    }

    private static bool PassesIncludeOwnFilter(MessageSource source, bool isSelf, bool includeOwn)
    {
        return source switch
        {
            MessageSource.RemoteUser => true,
            // Spec: only remote attachments in both includeOwn modes
            MessageSource.AttachmentPlaceholder => !isSelf,
            MessageSource.LocalUserManual => includeOwn,
            MessageSource.LocalUserAI => includeOwn,
            MessageSource.System => false,
            _ => false
        };
    }

    private static int TrimByBudget(List<WorkingMessage> ordered, int historyBudget)
    {
        var trimmed = 0;
        while (ordered.Count > 0)
        {
            var total = ordered.Sum(m => EstimateTokens(m.Content));
            if (total <= historyBudget)
            {
                break;
            }

            var ordinaryIndex = -1;
            for (var i = 0; i < ordered.Count; i++)
            {
                if (!ordered[i].IsPinned)
                {
                    ordinaryIndex = i;
                    break;
                }
            }

            if (ordinaryIndex >= 0)
            {
                ordered.RemoveAt(ordinaryIndex);
                trimmed++;
                continue;
            }

            // only pinned left — trim oldest pinned last
            ordered.RemoveAt(0);
            trimmed++;
        }

        return trimmed;
    }

    private static List<AIContextMessage> BuildSystemMessages(AIContextBuildInput input)
    {
        var list = new List<AIContextMessage>();
        var styleHint = input.ReplyStyle switch
        {
            ReplyStyle.Concise => "风格：简短直接，去掉多余客套。",
            ReplyStyle.Formal => "风格：礼貌、专业、清晰，措辞正式。",
            ReplyStyle.Humorous => "风格：适度幽默，不冒犯、不夸张。",
            _ => "风格：自然、口语化，匹配当前聊天语气。"
        };
        var lengthHint = input.ReplyLength switch
        {
            ReplyLength.Short => "长度：控制在一两句话内。",
            ReplyLength.Long => "长度：可写得更完整，约三四段以内。",
            _ => "长度：适中，通常两三句即可。"
        };

        list.Add(new AIContextMessage
        {
            MessageId = "sys-style",
            Role = "system",
            Content = $"{styleHint}{lengthHint}",
            Source = MessageSource.System,
            ContactId = input.ContactId,
            Timestamp = DateTime.MinValue
        });

        if (!string.IsNullOrWhiteSpace(input.TemporaryInstruction))
        {
            list.Add(new AIContextMessage
            {
                MessageId = "sys-temp",
                Role = "system",
                Content = $"临时指令：{input.TemporaryInstruction.Trim()}",
                Source = MessageSource.System,
                ContactId = input.ContactId,
                Timestamp = DateTime.MinValue
            });
        }

        return list;
    }

    private static AIContextMessage ToContextMessage(WorkingMessage m) => new()
    {
        MessageId = m.MessageId,
        Role = MapRole(m.Source),
        Content = m.Content,
        Source = m.Source,
        ContactId = m.ContactId,
        IsPinned = m.IsPinned,
        Timestamp = m.Timestamp
    };

    private static string MapRole(MessageSource source) => source switch
    {
        MessageSource.RemoteUser => "user",
        MessageSource.LocalUserManual => "assistant",
        MessageSource.LocalUserAI => "assistant",
        MessageSource.System => "system",
        MessageSource.AttachmentPlaceholder => "user",
        _ => "user"
    };

    private static string BuildSummaryText(
        bool includeOwn,
        int kept,
        int remote,
        int own,
        int pinned,
        bool trimmed)
    {
        string core;
        if (includeOwn)
        {
            core = $"本次参考 {kept} 条 · 对方 {remote} · 自己 {own}";
        }
        else
        {
            core = $"本次参考 {kept} 条 · 仅对方消息";
        }

        if (pinned > 0)
        {
            core += $" · 📌{pinned}";
        }

        if (trimmed)
        {
            core += " · 已按长度裁剪较早消息";
        }

        return core;
    }

    private sealed class WorkingMessage
    {
        public string MessageId { get; set; } = "";
        public string Content { get; set; } = "";
        public MessageSource Source { get; set; }
        public string ContactId { get; set; } = "";
        public bool IsPinned { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
