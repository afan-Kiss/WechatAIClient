using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IAIService
{
    string ModelName { get; }
    bool IsConnected { get; }
    Task<AIResponse> GenerateAsync(AIRequest request, CancellationToken cancellationToken = default);
    Task ConnectAsync(CancellationToken cancellationToken = default);
}
