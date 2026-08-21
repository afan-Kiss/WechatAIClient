namespace WechatAIClient.Models;

public enum WechatConnectionState
{
    Disconnected,
    WechatNotRunning,
    WaitingForLogin,
    Connecting,
    Connected,
    VersionUnsupported,
    BridgeError
}

public enum WechatProviderKind
{
    Mock,
    Real
}

public enum MessageSendStatus
{
    None,
    Pending,
    Sent,
    Failed
}

public sealed record WechatAccountInfo(
    string UserId,
    string DisplayName,
    string? AvatarPath);

public sealed record SendMessageResult(
    bool Success,
    string? MessageId,
    string ClientRequestId,
    DateTime Timestamp,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record WechatVersionInfo(
    string ProductVersion,
    string FilePath,
    bool IsSupported,
    string? SupportHint);
