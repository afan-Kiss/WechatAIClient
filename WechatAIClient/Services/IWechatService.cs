using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IWechatService
{
    Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string contactId, CancellationToken cancellationToken = default);
    Task<ChatMessage> SendMessageAsync(string contactId, string content, MessageType type = MessageType.Text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> SearchAsync(string keyword, CancellationToken cancellationToken = default);
}
