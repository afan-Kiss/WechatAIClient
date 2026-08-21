using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.Wechat;

/// <summary>Real WeChat provider aggregating multiple <see cref="WechatAccountSession"/> instances.</summary>
public sealed class MultiAccountWechatService : IWechatService, IAsyncDisposable
{
    private readonly IWechatAccountManager _manager;
    private readonly ILogger<MultiAccountWechatService> _logger;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private bool _started;
    private bool _disposed;

    public MultiAccountWechatService(
        IWechatAccountManager manager,
        ILogger<MultiAccountWechatService> logger)
    {
        _manager = manager;
        _logger = logger;
        _manager.MessageReceived += (_, e) => MessageReceived?.Invoke(this, e);
        _manager.AggregateConnectionStateChanged += (_, s) => ConnectionStateChanged?.Invoke(this, s);
        _manager.AccountConnectionStateChanged += (_, e) => AccountConnectionStateChanged?.Invoke(this, e);
        _manager.AccountIdentityChanged += (_, e) => AccountIdentityChanged?.Invoke(this, e);
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<WechatConnectionState>? ConnectionStateChanged;
    public event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
    public event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;

    public WechatConnectionState ConnectionState => _manager.GetAggregateState();

    public string? SelectedAccountId => _manager.SelectedAccountId;

    public IReadOnlyList<WechatAccountIdentity> GetAccounts() => _manager.GetIdentities();

    public WechatConnectionState GetAccountConnectionState(string accountId)
        => _manager.GetAccountConnectionState(accountId);

    public bool CanSend(ConversationKey key) => CanManualSend(key);

    public bool CanManualSend(ConversationKey key)
    {
        var state = GetAccountConnectionState(key.AccountId);
        return state is WechatConnectionState.Connected or WechatConnectionState.Degraded;
    }

    public bool CanAutoReply(ConversationKey key)
    {
        var state = GetAccountConnectionState(key.AccountId);
        return state == WechatConnectionState.Connected;
    }

    public Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default)
        => _manager.SelectAccountAsync(accountId, cancellationToken);

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            await _manager.StartAllAsync(cancellationToken);
            _started = true;
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task<WechatConnectionState> GetConnectionStateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        return _manager.GetAggregateState();
    }

    public async Task<WechatAccountInfo?> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var selected = SelectedAccountId;
        if (string.IsNullOrWhiteSpace(selected))
        {
            return null;
        }

        var session = _manager.GetSession(selected);
        if (session is null)
        {
            return null;
        }

        var account = await session.GetAccountAsync(cancellationToken);
        if (account is not null)
        {
            return account;
        }

        var id = session.Identity;
        return id is null
            ? new WechatAccountInfo(session.AccountId, session.Profile.DisplayName, null)
            : new WechatAccountInfo(id.AccountId, id.DisplayName, id.AvatarUrl);
    }

    public async Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        return await AggregateContactsAsync(s => s.GetContactsAsync(cancellationToken), ContactType.Friend);
    }

    public async Task<IReadOnlyList<Contact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        return await AggregateContactsAsync(s => s.GetGroupsAsync(cancellationToken), ContactType.Group);
    }

    public async Task<IReadOnlyList<Contact>> GetRecentChatsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var sessions = ActiveSessions().ToList();
        var tasks = sessions.Select(async session =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                return await session.GetRecentAsync(timeout.Token);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetRecent failed for {AccountId}", session.AccountId);
                return (IReadOnlyList<Contact>)Array.Empty<Contact>();
            }
        });
        var chunks = await Task.WhenAll(tasks);
        return chunks.SelectMany(c => c).OrderByDescending(c => c.LastMessageTime).ToList();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        ConversationKey key,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var session = RequireSession(key.AccountId);
        return await session.GetMessagesAsync(key.ConversationId, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string contactId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var session = ResolveSessionForContact(contactId);
        return await session.GetMessagesAsync(contactId, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword,
        ContactType? tabFilter,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var contacts = new List<Contact>();
        if (tabFilter is null or ContactType.Friend)
        {
            contacts.AddRange(await GetContactsAsync(cancellationToken));
        }

        if (tabFilter is null or ContactType.Group)
        {
            contacts.AddRange(await GetGroupsAsync(cancellationToken));
        }

        var byKey = new Dictionary<string, Contact>(StringComparer.Ordinal);
        foreach (var c in contacts)
        {
            byKey[c.Key.StableKey] = c;
        }

        contacts = byKey.Values.ToList();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return contacts
                .OrderByDescending(c => c.HasLastActivity)
                .ThenByDescending(c => c.LastMessageTime)
                .Select(c => new SearchHit
                {
                    Contact = c,
                    MatchSummary = c.LastMessage,
                    HitKind = c.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
                })
                .ToList();
        }

        var hits = new List<SearchHit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contact in contacts)
        {
            if (contact.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(new SearchHit
                {
                    Contact = contact,
                    MatchSummary = contact.Name,
                    HitKind = contact.Type == ContactType.Group ? SearchHitKind.Group : SearchHitKind.Contact
                });
                seen.Add(contact.Key.StableKey);
            }
        }

        foreach (var contact in contacts)
        {
            var session = _manager.GetSession(contact.AccountId);
            if (session is null)
            {
                continue;
            }

            var messages = session.SnapshotMessages(contact.Id);
            var match = messages.LastOrDefault(m =>
                m.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (match is null || !seen.Add(contact.Key.StableKey))
            {
                continue;
            }

            hits.Add(new SearchHit
            {
                Contact = contact,
                MatchSummary = match.Content,
                HitKind = SearchHitKind.Message
            });
        }

        foreach (var contact in contacts)
        {
            if (seen.Contains(contact.Key.StableKey))
            {
                continue;
            }

            if (contact.LastMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(new SearchHit
                {
                    Contact = contact,
                    MatchSummary = contact.LastMessage,
                    HitKind = SearchHitKind.Message
                });
            }
        }

        return hits;
    }

    public Task<ChatMessage> SendMessageAsync(
        ConversationKey key,
        string content,
        MessageType type = MessageType.Text,
        string? fileName = null,
        string? fileSize = null,
        string? imagePath = null,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        var session = RequireSession(key.AccountId);
        return session.SendMessageAsync(
            key.ConversationId, content, type, fileName, fileSize, imagePath, isFromAi, cancellationToken);
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
        await EnsureStartedAsync(cancellationToken);
        var session = ResolveSessionForContact(contactId);
        return await session.SendMessageAsync(
            contactId, content, type, fileName, fileSize, imagePath, isFromAi, cancellationToken);
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        ConversationKey key,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var session = RequireSession(key.AccountId);
        return await session.SendTextMessageAsync(
            key.ConversationId, content, isFromAi, clientRequestId, cancellationToken);
    }

    public async Task<SendMessageResult> SendTextMessageAsync(
        string contactId,
        string content,
        bool isFromAi = false,
        string? clientRequestId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var session = ResolveSessionForContact(contactId);
        return await session.SendTextMessageAsync(
            contactId, content, isFromAi, clientRequestId, cancellationToken);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        await _manager.ReconnectAsync(null, cancellationToken);
    }

    public async Task ReconnectAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        await _manager.ReconnectAsync(accountId, cancellationToken);
    }

    public Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(key, content, false, false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        CancellationToken cancellationToken = default)
        => SimulateIncomingMessageAsync(contactId, content, false, false, cancellationToken);

    public Task SimulateIncomingMessageAsync(
        ConversationKey key,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("SimulateIncomingMessageAsync is only supported by MockWechatService.");
    }

    public Task SimulateIncomingMessageAsync(
        string contactId,
        string content,
        bool mentionsMe,
        bool quotesMe,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("SimulateIncomingMessageAsync is only supported by MockWechatService.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _manager.DisposeAsync();
        _startGate.Dispose();
    }

    private async Task<IReadOnlyList<Contact>> AggregateContactsAsync(
        Func<WechatAccountSession, Task<IReadOnlyList<Contact>>> loader,
        ContactType expectedType)
    {
        var sessions = ActiveSessions().ToList();
        var tasks = sessions.Select(async session =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                var list = await loader(session);
                return list.Where(c => c.Type == expectedType).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Contact load failed for {AccountId}", session.AccountId);
                return new List<Contact>();
            }
        });
        var chunks = await Task.WhenAll(tasks);
        return chunks.SelectMany(c => c).ToList();
    }

    private IEnumerable<WechatAccountSession> ActiveSessions()
    {
        var selected = SelectedAccountId;
        var sessions = _manager.Sessions;
        if (string.IsNullOrWhiteSpace(selected))
        {
            return sessions;
        }

        return sessions.Where(s => string.Equals(s.AccountId, selected, StringComparison.Ordinal));
    }

    private WechatAccountSession RequireSession(string accountId)
    {
        var session = _manager.GetSession(accountId);
        if (session is null)
        {
            throw new InvalidOperationException("No WeChat session for account: " + accountId);
        }

        return session;
    }

    /// <summary>
    /// Legacy contactId routing: SelectedAccountId when set; otherwise unique owning session;
    /// throws <see cref="AmbiguousConversationException"/> when multiple sessions own the contact.
    /// </summary>
    private WechatAccountSession ResolveSessionForContact(string contactId)
    {
        var selected = SelectedAccountId;
        if (!string.IsNullOrWhiteSpace(selected))
        {
            var selectedSession = _manager.GetSession(selected);
            if (selectedSession is not null)
            {
                return selectedSession;
            }
        }

        var owners = _manager.Sessions
            .Where(s => s.TryGetContact(contactId, out _))
            .ToList();

        if (owners.Count == 1)
        {
            return owners[0];
        }

        if (owners.Count > 1)
        {
            throw new AmbiguousConversationException(
                contactId,
                owners.Select(o => o.AccountId).Distinct(StringComparer.Ordinal).ToList());
        }

        var sessions = _manager.Sessions;
        if (sessions.Count == 1)
        {
            return sessions[0];
        }

        if (sessions.Count == 0)
        {
            throw new InvalidOperationException("No WeChat sessions available");
        }

        throw new AmbiguousConversationException(
            contactId,
            sessions.Select(s => s.AccountId).Distinct(StringComparer.Ordinal).ToList());
    }
}
