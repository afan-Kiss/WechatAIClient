namespace WechatAIClient.Models;

public sealed class AIGenerationResult
{
    public required string GenerationId { get; init; }
    public required string ContactId { get; init; }
    public required string Content { get; init; }
    public int? DraftRevisionAtStart { get; init; }
    public string? ContextSummary { get; init; }
}
