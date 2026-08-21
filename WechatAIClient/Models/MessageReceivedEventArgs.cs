namespace WechatAIClient.Models;

public sealed class MessageReceivedEventArgs : EventArgs
{
    public required string AccountId { get; init; }
    public required string ContactId { get; init; }
    public required string MessageId { get; init; }
    public required ChatMessage Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public ConversationKey Conversation => new(AccountId, ContactId);
}

public sealed class AccountConnectionStateChangedEventArgs : EventArgs
{
    public required string AccountId { get; init; }
    public required WechatConnectionState State { get; init; }
}

public sealed class AccountIdentityChangedEventArgs : EventArgs
{
    public required string ProfileId { get; init; }
    public string? OldAccountId { get; init; }
    public required string NewAccountId { get; init; }
}
