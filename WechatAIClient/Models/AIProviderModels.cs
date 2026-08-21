namespace WechatAIClient.Models;

public enum AIProviderKind
{
    Mock = 0,
    DeepSeek = 1
}

public enum AIGenerationStatus
{
    Idle = 0,
    PreparingContext = 1,
    Connecting = 2,
    Streaming = 3,
    Completed = 4,
    Cancelled = 5,
    Failed = 6
}

public enum AIErrorKind
{
    None = 0,
    InvalidApiKey = 1,
    RateLimited = 2,
    ModelUnavailable = 3,
    ProviderUnavailable = 4,
    Network = 5,
    Timeout = 6,
    Cancelled = 7,
    Unknown = 8
}

public enum AIConnectionState
{
    NotConfigured = 0,
    Configured = 1,
    Connected = 2,
    Failed = 3
}

public sealed class AIModelDescriptor
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsReasoning { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public sealed class AIProviderSettings
{
    public AIProviderKind Provider { get; set; } = AIProviderKind.Mock;
    public string ModelId { get; set; } = "deepseek-v4-flash";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public int RequestTimeoutSeconds { get; set; } = 45;
    public int MaxOutputTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public bool Streaming { get; set; } = true;
}

public sealed class AIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public sealed class AIServiceException : Exception
{
    public AIServiceException(AIErrorKind kind, string userMessage, int? httpStatus = null, Exception? inner = null)
        : base(userMessage, inner)
    {
        Kind = kind;
        UserMessage = userMessage;
        HttpStatus = httpStatus;
    }

    public AIErrorKind Kind { get; }
    public int? HttpStatus { get; }
    public string UserMessage { get; }
}

public static class DeepSeekModels
{
    public static readonly AIModelDescriptor Flash = new()
    {
        Id = "deepseek-v4-flash",
        DisplayName = "DeepSeek V4 Flash",
        SupportsStreaming = true,
        SupportsReasoning = true,
        IsAvailable = true
    };

    public static readonly AIModelDescriptor Pro = new()
    {
        Id = "deepseek-v4-pro",
        DisplayName = "DeepSeek V4 Pro",
        SupportsStreaming = true,
        SupportsReasoning = true,
        IsAvailable = true
    };

    public static IReadOnlyList<AIModelDescriptor> All { get; } = [Flash, Pro];
}
