using System.Collections.Concurrent;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

/// <summary>Per-conversation draft isolation across accounts.</summary>
public interface IConversationDraftStore
{
    string? GetDraft(ConversationKey key);
    void SetDraft(ConversationKey key, string? text);
    void Clear(ConversationKey key);
}

public sealed class ConversationDraftStore : IConversationDraftStore
{
    private readonly ConcurrentDictionary<string, string> _drafts = new(StringComparer.Ordinal);

    public string? GetDraft(ConversationKey key)
        => _drafts.TryGetValue(key.StableKey, out var text) ? text : null;

    public void SetDraft(ConversationKey key, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _drafts.TryRemove(key.StableKey, out _);
            return;
        }

        _drafts[key.StableKey] = text;
    }

    public void Clear(ConversationKey key) => _drafts.TryRemove(key.StableKey, out _);
}
