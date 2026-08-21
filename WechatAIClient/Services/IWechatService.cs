using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IWechatService
{
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    event EventHandler<WechatConnectionState>? ConnectionStateChanged;

    WechatConnectionState ConnectionState { get; }

    Task<WechatConnectionState> GetConnectionStateAsync(CancellationToken cancellationToken = default);
    Task<WechatAccountInfo?> GetCurrentAccountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string contactId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword,
        ContactType? tabFilter,
        CancellationToken cancellationToken = default);

    Task<ChatMessage> SendMessageAsync(
        string contactId,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendTextMessageAsync(
        string contactId,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default);

    Task ReconnectAsync(CancellationToken cancellationToken = default);

    Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default);

    Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default);
}
