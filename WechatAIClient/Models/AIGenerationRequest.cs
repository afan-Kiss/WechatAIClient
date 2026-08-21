namespace WechatAIClient.Models;

public sealed class AIGenerationRequest
{
    public string GenerationId { get; set; } = Guid.NewGuid().ToString("N");
    public string AccountId { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string? TriggerAccountId { get; set; }
    public string? TriggerConversationId { get; set; }
    public string? TriggerMessageId { get; set; }
    public List<ChatMessage> ContextSnapshot { get; set; } = [];
    public int ContextLength { get; set; } = 10;
    public AIReplyMode ReplyMode { get; set; } = AIReplyMode.Auto;
    public bool IncludeOwnMessages { get; set; } = true;
    public string? TemporaryInstruction { get; set; }
    public ReplyStyle ReplyStyle { get; set; } = ReplyStyle.Natural;
    public ReplyLength ReplyLength { get; set; } = ReplyLength.Medium;
    public IReadOnlyList<string> PinnedMessageIds { get; set; } = Array.Empty<string>();
    public IReadOnlySet<string>? TemporarilyExcludedMessageIds { get; set; }
    public int? DraftRevisionAtStart { get; set; }
    public bool IsGroup { get; set; }
    public int TokenBudget { get; set; } = 3500;
}
