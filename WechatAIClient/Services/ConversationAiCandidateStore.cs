using System.Collections.Concurrent;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IConversationAiCandidateStore
{
    void Set(ConversationKey key, string content);
    bool TryGet(ConversationKey key, out string content);
    void Clear(ConversationKey key);
}

public sealed class ConversationAiCandidateStore : IConversationAiCandidateStore
{
    private readonly ConcurrentDictionary<string, string> _candidates = new(StringComparer.Ordinal);

    public void Set(ConversationKey key, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            Clear(key);
            return;
        }

        _candidates[key.StableKey] = content;
    }

    public bool TryGet(ConversationKey key, out string content)
        => _candidates.TryGetValue(key.StableKey, out content!);

    public void Clear(ConversationKey key)
        => _candidates.TryRemove(key.StableKey, out _);
}
