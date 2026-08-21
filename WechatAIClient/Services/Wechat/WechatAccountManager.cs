using System.Text.Json;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services.Media;
using WechatAIClient.Services.Weixin;

namespace WechatAIClient.Services.Wechat;

public interface IWechatAccountManager : IAsyncDisposable
{
    string? SelectedAccountId { get; }
    IReadOnlyList<WechatAccountConnectionProfile> Profiles { get; }
    IReadOnlyList<WechatAccountSession> Sessions { get; }

    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
    event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;
    event EventHandler<WechatConnectionState>? AggregateConnectionStateChanged;

    Task LoadProfilesAsync(CancellationToken cancellationToken = default);
    Task SaveProfilesAsync(CancellationToken cancellationToken = default);
    Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default);
    Task StartAllAsync(CancellationToken cancellationToken = default);
    Task StartSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default);
    Task StopSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default);
    Task ReconnectAsync(string? accountId = null, CancellationToken cancellationToken = default);

    WechatAccountSession? GetSession(string accountId);
    IReadOnlyList<WechatAccountIdentity> GetIdentities();
    WechatConnectionState GetAggregateState();
}

public sealed class WechatAccountManager : IWechatAccountManager
{
    public const string ProfilesSettingsKey = "wechat.account.profiles";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly ISettingsStore _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IWechatCallbackParser _parser;
    private readonly IMediaCacheService _mediaCache;
    private readonly ILogger<WechatAccountManager> _logger;
    private readonly object _gate = new();
    private readonly List<WechatAccountConnectionProfile> _profiles = [];
    private readonly Dictionary<string, WechatAccountSession> _sessions = new(StringComparer.Ordinal);
    private string? _selectedAccountId;
    private bool _disposed;

