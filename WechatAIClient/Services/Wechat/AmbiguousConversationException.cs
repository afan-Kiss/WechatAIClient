namespace WechatAIClient.Services.Wechat;

/// <summary>
/// Thrown when a legacy contactId maps to more than one account session and no SelectedAccountId is set.
/// </summary>
public sealed class AmbiguousConversationException : InvalidOperationException
{
    public AmbiguousConversationException(string contactId, IReadOnlyList<string> accountIds)
        : base(BuildMessage(contactId, accountIds))
    {
        ContactId = contactId;
        AccountIds = accountIds;
    }

    public string ContactId { get; }
    public IReadOnlyList<string> AccountIds { get; }

    private static string BuildMessage(string contactId, IReadOnlyList<string> accountIds)
        => $"ContactId '{contactId}' is ambiguous across accounts: {string.Join(", ", accountIds)}. Use ConversationKey.";
}
