namespace WechatAIClient.Models;

public sealed class ChatMessagesChangedEventArgs : EventArgs
{
    public ChatMessagesChangedEventArgs(string contactId, bool forceScroll = true)
    {
        ContactId = contactId;
        ForceScroll = forceScroll;
    }

    public string ContactId { get; }
    public bool ForceScroll { get; }
}
