using WechatAIClient.Models;

namespace WechatAIClient.Services.Wechat;

public enum OutgoingMatchSource
{
    UserManual,
    AiGenerated
}

public sealed record PendingOutgoingEntry(
    string ClientRequestId,
    string ConversationId,
    string Content,
    bool IsFromAi,
    DateTime Timestamp);

/// <summary>
/// Matches WeChat IsFromMe echoes to local pending sends to avoid duplicate UI rows
/// and to classify AI vs manual source for the auto-reply pipeline.
/// </summary>
public sealed class PendingOutgoingTracker
{
    private readonly TimeSpan _matchWindow;
    private readonly List<PendingOutgoingEntry> _pending = [];
    private readonly object _gate = new();

    public PendingOutgoingTracker(TimeSpan? matchWindow = null)
    {
        _matchWindow = matchWindow ?? TimeSpan.FromMinutes(2);
    }

    public void Register(string clientRequestId, string conversationId, string content, bool isFromAi, DateTime? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(clientRequestId) || string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        lock (_gate)
        {
            PruneLocked(DateTime.UtcNow);
            _pending.Add(new PendingOutgoingEntry(
                clientRequestId,
                conversationId,
                content ?? string.Empty,
                isFromAi,
                timestamp ?? DateTime.UtcNow));
        }
    }

    public bool TryMatchEcho(
        string conversationId,
        string content,
        out OutgoingMatchSource source,
        out string? clientRequestId)
    {
        source = OutgoingMatchSource.UserManual;
        clientRequestId = null;
        lock (_gate)
        {
            PruneLocked(DateTime.UtcNow);
            for (var i = 0; i < _pending.Count; i++)
            {
                var item = _pending[i];
                if (!string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(item.Content, content, StringComparison.Ordinal))
                {
                    continue;
                }

                source = item.IsFromAi ? OutgoingMatchSource.AiGenerated : OutgoingMatchSource.UserManual;
                clientRequestId = item.ClientRequestId;
                _pending.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool TryConsumeByClientRequestId(string clientRequestId, out PendingOutgoingEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(clientRequestId))
        {
            return false;
        }

        lock (_gate)
        {
            for (var i = 0; i < _pending.Count; i++)
            {
                if (!string.Equals(_pending[i].ClientRequestId, clientRequestId, StringComparison.Ordinal))
                {
                    continue;
                }

                entry = _pending[i];
                _pending.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    private void PruneLocked(DateTime utcNow)
    {
        _pending.RemoveAll(p => utcNow - p.Timestamp > _matchWindow);
    }
}
