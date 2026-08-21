namespace WechatAIClient.Models;

public sealed class ChatMessagesChangedEventArgs : EventArgs
{
    public ChatMessagesChangedEventArgs(string contactId, bool forceScroll = true)
        : this(accountId: string.Empty, contactId, forceScroll)
    {
    }

    public ChatMessagesChangedEventArgs(string accountId, string contactId, bool forceScroll = true)
    {
        AccountId = accountId ?? string.Empty;
        ContactId = contactId;
        ForceScroll = forceScroll;
    }

    public string AccountId { get; }
    public string ContactId { get; }
    public bool ForceScroll { get; }

    public ConversationKey Conversation => new(AccountId, ContactId);
}
