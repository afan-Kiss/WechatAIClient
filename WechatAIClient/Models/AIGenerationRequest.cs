namespace WechatAIClient.Models;

public sealed class AIGenerationRequest
{
    public string GenerationId { get; set; } = Guid.NewGuid().ToString("N");
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public List<ChatMessage> ContextSnapshot { get; set; } = [];
    public int ContextLength { get; set; } = 10;
    public AIReplyMode ReplyMode { get; set; } = AIReplyMode.Auto;
}
