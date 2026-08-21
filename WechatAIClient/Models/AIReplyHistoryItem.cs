namespace WechatAIClient.Models;

public sealed class AIReplyHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Status { get; set; } = "自动回复";
    public string Content { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? RequestId { get; set; }
    public string? ContextSummary { get; set; }
}
