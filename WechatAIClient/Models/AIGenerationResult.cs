namespace WechatAIClient.Models;

public sealed class AIGenerationResult
{
    public required string GenerationId { get; init; }
    public string AccountId { get; init; } = string.Empty;
    public required string ContactId { get; init; }
    public required string Content { get; init; }
    public int? DraftRevisionAtStart { get; init; }
    public string? ContextSummary { get; init; }
    public AIGenerationStatus Status { get; init; } = AIGenerationStatus.Completed;
    public AIErrorKind ErrorKind { get; init; } = AIErrorKind.None;
    public string? Model { get; init; }
    public string? RequestId { get; init; }
}
