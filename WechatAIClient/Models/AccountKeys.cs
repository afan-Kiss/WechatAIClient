namespace WechatAIClient.Models;

/// <summary>Stable WeChat account identity. AccountId prefers real account wxid.</summary>
public sealed record WechatAccountIdentity(
    string AccountId,
    string Wxid,
    string DisplayName,
    string? AvatarUrl = null);

public sealed record WechatAccountConnectionProfile(
    string ProfileId,
    string DisplayName,
    string BaseUrl,
    int HttpCallbackPort,
    int TcpCallbackPort,
    string? ExpectedAccountWxid,
    bool Enabled,
    string? RuntimeVersionHint = null,
    bool AllowAutoReply = true);

/// <summary>Account-scoped conversation identity. Prefer this over hand-built strings.</summary>
public readonly record struct ConversationKey(string AccountId, string ConversationId)
{
    public string StableKey => $"{AccountId}::{ConversationId}";

    public static ConversationKey Parse(string stableKey)
    {
        var idx = stableKey.IndexOf("::", StringComparison.Ordinal);
        if (idx <= 0 || idx >= stableKey.Length - 2)
        {
            throw new FormatException("Invalid ConversationKey: " + stableKey);
        }

        return new ConversationKey(stableKey[..idx], stableKey[(idx + 2)..]);
    }

    public override string ToString() => StableKey;
}

/// <summary>Account-scoped message identity for dedup / pin / lookup.</summary>
public readonly record struct MessageKey(string AccountId, string ConversationId, string MessageId)
{
    public ConversationKey Conversation => new(AccountId, ConversationId);
    public string StableKey => $"{AccountId}::{ConversationId}::{MessageId}";

    public override string ToString() => StableKey;
}

public enum MediaLoadState
{
    None,
    Loading,
    Loaded,
    Failed
}

public enum AvatarLoadState
{
    None,
    Loading,
    Loaded,
    Failed
}
