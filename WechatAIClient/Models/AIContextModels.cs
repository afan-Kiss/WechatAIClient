namespace WechatAIClient.Models;

public sealed class AIContextBuildInput
{
    public string ContactId { get; set; } = "";
    public string ContactName { get; set; } = "";
    public bool IsGroup { get; set; }
    public IReadOnlyList<ChatMessage> Messages { get; set; } = Array.Empty<ChatMessage>();
    public int ContextCount { get; set; } = 10;
    public bool IncludeOwnMessages { get; set; } = true;
    public IReadOnlyList<string> PinnedMessageIds { get; set; } = Array.Empty<string>();
    public string? TemporaryInstruction { get; set; }
    public ReplyStyle ReplyStyle { get; set; } = ReplyStyle.Natural;
    public ReplyLength ReplyLength { get; set; } = ReplyLength.Medium;
    public int TokenBudget { get; set; } = 3500;
    public IReadOnlySet<string>? TemporarilyExcludedMessageIds { get; set; }
}

public sealed class AIContextMessage
{
    public string MessageId { get; set; } = "";
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public MessageSource Source { get; set; }
    public string ContactId { get; set; } = "";
    public bool IsPinned { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class AIContextBuildResult
{
    public IReadOnlyList<AIContextMessage> Messages { get; set; } = Array.Empty<AIContextMessage>();
    public int RequestedCount { get; set; }
    public int SelectedOrdinaryCount { get; set; }
    public int RemoteCount { get; set; }
    public int OwnCount { get; set; }
    public int PinnedCount { get; set; }
    public int FilteredOutCount { get; set; }
    public int TrimmedByBudgetCount { get; set; }
    public bool IncludeOwnMessages { get; set; }
    public string SummaryText { get; set; } = "";
    public string? TemporaryInstruction { get; set; }
    public ReplyStyle ReplyStyle { get; set; }
    public ReplyLength ReplyLength { get; set; }
    public int EstimatedTokens { get; set; }
}

public sealed class AIRequest
{
    public string ContactId { get; set; } = "";
    public string GenerationId { get; set; } = "";
    public IReadOnlyList<AIContextMessage> Messages { get; set; } = Array.Empty<AIContextMessage>();
    public ReplyStyle Style { get; set; } = ReplyStyle.Natural;
    public ReplyLength Length { get; set; } = ReplyLength.Medium;
    public string? TemporaryInstruction { get; set; }
    public AIContextBuildResult ContextMeta { get; set; } = new();
}

public sealed class AIResponse
{
    public string Content { get; set; } = "";
    public string GenerationId { get; set; } = "";
    public string ContactId { get; set; } = "";
}
