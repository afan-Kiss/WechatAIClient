using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IWechatService
{
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    event EventHandler<WechatConnectionState>? ConnectionStateChanged;
    event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
    event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;

    WechatConnectionState ConnectionState { get; }

    /// <summary>null = aggregate all accounts view.</summary>
    string? SelectedAccountId { get; }

    IReadOnlyList<WechatAccountIdentity> GetAccounts();

    /// <summary>Per-account connection state (not the aggregate).</summary>
    WechatConnectionState GetAccountConnectionState(string accountId);

    /// <summary>Whether the target conversation's owning account can send.</summary>
    bool CanSend(ConversationKey key);

    Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default);

    Task<WechatConnectionState> GetConnectionStateAsync(CancellationToken cancellationToken = default);

    Task<WechatAccountInfo?> GetCurrentAccountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(ConversationKey key, CancellationToken cancellationToken = default);

    /// <summary>Legacy helper — uses SelectedAccountId or first account.</summary>
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string contactId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword,
        ContactType? tabFilter,
        CancellationToken cancellationToken = default);

    Task<ChatMessage> SendMessageAsync(
        ConversationKey key,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
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
        ConversationKey key,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendTextMessageAsync(
        string contactId,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default);

    Task ReconnectAsync(CancellationToken cancellationToken = default);
    Task ReconnectAsync(string accountId, CancellationToken cancellationToken = default);

    Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        CancellationToken cancellationToken = default);

    Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default);

    Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default);

    Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default);
}
