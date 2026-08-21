using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IAIService
{
    string ModelName { get; }
    bool IsConnected { get; }
    AIProviderKind ProviderKind { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<AIResponse> GenerateAsync(AIRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AIStreamEvent> GenerateStreamAsync(AIRequest request, CancellationToken cancellationToken = default);
    Task<AIConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
