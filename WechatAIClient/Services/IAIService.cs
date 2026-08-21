using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IAIService
{
    string ModelName { get; }
    bool IsConnected { get; }
    Task<string> GenerateReplyAsync(IReadOnlyList<ChatMessage> context, CancellationToken cancellationToken = default);
    Task ConnectAsync(CancellationToken cancellationToken = default);
}
