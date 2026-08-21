namespace WechatAIClient.Models;

public sealed class AIGenerationResult
{
    public required string GenerationId { get; init; }
    public required string ContactId { get; init; }
    public required string Content { get; init; }
}
