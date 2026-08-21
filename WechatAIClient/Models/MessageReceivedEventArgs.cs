namespace WechatAIClient.Models;

public sealed class MessageReceivedEventArgs : EventArgs
{
    public required string ContactId { get; init; }
    public required string MessageId { get; init; }
    public required ChatMessage Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
