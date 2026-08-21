namespace WechatAIClient.Models;

public enum SearchHitKind
{
    Contact,
    Group,
    Message
}

public sealed class SearchHit
{
    public required Contact Contact { get; init; }
    public string MatchSummary { get; init; } = string.Empty;
    public SearchHitKind HitKind { get; init; } = SearchHitKind.Contact;
}
