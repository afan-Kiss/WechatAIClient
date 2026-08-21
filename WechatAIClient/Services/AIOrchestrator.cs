using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class AIOrchestrator
{
    private readonly IAIService _aiService;
    private readonly IAIContextBuilder _contextBuilder;
    private readonly ILogger<AIOrchestrator> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;

    public AIOrchestrator(
        IAIService aiService,
        IAIContextBuilder contextBuilder,
        ILogger<AIOrchestrator> logger)
    {
        _aiService = aiService;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public AIGenerationRequest? LastRequest { get; private set; }
    public AIContextBuildResult? LastBuildResult { get; private set; }
    public AIRequest? LastAiRequest { get; private set; }

    public void CancelAll() => CancelCurrentGeneration();

    public void CancelCurrentGeneration()
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
            var buildInput = new AIContextBuildInput
            {
                ContactId = request.ContactId,
                ContactName = request.ContactName,
                IsGroup = request.IsGroup,
                Messages = request.ContextSnapshot,
                ContextCount = Math.Max(1, request.ContextLength),
                IncludeOwnMessages = request.IncludeOwnMessages,
                PinnedMessageIds = request.PinnedMessageIds,
                TemporaryInstruction = request.TemporaryInstruction,
                ReplyStyle = request.ReplyStyle,
                ReplyLength = request.ReplyLength,
                TokenBudget = request.TokenBudget > 0 ? request.TokenBudget : 3500,
                TemporarilyExcludedMessageIds = request.TemporarilyExcludedMessageIds
            };

            var buildResult = _contextBuilder.Build(buildInput);
            LastBuildResult = buildResult;

            var aiRequest = new AIRequest
            {
                ContactId = request.ContactId,
                GenerationId = request.GenerationId,
                Messages = buildResult.Messages,
                Style = request.ReplyStyle,
                Length = request.ReplyLength,
                TemporaryInstruction = request.TemporaryInstruction,
                ContextMeta = buildResult
            };
            LastAiRequest = aiRequest;

            var response = await _aiService.GenerateAsync(aiRequest, token);
            if (token.IsCancellationRequested || response is null || string.IsNullOrEmpty(response.Content))
            {
                return null;
            }

            await AnimateTypingAsync(response.Content, onTypingChunk, token);
            if (token.IsCancellationRequested)
            {
                return null;
            }

            return new AIGenerationResult
            {
                GenerationId = request.GenerationId,
                ContactId = request.ContactId,
                Content = response.Content,
                DraftRevisionAtStart = request.DraftRevisionAtStart,
                ContextSummary = buildResult.SummaryText
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
