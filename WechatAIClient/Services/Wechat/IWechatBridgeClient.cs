using WechatAIClient.Models;

namespace WechatAIClient.Services.Wechat;

public enum BridgeMessageKind
{
    Text,
    Image,
    File,
    Emoji,
    Video,
    Voice,
    System
}

public sealed record BridgeContact(
    string Id,
    string DisplayName,
    bool IsGroup,
    string? AvatarHint,
    string? LastMessage,
    DateTime? LastMessageTime,
    int MemberCount = 0);

public sealed record BridgeMessage(
    string Id,
    string ConversationId,
    string Content,
    bool IsFromMe,
    bool IsGroup,
    string? SenderId,
    string? SenderDisplayName,
    DateTime Timestamp,
    BridgeMessageKind Kind = BridgeMessageKind.Text,
    bool MentionsMe = false,
    bool QuotesMe = false,
    string? ReplyToMessageId = null,
    string? LocalPath = null,
    string? FileName = null,
    string? FileSize = null);

public sealed class BridgeMessageEvent
{
    public required BridgeMessage Message { get; init; }
}

public sealed class OutgoingAcknowledgedEvent
{
    public string AccountId { get; init; } = string.Empty;
    public required string ClientRequestId { get; init; }
    public required string RealMessageId { get; init; }
    public required string ConversationId { get; init; }
    public required bool IsFromAi { get; init; }
    public required BridgeMessage Message { get; init; }
}

public interface IWechatBridgeClient : IAsyncDisposable
{
    WechatConnectionState State { get; }
    event EventHandler<WechatConnectionState>? StateChanged;
    event EventHandler<BridgeMessageEvent>? MessageReceived;
    event EventHandler<OutgoingAcknowledgedEvent>? OutgoingAcknowledged;
    event EventHandler? BridgeCrashed;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task ReconnectAsync(CancellationToken cancellationToken = default);
    Task<WechatAccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BridgeContact>> GetContactsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BridgeContact>> GetRecentAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BridgeContact>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BridgeMessage>> GetMessagesAsync(
        string conversationId,
        int limit,
        CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendTextAsync(
        string conversationId,
        string text,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendImageAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendFileAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default);
    Task<WechatVersionInfo> DetectVersionAsync(CancellationToken cancellationToken = default);
}
