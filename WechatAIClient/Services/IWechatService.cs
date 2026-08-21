using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IWechatService
{
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;

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

    Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default);
}
