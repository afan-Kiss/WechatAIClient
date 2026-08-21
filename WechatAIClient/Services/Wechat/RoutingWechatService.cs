using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services.Mock;

namespace WechatAIClient.Services.Wechat;

/// <summary>
/// Routes WeChat calls to Mock or MultiAccount (Real) based on settings key <c>wechat.provider</c>.
/// </summary>
public sealed class RoutingWechatService : IWechatService, IAsyncDisposable
{
    public const string ProviderSettingsKey = "wechat.provider";

    private readonly MockWechatService _mock;
    private readonly MultiAccountWechatService _real;
    private readonly ISettingsStore _settings;
    private readonly ILogger<RoutingWechatService> _logger;
    private IWechatService _active;
    private WechatProviderKind _kind;
    private EventHandler<MessageReceivedEventArgs>? _msgHandler;
    private EventHandler<WechatConnectionState>? _stateHandler;
    private EventHandler<AccountConnectionStateChangedEventArgs>? _accountStateHandler;
    private EventHandler<AccountIdentityChangedEventArgs>? _identityHandler;

    public RoutingWechatService(
        MockWechatService mock,
        MultiAccountWechatService real,
        ISettingsStore settings,
        ILogger<RoutingWechatService> logger)
    {
        _mock = mock;
        _real = real;
        _settings = settings;
        _logger = logger;
        _active = real;
        _kind = WechatProviderKind.Real;
        Attach(_active);
        _ = WarmupAsync();
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<WechatConnectionState>? ConnectionStateChanged;
    public event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
    public event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;

    public WechatConnectionState ConnectionState => _active.ConnectionState;

    public string? SelectedAccountId => _active.SelectedAccountId;

    public WechatProviderKind ActiveProvider => _kind;

    public IReadOnlyList<WechatAccountIdentity> GetAccounts() => _active.GetAccounts();

    public WechatConnectionState GetAccountConnectionState(string accountId)
        => _active.GetAccountConnectionState(accountId);

    public bool CanSend(ConversationKey key) => _active.CanSend(key);

    public async Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.SelectAccountAsync(accountId, cancellationToken);
    }

    public async Task SwitchProviderAsync(WechatProviderKind kind, CancellationToken cancellationToken = default)
    {
        await _settings.SetAsync(ProviderSettingsKey, kind.ToString(), cancellationToken);
        await ResolveAsync(force: true, cancellationToken);
    }

    public async Task<WechatConnectionState> GetConnectionStateAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetConnectionStateAsync(cancellationToken);
    }

    public async Task<WechatAccountInfo?> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetCurrentAccountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetContactsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetGroupsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetRecentChatsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        ConversationKey key,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetMessagesAsync(key, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string contactId,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.GetMessagesAsync(contactId, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword,
        ContactType? tabFilter,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.SearchAsync(keyword, tabFilter, cancellationToken);
    }

    public async Task<ChatMessage> SendMessageAsync(
        ConversationKey key,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.SendMessageAsync(
            key, content, type, fileName, fileSize, imagePath, isFromAi, cancellationToken);
    }

    public async Task<ChatMessage> SendMessageAsync(
        string contactId,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.SendMessageAsync(
            contactId, content, type, fileName, fileSize, imagePath, isFromAi, cancellationToken);
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        ConversationKey key,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.SendTextMessageAsync(key, content, isFromAi, clientRequestId, cancellationToken);
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        string contactId,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        return await _active.SendTextMessageAsync(contactId, content, isFromAi, clientRequestId, cancellationToken);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.ReconnectAsync(cancellationToken);
    }

    public async Task ReconnectAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.ReconnectAsync(accountId, cancellationToken);
    }

    public async Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.SimulateIncomingMessageAsync(key, content, cancellationToken);
    }

    public async Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.SimulateIncomingMessageAsync(contactId, content, cancellationToken);
    }

    public async Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.SimulateIncomingMessageAsync(key, content, mentionsMe, quotesMe, cancellationToken);
    }

    public async Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(force: false, cancellationToken);
        await _active.SimulateIncomingMessageAsync(contactId, content, mentionsMe, quotesMe, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Detach(_active);
        await _real.DisposeAsync();
    }

    private async Task WarmupAsync()
    {
        try
        {
            await ResolveAsync(force: true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WeChat provider warmup failed");
        }
    }

    private async Task ResolveAsync(bool force, CancellationToken cancellationToken)
    {
        var raw = await _settings.GetAsync(ProviderSettingsKey, cancellationToken);
        var kind = ParseProvider(raw);
        if (!force && kind == _kind)
        {
            return;
        }

        var next = kind == WechatProviderKind.Mock ? (IWechatService)_mock : _real;
        if (!ReferenceEquals(next, _active))
        {
            Detach(_active);
            _active = next;
            _kind = kind;
            Attach(_active);
            ConnectionStateChanged?.Invoke(this, _active.ConnectionState);
        }
        else
        {
            _kind = kind;
        }

        if (_active is MultiAccountWechatService multi)
        {
            await multi.EnsureStartedAsync(cancellationToken);
        }
    }

    private void Attach(IWechatService service)
    {
        _msgHandler = (_, e) => MessageReceived?.Invoke(this, e);
        _stateHandler = (_, s) => ConnectionStateChanged?.Invoke(this, s);
        _accountStateHandler = (_, e) => AccountConnectionStateChanged?.Invoke(this, e);
        _identityHandler = (_, e) => AccountIdentityChanged?.Invoke(this, e);
        service.MessageReceived += _msgHandler;
        service.ConnectionStateChanged += _stateHandler;
        service.AccountConnectionStateChanged += _accountStateHandler;
        service.AccountIdentityChanged += _identityHandler;
    }

    private void Detach(IWechatService service)
    {
        if (_msgHandler is not null)
        {
            service.MessageReceived -= _msgHandler;
        }

        if (_stateHandler is not null)
        {
            service.ConnectionStateChanged -= _stateHandler;
        }

        if (_accountStateHandler is not null)
        {
            service.AccountConnectionStateChanged -= _accountStateHandler;
        }

        if (_identityHandler is not null)
        {
            service.AccountIdentityChanged -= _identityHandler;
        }
    }

    private static WechatProviderKind ParseProvider(string? raw)
    {
        if (string.Equals(raw, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            return WechatProviderKind.Mock;
        }

        return WechatProviderKind.Real;
    }
}
