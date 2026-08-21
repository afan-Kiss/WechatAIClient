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
    event EventHandler? ProfilesChanged;

    Task LoadProfilesAsync(CancellationToken cancellationToken = default);
    Task SaveProfilesAsync(CancellationToken cancellationToken = default);
    Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default);
    Task StartAllAsync(CancellationToken cancellationToken = default);
    Task StartSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default);
    Task StopSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default);
    Task ReconnectAsync(string? accountId = null, CancellationToken cancellationToken = default);

    WechatAccountSession? GetSession(string accountId);
    WechatConnectionState GetAccountConnectionState(string accountId);
    IReadOnlyList<WechatAccountIdentity> GetIdentities();
    WechatConnectionState GetAggregateState();

    Task<WechatAccountConnectionProfile> AddProfileAsync(
        WechatAccountConnectionProfile profile,
        CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(
        WechatAccountConnectionProfile profile,
        CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default);
    Task SetProfileEnabledAsync(string profileId, bool enabled, CancellationToken cancellationToken = default);
    void ValidatePortsOrThrow(WechatAccountConnectionProfile candidate, string? excludeProfileId = null);
}

public sealed class WechatAccountManager : IWechatAccountManager
{
    public const string ProfilesSettingsKey = "wechat.account.profiles";
    public const string SelectedAccountSettingsKey = "wechat.selectedAccountId";

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
    private readonly Dictionary<string, WechatAccountSession> _sessionsByProfileId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _accountIdToProfileId = new(StringComparer.Ordinal);
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
                return _sessionsByProfileId.Values.ToList();
            }
        }
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<AccountConnectionStateChangedEventArgs>? AccountConnectionStateChanged;
    public event EventHandler<AccountIdentityChangedEventArgs>? AccountIdentityChanged;
    public event EventHandler<WechatConnectionState>? AggregateConnectionStateChanged;
    public event EventHandler? ProfilesChanged;

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

        var selectedRaw = await _settings.GetAsync(SelectedAccountSettingsKey, cancellationToken);

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

            if (string.IsNullOrWhiteSpace(selectedRaw) ||
                selectedRaw.Equals("__all__", StringComparison.Ordinal))
            {
                _selectedAccountId = null;
            }
            else
            {
                _selectedAccountId = selectedRaw;
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
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SelectAccountAsync(string? accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? normalized;
        lock (_gate)
        {
            _selectedAccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId;
            normalized = _selectedAccountId;
        }

        await _settings.SetAsync(
            SelectedAccountSettingsKey,
            normalized ?? "__all__",
            cancellationToken);
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
            try
            {
                ValidatePortsOrThrow(profile, profile.ProfileId);
                await EnsureSessionStartedAsync(profile, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start profile {ProfileId}; continuing others", profile.ProfileId);
            }
        }

        EnsureSelectedAccountStillValid();
        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    public async Task StartSessionAsync(string profileOrAccountId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var profile = FindProfile(profileOrAccountId)
                      ?? throw new InvalidOperationException("Unknown profile/account: " + profileOrAccountId);
        ValidatePortsOrThrow(profile, profile.ProfileId);
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
                try
                {
                    await session.ReconnectAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconnect failed for {AccountId}", session.AccountId);
                }
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

    public WechatConnectionState GetAccountConnectionState(string accountId)
    {
        var session = GetSession(accountId);
        return session?.State ?? WechatConnectionState.Disconnected;
    }

    public IReadOnlyList<WechatAccountIdentity> GetIdentities()
    {
        lock (_gate)
        {
            var list = new List<WechatAccountIdentity>();
            foreach (var session in _sessionsByProfileId.Values)
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

    public async Task<WechatAccountConnectionProfile> AddProfileAsync(
        WechatAccountConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(profile);
        ValidatePortsOrThrow(profile);

        lock (_gate)
        {
            if (_profiles.Any(p => string.Equals(p.ProfileId, profile.ProfileId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Profile already exists: " + profile.ProfileId);
            }

            _profiles.Add(profile);
        }

        await SaveProfilesAsync(cancellationToken);
        if (profile.Enabled)
        {
            try
            {
                await EnsureSessionStartedAsync(profile, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start newly added profile {ProfileId}", profile.ProfileId);
            }
        }

        return profile;
    }

    public async Task UpdateProfileAsync(
        WechatAccountConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(profile);
        ValidatePortsOrThrow(profile, profile.ProfileId);

        WechatAccountSession? oldSession = null;
        WechatAccountConnectionProfile? previous;
        lock (_gate)
        {
            var idx = _profiles.FindIndex(p =>
                string.Equals(p.ProfileId, profile.ProfileId, StringComparison.Ordinal));
            if (idx < 0)
            {
                throw new InvalidOperationException("Unknown profile: " + profile.ProfileId);
            }

            previous = _profiles[idx];
            _profiles[idx] = profile;
            _sessionsByProfileId.TryGetValue(profile.ProfileId, out oldSession);
        }

        await SaveProfilesAsync(cancellationToken);

        var needsRebuild = previous is null ||
                           !string.Equals(previous.BaseUrl, profile.BaseUrl, StringComparison.OrdinalIgnoreCase) ||
                           previous.HttpCallbackPort != profile.HttpCallbackPort ||
                           previous.TcpCallbackPort != profile.TcpCallbackPort;

        if (oldSession is not null && needsRebuild)
        {
            await DisposeSessionAsync(oldSession);
            if (profile.Enabled)
            {
                await EnsureSessionStartedAsync(profile, cancellationToken);
            }
        }
        else if (oldSession is not null && !profile.Enabled)
        {
            await oldSession.StopAsync(cancellationToken);
        }
        else if (oldSession is null && profile.Enabled)
        {
            await EnsureSessionStartedAsync(profile, cancellationToken);
        }

        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    public async Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WechatAccountSession? session;
        lock (_gate)
        {
            var idx = _profiles.FindIndex(p =>
                string.Equals(p.ProfileId, profileId, StringComparison.Ordinal));
            if (idx < 0)
            {
                return;
            }

            _profiles.RemoveAt(idx);
            _sessionsByProfileId.TryGetValue(profileId, out session);
        }

        if (session is not null)
        {
            await DisposeSessionAsync(session);
        }

        await SaveProfilesAsync(cancellationToken);
        EnsureSelectedAccountStillValid();
        AggregateConnectionStateChanged?.Invoke(this, GetAggregateState());
    }

    public async Task SetProfileEnabledAsync(
        string profileId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        WechatAccountConnectionProfile? profile;
        lock (_gate)
        {
            profile = _profiles.FirstOrDefault(p =>
                string.Equals(p.ProfileId, profileId, StringComparison.Ordinal));
        }

        if (profile is null)
        {
            throw new InvalidOperationException("Unknown profile: " + profileId);
        }

        var updated = profile with { Enabled = enabled };
        await UpdateProfileAsync(updated, cancellationToken);
    }

    public void ValidatePortsOrThrow(WechatAccountConnectionProfile candidate, string? excludeProfileId = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_gate)
        {
            foreach (var other in _profiles)
            {
                if (!string.IsNullOrWhiteSpace(excludeProfileId) &&
                    string.Equals(other.ProfileId, excludeProfileId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!other.Enabled && !candidate.Enabled)
                {
                    continue;
                }

                if (other.HttpCallbackPort == candidate.HttpCallbackPort)
                {
                    throw new InvalidOperationException(
                        $"HTTP callback 端口冲突：{candidate.HttpCallbackPort} 已被 {other.DisplayName} 使用");
                }

                if (other.TcpCallbackPort == candidate.TcpCallbackPort)
                {
                    throw new InvalidOperationException(
                        $"TCP callback 端口冲突：{candidate.TcpCallbackPort} 已被 {other.DisplayName} 使用");
                }
            }
        }
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
            sessions = _sessionsByProfileId.Values.ToList();
            _sessionsByProfileId.Clear();
            _accountIdToProfileId.Clear();
        }

        foreach (var session in sessions.Distinct())
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
            if (!_sessionsByProfileId.TryGetValue(profile.ProfileId, out session!))
            {
                session = new WechatAccountSession(
                    profile,
                    _httpClientFactory,
                    _loggerFactory,
                    _parser,
                    _mediaCache);
                _sessionsByProfileId[profile.ProfileId] = session;
                if (!string.IsNullOrWhiteSpace(session.AccountId))
                {
                    _accountIdToProfileId[session.AccountId] = profile.ProfileId;
                }

                Wire(session);
            }
        }

        await session.StartAsync(cancellationToken);
    }

    private async Task DisposeSessionAsync(WechatAccountSession session)
    {
        Unwire(session);
        try
        {
            await session.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stop before dispose failed for {ProfileId}", session.Profile.ProfileId);
        }

        lock (_gate)
        {
            _sessionsByProfileId.Remove(session.Profile.ProfileId);
            RemoveAccountAliasesLocked(session.Profile.ProfileId);
        }

        await session.DisposeAsync();
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
                if (!string.IsNullOrWhiteSpace(e.OldAccountId))
                {
                    if (_accountIdToProfileId.TryGetValue(e.OldAccountId, out var mapped) &&
                        string.Equals(mapped, session.Profile.ProfileId, StringComparison.Ordinal))
                    {
                        _accountIdToProfileId.Remove(e.OldAccountId);
                    }
                }

                if (!string.IsNullOrWhiteSpace(e.NewAccountId))
                {
                    _accountIdToProfileId[e.NewAccountId] = session.Profile.ProfileId;
                }

                if (!string.IsNullOrWhiteSpace(e.OldAccountId) &&
                    string.Equals(_selectedAccountId, e.OldAccountId, StringComparison.Ordinal))
                {
                    _selectedAccountId = e.NewAccountId;
                }
            }

            if (!string.IsNullOrWhiteSpace(e.OldAccountId) &&
                !string.IsNullOrWhiteSpace(e.NewAccountId) &&
                !string.Equals(e.OldAccountId, e.NewAccountId, StringComparison.Ordinal))
            {
                _ = PersistSelectedAccountAsync();
            }
        }

        AccountIdentityChanged?.Invoke(this, e);
    }

    private async Task PersistSelectedAccountAsync()
    {
        try
        {
            string? selected;
            lock (_gate)
            {
                selected = _selectedAccountId;
            }

            await _settings.SetAsync(SelectedAccountSettingsKey, selected ?? "__all__");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist selected account after identity change");
        }
    }

    private void EnsureSelectedAccountStillValid()
    {
        lock (_gate)
        {
            if (_selectedAccountId is null)
            {
                return;
            }

            if (FindSessionLocked(_selectedAccountId) is null)
            {
                _selectedAccountId = null;
            }
        }
    }

    private void RemoveAccountAliasesLocked(string profileId)
    {
        var toRemove = _accountIdToProfileId
            .Where(kv => string.Equals(kv.Value, profileId, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in toRemove)
        {
            _accountIdToProfileId.Remove(key);
        }
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
        if (_sessionsByProfileId.TryGetValue(profileOrAccountId, out var byProfile))
        {
            return byProfile;
        }

        if (_accountIdToProfileId.TryGetValue(profileOrAccountId, out var profileId) &&
            _sessionsByProfileId.TryGetValue(profileId, out var byAlias))
        {
            return byAlias;
        }

        return null;
    }
}
