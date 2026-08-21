namespace WechatAIClient.Models;

public sealed class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ContactId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderAvatarColor { get; set; } = "#7C5CFF";
    public string SenderInitials { get; set; } = "?";
    public bool IsSelf { get; set; }
    public bool IsFromAi { get; set; }
    public MessageSource Source { get; set; } = MessageSource.RemoteUser;
    public MessageType Type { get; set; } = MessageType.Text;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public string? QuoteSender { get; set; }
    public string? QuoteContent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool ShowTimeSeparator { get; set; }
    public string? TimeSeparatorText { get; set; }
    public bool MentionsMe { get; set; }
    public bool QuotesMe { get; set; }
}
