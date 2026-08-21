using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services.DeepSeek;
using WechatAIClient.Services.Mock;

namespace WechatAIClient.Services;

/// <summary>
/// Routes AI calls to Mock or DeepSeek based on provider settings and API key presence.
/// </summary>
public sealed class RoutingAIService : IAIService
{
    private readonly MockAIService _mock;
    private readonly DeepSeekAIService _deepSeek;
    private readonly IAISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ILogger<RoutingAIService> _logger;
    private IAIService _active;

    public RoutingAIService(
        MockAIService mock,
        DeepSeekAIService deepSeek,
        IAISettingsService settings,
        ISecretStore secrets,
        ILogger<RoutingAIService> logger)
    {
        _mock = mock;
        _deepSeek = deepSeek;
        _settings = settings;
        _secrets = secrets;
        _logger = logger;
        _active = mock;
    }

    public string ModelName => _active.ModelName;

    public bool IsConnected => _active.IsConnected;

    public AIProviderKind ProviderKind => _active.ProviderKind;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _active = await ResolveAsync(cancellationToken);
        await _active.ConnectAsync(cancellationToken);
    }

    public async Task<AIResponse> GenerateAsync(AIRequest request, CancellationToken cancellationToken = default)
    {
        _active = await ResolveAsync(cancellationToken);
        return await _active.GenerateAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<AIStreamEvent> GenerateStreamAsync(
        AIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _active = await ResolveAsync(cancellationToken);
        await foreach (var evt in _active.GenerateStreamAsync(request, cancellationToken))
        {
            yield return evt;
        }
    }

    public async Task<AIConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _active = await ResolveAsync(cancellationToken);
        return await _active.TestConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Forces resolution without generating — useful for settings UI.
    /// </summary>
    public async Task<IAIService> ResolveTargetAsync(CancellationToken cancellationToken = default)
    {
        _active = await ResolveAsync(cancellationToken);
        return _active;
    }

    private async Task<IAIService> ResolveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _settings.GetProviderSettingsAsync(cancellationToken);
            if (provider.Provider == AIProviderKind.DeepSeek)
            {
                var key = await _secrets.GetSecretAsync(DeepSeekAIService.ApiKeySecretName, cancellationToken);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _logger.LogDebug("Routing AI to DeepSeek model={Model}", provider.ModelId);
                    return _deepSeek;
                }

                _logger.LogDebug("DeepSeek selected but API key missing; using Mock");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve AI provider; falling back to Mock");
        }

        return _mock;
    }
}
