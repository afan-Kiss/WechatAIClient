using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class AIOrchestrator
{
    private const int MaxConcurrent = 3;
    private const int UiBatchMs = 50;

    private readonly IAIService _aiService;
    private readonly IAIContextBuilder _contextBuilder;
    private readonly ILogger<AIOrchestrator> _logger;
    private readonly SemaphoreSlim _globalGate = new(MaxConcurrent, MaxConcurrent);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _perContact = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private AIGenerationStatus _status = AIGenerationStatus.Idle;

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
    public string? ActiveContactId { get; private set; }

    public AIGenerationStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StatusChanged;

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
        }

        foreach (var pair in _perContact)
        {
            try
            {
                pair.Value.Cancel();
            }
            catch
            {
                // ignored
            }
        }

        if (Status is AIGenerationStatus.PreparingContext or AIGenerationStatus.Connecting or AIGenerationStatus.Streaming)
        {
            Status = AIGenerationStatus.Cancelled;
        }
    }

    public void CancelContactGeneration(string contactId)
    {
        if (string.IsNullOrWhiteSpace(contactId))
        {
            return;
        }

        if (_perContact.TryGetValue(contactId, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
                // ignored
            }
        }
    }

    public async Task<AIGenerationResult?> GenerateAsync(
        AIGenerationRequest request,
        Action<string>? onTypingChunk = null,
        CancellationToken linked = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ContactId))
        {
            throw new ArgumentException("ContactId is required", nameof(request));
        }

        // Per-contact: cancel previous generation for same contact
        CancelContactGeneration(request.ContactId);

        var localCts = CancellationTokenSource.CreateLinkedTokenSource(linked);
        _perContact[request.ContactId] = localCts;
        lock (_gate)
        {
            _cts = localCts;
        }

        LastRequest = request;
        ActiveContactId = request.ContactId;
        var token = localCts.Token;
        var acquired = false;

        try
        {
            Status = AIGenerationStatus.PreparingContext;
            await _globalGate.WaitAsync(token);
            acquired = true;

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

            Status = AIGenerationStatus.Connecting;
            var content = new System.Text.StringBuilder();
            var lastFlush = DateTime.UtcNow;
            string? flushSnapshot = null;

            Status = AIGenerationStatus.Streaming;
            await foreach (var evt in _aiService.GenerateStreamAsync(aiRequest, token))
            {
                if (!string.IsNullOrEmpty(evt.DeltaContent))
                {
                    content.Append(evt.DeltaContent);
                }

                var now = DateTime.UtcNow;
                if (onTypingChunk is not null &&
                    ((now - lastFlush).TotalMilliseconds >= UiBatchMs || evt.IsDone))
                {
                    flushSnapshot = content.ToString();
                    onTypingChunk(flushSnapshot);
                    lastFlush = now;
                }

                if (evt.IsDone)
                {
                    break;
                }
            }

            if (token.IsCancellationRequested)
            {
                Status = AIGenerationStatus.Cancelled;
                return null;
            }

            var full = content.ToString();
            if (string.IsNullOrEmpty(full))
            {
                Status = AIGenerationStatus.Failed;
                return null;
            }

            if (onTypingChunk is not null && flushSnapshot != full)
            {
                onTypingChunk(full);
            }

            Status = AIGenerationStatus.Completed;
            return new AIGenerationResult
            {
                GenerationId = request.GenerationId,
                AccountId = request.AccountId,
                ContactId = request.ContactId,
                Content = full,
                DraftRevisionAtStart = request.DraftRevisionAtStart,
                ContextSummary = buildResult.SummaryText,
                Status = AIGenerationStatus.Completed
            };
        }
        catch (OperationCanceledException)
        {
            Status = AIGenerationStatus.Cancelled;
            _logger.LogDebug("AI generation cancelled for {ContactId}", request.ContactId);
            return null;
        }
        catch (AIServiceException ex)
        {
            Status = AIGenerationStatus.Failed;
            _logger.LogError(ex, "AI generation failed for {ContactId}: {Kind}", request.ContactId, ex.Kind);
            throw;
        }
        catch (Exception ex)
        {
            Status = AIGenerationStatus.Failed;
            _logger.LogError(ex, "AI generation failed for {ContactId}", request.ContactId);
            throw;
        }
        finally
        {
            if (acquired)
            {
                _globalGate.Release();
            }

            _perContact.TryRemove(request.ContactId, out _);
            lock (_gate)
            {
                if (ReferenceEquals(_cts, localCts))
                {
                    _cts = null;
                }
            }

            if (ActiveContactId == request.ContactId)
            {
                ActiveContactId = null;
            }

            localCts.Dispose();
            if (Status is AIGenerationStatus.PreparingContext or AIGenerationStatus.Connecting or AIGenerationStatus.Streaming)
            {
                Status = AIGenerationStatus.Idle;
            }
        }
    }
}
