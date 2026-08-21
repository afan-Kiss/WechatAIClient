namespace WechatAIClient.Services.Wechat;

/// <summary>
/// Per-conversation recent MessageId window to drop duplicate bridge events.
/// </summary>
public sealed class MessageDeduplicator
{
    private readonly int _capacityPerConversation;
    private readonly Dictionary<string, LinkedList<string>> _queues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _sets = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public MessageDeduplicator(int capacityPerConversation = 500)
    {
        _capacityPerConversation = Math.Max(16, capacityPerConversation);
    }

    /// <summary>Returns false if the id was already seen for this conversation.</summary>
    public bool TryAdd(string conversationId, string messageId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(messageId))
        {
            return true;
        }

        lock (_gate)
        {
            if (!_queues.TryGetValue(conversationId, out var queue))
            {
                queue = new LinkedList<string>();
                _queues[conversationId] = queue;
                _sets[conversationId] = new HashSet<string>(StringComparer.Ordinal);
            }

            var set = _sets[conversationId];
            if (!set.Add(messageId))
            {
                return false;
            }

            queue.AddLast(messageId);
            while (queue.Count > _capacityPerConversation)
            {
                var oldest = queue.First!.Value;
                queue.RemoveFirst();
                set.Remove(oldest);
            }

            return true;
        }
    }

    public void Clear(string? conversationId = null)
    {
        lock (_gate)
        {
            if (conversationId is null)
            {
                _queues.Clear();
                _sets.Clear();
                return;
            }

            _queues.Remove(conversationId);
            _sets.Remove(conversationId);
        }
    }
}