    public WechatAccountManager(
        ISettingsStore settings,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IWechatCallbackParser parser,
        IMediaCacheService mediaCache)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _parser = parser;
        _mediaCache = mediaCache;
        _logger = loggerFactory.CreateLogger<WechatAccountManager>();
    }

    public string? SelectedAccountId
    {
        get
        {
            lock (_gate)
            {
                return _selectedAccountId;
            }
        }
    }

    public IReadOnlyList<WechatAccountConnectionProfile> Profiles
    {
        get
        {
            lock (_gate)
            {
                return _profiles.ToList();
            }
        }
    }

    public IReadOnlyList<WechatAccountSession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Values.ToList();
            }
        }
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
    public event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;
    public event EventHandler<WechatConnectionState>? AggregateConnectionStateChanged;

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var raw = await _settings.GetAsync(ProfilesSettingsKey, cancellationToken);
        List<WechatAccountConnectionProfile>? loaded = null;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                loaded = JsonSerializer.Deserialize<List<WechatAccountConnectionProfile>>(raw, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize account profiles");
            }
        }

        lock (_gate)
        {
            _profiles.Clear();
            if (loaded is { Count: > 0 })
            {
                _profiles.AddRange(loaded.Where(p => p is not null));
            }

            if (_profiles.Count == 0)
            {
                _profiles.Add(new WechatAccountConnectionProfile(
                    "default",
                    "微信主账号",
                    "http://127.0.0.1:19088",
                    5000,
                    61108,
                    null,
                    true));
            }
        }
    }

    public async Task SaveProfilesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string json;
        lock (_gate)
        {
            json = JsonSerializer.Serialize(_profiles, JsonOptions);
        }

        await _settings.SetAsync(ProfilesSettingsKey, json, cancellationToken);
    }

    public Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _selectedAccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId;
        }

        return Task.CompletedTask;
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await LoadProfilesAsync(cancellationToken);

        List<WechatAccountConnectionProfile> enabled;
        lock (_gate)
        {
            enabled = _profiles.Where(p => p.Enabled).ToList();
        }

        foreach (var profile in enabled)
        {
            await EnsureSessionStartedAsync(profile, cancellationToken);
        }

        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    public async Task StartSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var profile = FindProfile(profileOrAccountId)
                      ?? throw new InvalidOperationException("Unknown profile/account: " + profileOrAccountId);
        await EnsureSessionStartedAsync(profile, cancellationToken);
    }

    public async Task StopSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WechatAccountSession? session;
        lock (_gate)
        {
            session = FindSessionLocked(profileOrAccountId);
        }

        if (session is null)
        {
            return;
        }

        await session.StopAsync(cancellationToken);
        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    public async Task ReconnectAsync(string? accountId = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            foreach (var session in Sessions)
            {
                await session.ReconnectAsync(cancellationToken);
            }
        }
        else
        {
            var session = GetSession(accountId) ?? throw new InvalidOperationException("Unknown account: " + accountId);
            await session.ReconnectAsync(cancellationToken);
        }

        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    public WechatAccountSession? GetSession(string accountId)
    {
        lock (_gate)
        {
            return FindSessionLocked(accountId);
        }
    }

    public IReadOnlyList<WechatAccountIdentity> GetIdentities()
    {
        lock (_gate)
        {
            var list = new List<WechatAccountIdentity>();
            foreach (var session in _sessions.Values)
            {
                if (session.Identity is { } id)
                {
                    list.Add(id);
                }
                else
                {
                    list.Add(new WechatAccountIdentity(
                        session.AccountId,
                        session.AccountId,
                        session.Profile.DisplayName));
                }
            }

            return list;
        }
    }

    public WechatConnectionState GetAggregateState()
    {
        var sessions = Sessions;
        if (sessions.Count == 0)
        {
            return WechatConnectionState.Disconnected;
        }

        if (sessions.Any(s => s.State == WechatConnectionState.Connected))
        {
            return WechatConnectionState.Connected;
        }

        if (sessions.Any(s => s.State == WechatConnectionState.Degraded))
        {
            return WechatConnectionState.Degraded;
        }

        if (sessions.Any(s => s.State == WechatConnectionState.Connecting))
        {
            return WechatConnectionState.Connecting;
        }

        if (sessions.Any(s => s.State == WechatConnectionState.WaitingForLogin))
        {
            return WechatConnectionState.WaitingForLogin;
        }

        if (sessions.Any(s => s.State == WechatConnectionState.WechatNotRunning))
        {
            return WechatConnectionState.WechatNotRunning;
        }

        if (sessions.Any(s => s.State == WechatConnectionState.BridgeError))
        {
            return WechatConnectionState.BridgeError;
        }

        return sessions[0].State;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<WechatAccountSession> sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToList();
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            Unwire(session);
            await session.DisposeAsync();
        }
    }

    private async Task EnsureSessionStartedAsync(
        WechatAccountConnectionProfile profile,
        CancellationToken cancellationToken)
    {
        WechatAccountSession session;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(profile.ProfileId, out session!))
            {
                session = new WechatAccountSession(
                    profile,
                    _httpClientFactory,
                    _loggerFactory,
                    _parser,
                    _mediaCache);
                _sessions[profile.ProfileId] = session;
                Wire(session);
            }
        }

        await session.StartAsync(cancellationToken);
    }

    private void Wire(WechatAccountSession session)
    {
        session.MessageReceived += OnSessionMessage;
        session.StateChanged += OnSessionState;
        session.IdentityChanged += OnSessionIdentity;
    }

    private void Unwire(WechatAccountSession session)
    {
        session.MessageReceived -= OnSessionMessage;
        session.StateChanged -= OnSessionState;
        session.IdentityChanged -= OnSessionIdentity;
    }

    private void OnSessionMessage(object? sender, MessageReceivedEventArgs e)
        => MessageReceived?.Invoke(this, e);

    private void OnSessionState(object? sender, WechatConnectionState state)
    {
        if (sender is not WechatAccountSession session)
        {
            return;
        }

        AccountConnectionStateChanged?.Invoke(this, new AccountConnectionStateChangedEventArgs
        {
            AccountId = session.AccountId,
            State = state
        });
        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    private void OnSessionIdentity(object? sender, AccountIdentityChangedEventArgs e)
    {
        if (sender is WechatAccountSession session)
        {
            lock (_gate)
            {
                // Keep profile-key lookup; also index by live AccountId for routing.
                _sessions[session.AccountId] = session;
            }
        }

        AccountIdentityChanged?.Invoke(this, e);
    }

    private WechatAccountConnectionProfile? FindProfile(string profileOrAccountId)
    {
        lock (_gate)
        {
            return _profiles.FirstOrDefault(p =>
                string.Equals(p.ProfileId, profileOrAccountId, StringComparison.Ordinal) ||
                string.Equals(p.ExpectedAccountWxid, profileOrAccountId, StringComparison.Ordinal));
        }
    }

    private WechatAccountSession? FindSessionLocked(string profileOrAccountId)
    {
        if (_sessions.TryGetValue(profileOrAccountId, out var byKey))
        {
            return byKey;
        }

        return _sessions.Values.FirstOrDefault(s =>
            string.Equals(s.AccountId, profileOrAccountId, StringComparison.Ordinal) ||
            string.Equals(s.Profile.ProfileId, profileOrAccountId, StringComparison.Ordinal));
    }
}
