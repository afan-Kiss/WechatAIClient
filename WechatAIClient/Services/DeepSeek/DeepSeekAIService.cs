using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.DeepSeek;

public sealed class DeepSeekAIService : IAIService
{
    public const string HttpClientName = "DeepSeek";
    public const string ApiKeySecretName = "deepseek.apiKey";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretStore _secretStore;
    private readonly IAISettingsService _settings;
    private readonly ILogger<DeepSeekAIService> _logger;

    public DeepSeekAIService(
        IHttpClientFactory httpClientFactory,
        ISecretStore secretStore,
        IAISettingsService settings,
        ILogger<DeepSeekAIService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _secretStore = secretStore;
        _settings = settings;
        _logger = logger;
    }

    public string ModelName { get; private set; } = "deepseek-v4-flash";

    public bool IsConnected { get; private set; }

    public AIProviderKind ProviderKind => AIProviderKind.DeepSeek;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var key = await _secretStore.GetSecretAsync(ApiKeySecretName, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            IsConnected = false;
            throw new AIServiceException(AIErrorKind.InvalidApiKey, "请先配置 API Key");
        }

        var result = await TestConnectionAsync(cancellationToken);
        IsConnected = result.Success;
        if (!result.Success)
        {
            throw new AIServiceException(result.ErrorKind, result.Message);
        }
    }

    public async Task<AIResponse> GenerateAsync(AIRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();
        AIUsage? usage = null;
        string? requestId = null;
        string? model = null;

        await foreach (var evt in GenerateStreamAsync(request, cancellationToken))
        {
            if (!string.IsNullOrEmpty(evt.DeltaContent))
            {
                sb.Append(evt.DeltaContent);
            }

            if (evt.Usage is not null)
            {
                usage = evt.Usage;
            }

            if (!string.IsNullOrEmpty(evt.RequestId))
            {
                requestId = evt.RequestId;
            }
        }

        sw.Stop();
        var provider = await _settings.GetProviderSettingsAsync(cancellationToken);
        model = provider.ModelId;
        ModelName = model;

        return new AIResponse
        {
            Content = sb.ToString(),
            GenerationId = request.GenerationId,
            ContactId = request.ContactId,
            RequestId = requestId,
            Model = model,
            Duration = sw.Elapsed,
            Usage = usage,
            Status = AIGenerationStatus.Completed
        };
    }

    public async IAsyncEnumerable<AIStreamEvent> GenerateStreamAsync(
        AIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = await _settings.GetProviderSettingsAsync(cancellationToken);
        ModelName = string.IsNullOrWhiteSpace(provider.ModelId) ? "deepseek-v4-flash" : provider.ModelId;

        var apiKey = await _secretStore.GetSecretAsync(ApiKeySecretName, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AIServiceException(AIErrorKind.InvalidApiKey, "请先配置 API Key");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(provider.RequestTimeoutSeconds <= 0 ? 45 : provider.RequestTimeoutSeconds, 5, 300);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var token = timeoutCts.Token;

        var body = BuildRequestBody(request, provider, stream: true);
        var baseUrl = string.IsNullOrWhiteSpace(provider.BaseUrl)
            ? "https://api.deepseek.com"
            : provider.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/chat/completions";

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        try
        {
            try
            {
                response = await client.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AIServiceException(AIErrorKind.Timeout, "请求超时，请稍后重试");
            }
            catch (OperationCanceledException)
            {
                throw new AIServiceException(AIErrorKind.Cancelled, "已取消生成", inner: null);
            }
            catch (HttpRequestException ex)
            {
                throw new AIServiceException(AIErrorKind.Network, "网络异常，无法连接 DeepSeek", inner: ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var (kind, message) = MapHttpError(response.StatusCode);
                _logger.LogWarning(
                    "DeepSeek HTTP {Status} model={Model} generation={GenerationId}",
                    status,
                    ModelName,
                    request.GenerationId);
                throw new AIServiceException(kind, message, status);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var parser = new DeepSeekSseParser();
            string? requestId = null;
            AIUsage? usage = null;
            var messageCount = request.Messages?.Count ?? 0;

            while (!reader.EndOfStream && !parser.IsDone)
            {
                token.ThrowIfCancellationRequested();
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new AIServiceException(AIErrorKind.Timeout, "请求超时，请稍后重试");
                }

                if (line is null)
                {
                    break;
                }

                // Re-add newline so parser line-splitting stays consistent when feeding full lines.
                foreach (var evt in parser.Feed(line + "\n"))
                {
                    if (!string.IsNullOrEmpty(evt.RequestId))
                    {
                        requestId = evt.RequestId;
                    }

                    if (evt.Usage is not null)
                    {
                        usage = evt.Usage;
                    }

                    request.OnStreamEvent?.Invoke(evt);
                    yield return evt;

                    if (evt.IsDone)
                    {
                        sw.Stop();
                        _logger.LogInformation(
                            "DeepSeek stream done RequestId={RequestId} Model={Model} Msgs={Count} DurationMs={Ms}",
                            requestId,
                            ModelName,
                            messageCount,
                            sw.ElapsedMilliseconds);
                        yield break;
                    }
                }
            }

            if (!parser.IsDone)
            {
                var done = new AIStreamEvent(null, IsDone: true, Usage: usage, RequestId: requestId);
                request.OnStreamEvent?.Invoke(done);
                yield return done;
            }

            sw.Stop();
            _logger.LogInformation(
                "DeepSeek stream finished RequestId={RequestId} Model={Model} Msgs={Count} DurationMs={Ms}",
                requestId,
                ModelName,
                messageCount,
                sw.ElapsedMilliseconds);
        }
        finally
        {
            response?.Dispose();
        }
    }

    public async Task<AIConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var provider = await _settings.GetProviderSettingsAsync(cancellationToken);
            ModelName = string.IsNullOrWhiteSpace(provider.ModelId) ? "deepseek-v4-flash" : provider.ModelId;
            var apiKey = await _secretStore.GetSecretAsync(ApiKeySecretName, cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new AIConnectionTestResult(false, "请先配置 API Key", 0, AIErrorKind.InvalidApiKey);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(provider.RequestTimeoutSeconds, 5, 60)));

            var baseUrl = string.IsNullOrWhiteSpace(provider.BaseUrl)
                ? "https://api.deepseek.com"
                : provider.BaseUrl.TrimEnd('/');
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var probe = new
            {
                model = ModelName,
                stream = false,
                max_tokens = 8,
                temperature = 0,
                thinking = new { type = "disabled" },
                messages = new[]
                {
                    new { role = "user", content = "ping" }
                }
            };
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(probe, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(httpRequest, timeoutCts.Token);
            sw.Stop();
            if (response.IsSuccessStatusCode)
            {
                IsConnected = true;
                return new AIConnectionTestResult(true, "连接成功", (int)sw.ElapsedMilliseconds, AIErrorKind.None);
            }

            IsConnected = false;
            var (kind, message) = MapHttpError(response.StatusCode);
            return new AIConnectionTestResult(false, message, (int)sw.ElapsedMilliseconds, kind);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            IsConnected = false;
            return new AIConnectionTestResult(false, "连接超时", (int)sw.ElapsedMilliseconds, AIErrorKind.Timeout);
        }
        catch (OperationCanceledException)
        {
            IsConnected = false;
            return new AIConnectionTestResult(false, "已取消", (int)sw.ElapsedMilliseconds, AIErrorKind.Cancelled);
        }
        catch (HttpRequestException)
        {
            IsConnected = false;
            return new AIConnectionTestResult(false, "网络异常，无法连接 DeepSeek", (int)sw.ElapsedMilliseconds, AIErrorKind.Network);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            _logger.LogWarning(ex, "DeepSeek connection test failed");
            return new AIConnectionTestResult(false, "连接失败", (int)sw.ElapsedMilliseconds, AIErrorKind.Unknown);
        }
    }

    private static string BuildRequestBody(AIRequest request, AIProviderSettings provider, bool stream)
    {
        var messages = new List<object>();
        foreach (var msg in request.Messages ?? Array.Empty<AIContextMessage>())
        {
            if (string.IsNullOrWhiteSpace(msg.Content))
            {
                continue;
            }

            var role = msg.Role?.Trim().ToLowerInvariant() switch
            {
                "system" => "system",
                "assistant" => "assistant",
                _ => "user"
            };
            messages.Add(new { role, content = msg.Content });
        }

        if (messages.Count == 0)
        {
            messages.Add(new { role = "user", content = "请根据上下文生成一条简短回复。" });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = string.IsNullOrWhiteSpace(provider.ModelId) ? "deepseek-v4-flash" : provider.ModelId,
            ["messages"] = messages,
            ["stream"] = stream,
            ["temperature"] = provider.Temperature,
            ["max_tokens"] = provider.MaxOutputTokens > 0 ? provider.MaxOutputTokens : 2048,
            ["thinking"] = new { type = "disabled" }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    internal static (AIErrorKind Kind, string Message) MapHttpError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            => (AIErrorKind.InvalidApiKey, "API Key 无效或权限不足"),
        (HttpStatusCode)429
            => (AIErrorKind.RateLimited, "请求过于频繁，请稍后再试"),
        HttpStatusCode.NotFound
            => (AIErrorKind.ModelUnavailable, "模型不可用，请更换模型"),
        HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
            => (AIErrorKind.ProviderUnavailable, "DeepSeek 服务暂时不可用"),
        HttpStatusCode.RequestTimeout
            => (AIErrorKind.Timeout, "请求超时，请稍后重试"),
        _ when (int)status >= 500
            => (AIErrorKind.ProviderUnavailable, "DeepSeek 服务暂时不可用"),
        _ => (AIErrorKind.Unknown, $"请求失败（HTTP {(int)status}）")
    };
}
