using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.Mock;

public sealed class MockAIService : IAIService
{
    private readonly ILogger<MockAIService> _logger;
    private readonly string[] _templates =
    [
        "收到，这个方案整体方向很清晰，我建议先把核心交互流程定下来。",
        "可以的。我稍后整理一版更简洁的回复，方便你直接发送。",
        "理解你的意思了。当前上下文里大家都认可深色玻璃拟态，我建议保持统一视觉语言。",
        "好的，我根据最近几条消息生成了一条自然、礼貌的回复供你参考。",
        "这个点很关键。我建议补充一句确认时间，避免对方误解。"
    ];

    public MockAIService(ILogger<MockAIService> logger)
    {
        _logger = logger;
    }

    public string ModelName => "DeepSeek-V3";

    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = true;
        _logger.LogInformation("Mock AI service connected: {Model}", ModelName);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateReplyAsync(
        IReadOnlyList<ChatMessage> context,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(700, cancellationToken);

        var seed = context.Count == 0
            ? Random.Shared.Next()
            : Math.Abs(context[^1].Content.GetHashCode(StringComparison.Ordinal));
        var reply = _templates[seed % _templates.Length];

        // Chunk-friendly pause so orchestrator typing animation stays responsive to cancel.
        await Task.Delay(120, cancellationToken);
        _logger.LogInformation("Generated mock AI reply with {Count} context messages", context.Count);
        return reply;
    }
}
