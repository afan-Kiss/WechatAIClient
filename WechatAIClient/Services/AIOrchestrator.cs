using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class AIOrchestrator
{
    private readonly IAIService _aiService;
    private readonly ILogger<AIOrchestrator> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;

    public AIOrchestrator(IAIService aiService, ILogger<AIOrchestrator> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public AIGenerationRequest? LastRequest { get; private set; }

    public void CancelAll()
    {
        lock (_gate)
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignored
            }

            _cts?.Dispose();
            _cts = null;
        }
    }

    public async Task<AIGenerationResult?> GenerateAsync(
        AIGenerationRequest request,
        Action<string>? onTypingChunk = null,
        CancellationToken linked = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CancelAll();

        var localCts = CancellationTokenSource.CreateLinkedTokenSource(linked);
        lock (_gate)
        {
            _cts = localCts;
        }

        LastRequest = request;
        var token = localCts.Token;

        try
        {
            var context = request.ContextSnapshot
                .TakeLast(Math.Max(1, request.ContextLength))
                .ToList();

            var content = await _aiService.GenerateReplyAsync(context, token);
            if (token.IsCancellationRequested || string.IsNullOrEmpty(content))
            {
                return null;
            }

            await AnimateTypingAsync(content, onTypingChunk, token);
            if (token.IsCancellationRequested)
            {
                return null;
            }

            return new AIGenerationResult
            {
                GenerationId = request.GenerationId,
                ContactId = request.ContactId,
                Content = content
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("AI generation cancelled for {ContactId}", request.ContactId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI generation failed for {ContactId}", request.ContactId);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cts, localCts))
                {
                    _cts = null;
                }
            }

            localCts.Dispose();
        }
    }

    private static async Task AnimateTypingAsync(
        string text,
        Action<string>? onTypingChunk,
        CancellationToken cancellationToken)
    {
        if (onTypingChunk is null)
        {
            return;
        }

        var preview = string.Empty;
        var random = Random.Shared;
        var index = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = Math.Min(random.Next(4, 9), text.Length - index);
            preview += text.Substring(index, chunk);
            index += chunk;
            onTypingChunk(preview);
            await Task.Delay(18, cancellationToken);
        }
    }
}
