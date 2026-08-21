using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Helpers;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.DeepSeek;
using WechatAIClient.Services.Wechat;

namespace WechatAIClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IWechatService _wechatService;
    private readonly IWechatAccountManager _accountManager;
    private readonly IAISettingsService _aiSettings;
    private readonly ISecretStore _secrets;
    private readonly IAIService _aiService;
    private readonly IToastService _toast;
    private readonly ISettingsStore _settingsStore;
    private readonly IConversationAiCandidateStore _aiCandidates;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _autoDebounce = new(StringComparer.Ordinal);
    private CancellationTokenSource? _selectionCts;
    private int _selectionVersion;
    private EventHandler<MessageReceivedEventArgs>? _messageReceivedHandler;
    private EventHandler<WechatConnectionState>? _connectionStateHandler;
    private EventHandler<AccountConnectionStateChangedEventArgs>? _accountConnectionHandler;
    private EventHandler<AccountIdentityChangedEventArgs>? _accountIdentityHandler;
    private EventHandler? _profilesChangedHandler;
    private EventHandler? _toastChangedHandler;
    private EventHandler<Contact>? _contactSelectedHandler;
    private EventHandler? _requestAiAssistHandler;
    private EventHandler<Contact>? _contactPreviewHandler;
    private EventHandler<ConversationKey>? _messageSentHandler;

    public MainWindowViewModel(
        ContactListViewModel contactList,
        ChatViewModel chat,
        AIPanelViewModel aiPanel,
        IThemeService themeService,
        IWechatService wechatService,
        IWechatAccountManager accountManager,
        IAISettingsService aiSettings,
        ISecretStore secrets,
        IAIService aiService,
        IToastService toast,
        ISettingsStore settingsStore,
        IConversationAiCandidateStore aiCandidates,
        ILogger<MainWindowViewModel> logger)
    {
        ContactList = contactList;
        Chat = chat;
        AiPanel = aiPanel;
        _themeService = themeService;
        _wechatService = wechatService;
        _accountManager = accountManager;
        _aiSettings = aiSettings;
        _secrets = secrets;
        _aiService = aiService;
        _toast = toast;
        _settingsStore = settingsStore;
        _aiCandidates = aiCandidates;
        _logger = logger;
        SelectedThemeMode = themeService.CurrentMode;
        RefreshWechatStatus(_wechatService.ConnectionState);

        _contactSelectedHandler = (_, contact) =>
        {
            Chat.LoadContactAsync(contact).SafeFireAndForget(_logger);
            AiPanel.BindContactAsync(contact.Id, contact.AccountId).SafeFireAndForget(_logger);
            RestoreAiCandidate(contact.Key);
        };
        ContactList.ContactSelected += _contactSelectedHandler;

        _requestAiAssistHandler = (_, _) =>
            GenerateAiReplyAsync().SafeFireAndForget(_logger);
        Chat.RequestAiAssist += _requestAiAssistHandler;

        _contactPreviewHandler = (_, contact) => ContactList.BumpRecent(contact);
        Chat.ContactPreviewUpdated += _contactPreviewHandler;

        _messageSentHandler = (_, key) =>
        {
            var contact = ContactList.FindContact(key.AccountId, key.ConversationId);
            if (contact is not null)
            {
                ContactList.BumpRecent(contact);
            }
        };
        Chat.MessageSent += _messageSentHandler;

        _messageReceivedHandler = OnMessageReceived;
        _wechatService.MessageReceived += _messageReceivedHandler;

        _connectionStateHandler = (_, state) =>
            Dispatcher.UIThread.Post(() =>
            {
                RefreshWechatStatus(state);
                Chat.NotifyConnectionStateChanged();
                RefreshAccountMenu();
                RefreshAccountProfileCards();
            });
        _wechatService.ConnectionStateChanged += _connectionStateHandler;

        _accountConnectionHandler = (_, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                RefreshAccountMenu();
                RefreshAccountProfileCards();
                Chat.NotifyConnectionStateChanged();
            });
        _wechatService.AccountConnectionStateChanged += _accountConnectionHandler;

        _accountIdentityHandler = (_, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                RefreshAccountMenu();
                RefreshAccountProfileCards();
            });
        _wechatService.AccountIdentityChanged += _accountIdentityHandler;

        _profilesChangedHandler = (_, _) =>
            Dispatcher.UIThread.Post(RefreshAccountProfileCards);
        _accountManager.ProfilesChanged += _profilesChangedHandler;

        _toastChangedHandler = (_, _) =>
        {
            ToastMessage = _toast.Message;
            IsToastVisible = _toast.IsVisible;
        };
        _toast.Changed += _toastChangedHandler;
        ToastMessage = _toast.Message;
        IsToastVisible = _toast.IsVisible;
    }

    public ContactListViewModel ContactList { get; }
    public ChatViewModel Chat { get; }
    public AIPanelViewModel AiPanel { get; }

    public IReadOnlyList<AIModelDescriptor> AvailableAiModels { get; } = DeepSeekModels.All;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
    [NotifyPropertyChangedFor(nameof(IsLightTheme))]
    [NotifyPropertyChangedFor(nameof(IsSystemTheme))]
    private AppThemeMode _selectedThemeMode;

    public bool IsDarkTheme => SelectedThemeMode == AppThemeMode.Dark;
    public bool IsLightTheme => SelectedThemeMode == AppThemeMode.Light;
    public bool IsSystemTheme => SelectedThemeMode == AppThemeMode.System;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChatNav))]
    [NotifyPropertyChangedFor(nameof(IsContactsNav))]
    [NotifyPropertyChangedFor(nameof(IsFavoritesNav))]
    [NotifyPropertyChangedFor(nameof(IsSettingsNav))]
    [NotifyPropertyChangedFor(nameof(IsFavoritesVisible))]
    private int _navIndex;

    public bool IsChatNav => NavIndex == 0;
    public bool IsContactsNav => NavIndex == 1;
    public bool IsFavoritesNav => NavIndex == 2;
    public bool IsSettingsNav => NavIndex == 3;
    public bool IsFavoritesVisible => NavIndex == 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiPanelToggleText))]
    private bool _isAiPanelVisible = true;

    public string AiPanelToggleText => IsAiPanelVisible ? "收起 AI 助手" : "展开 AI 助手";

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProviderMock))]
    [NotifyPropertyChangedFor(nameof(IsProviderDeepSeek))]
    private AIProviderKind _settingsAiProvider = AIProviderKind.Mock;

    public bool IsProviderMock => SettingsAiProvider == AIProviderKind.Mock;
    public bool IsProviderDeepSeek => SettingsAiProvider == AIProviderKind.DeepSeek;

    [ObservableProperty]
    private string _settingsAiModelId = "deepseek-v4-flash";

    [ObservableProperty]
    private string _settingsApiKey = string.Empty;

    [ObservableProperty]
    private bool _settingsShowApiKey;

    [ObservableProperty]
    private string _settingsBaseUrl = "https://api.deepseek.com";

    [ObservableProperty]
    private int _settingsTimeoutSeconds = 45;

    [ObservableProperty]
    private int _settingsMaxTokens = 2048;

    [ObservableProperty]
    private double _settingsTemperature = 0.7;

    [ObservableProperty]
    private bool _settingsStreaming = true;

    [ObservableProperty]
    private bool _isAiAdvancedExpanded;

    [ObservableProperty]
    private string _settingsConnectionMessage = string.Empty;

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWechatProviderMock))]
    [NotifyPropertyChangedFor(nameof(IsWechatProviderReal))]
    private WechatProviderKind _settingsWechatProvider = WechatProviderKind.Real;

    public bool IsWechatProviderMock => SettingsWechatProvider == WechatProviderKind.Mock;
    public bool IsWechatProviderReal => SettingsWechatProvider == WechatProviderKind.Real;

    [ObservableProperty]
    private string _wechatStatusText = "○ 未连接";

    [ObservableProperty]
    private bool _isWechatConnected;

    [ObservableProperty]
    private bool _isWechatWarning;

    [ObservableProperty]
    private string _accountSwitcherText = "全部账号";

    [ObservableProperty]
    private bool _isAccountMenuOpen;

    public System.Collections.ObjectModel.ObservableCollection<AccountMenuItem> AccountMenuItems { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<AccountProfileCardItem> AccountProfileCards { get; } = [];

    [ObservableProperty]
    private bool _isEditingAccountProfile;

    [ObservableProperty]
    private string? _editingProfileId;

    [ObservableProperty]
    private string _editProfileDisplayName = string.Empty;

    [ObservableProperty]
    private string _editProfileBaseUrl = "http://127.0.0.1:19088";

    [ObservableProperty]
    private int _editProfileHttpPort = 5000;

    [ObservableProperty]
    private int _editProfileTcpPort = 61108;

    public async Task InitializeAsync()
    {
        try
        {
            await _themeService.RestoreAsync();
            SelectedThemeMode = _themeService.CurrentMode;
            await LoadAiProviderSettingsAsync();
            await LoadWechatProviderSettingsAsync();
            await _accountManager.LoadProfilesAsync();
            RefreshWechatStatus(await _wechatService.GetConnectionStateAsync());
            Chat.NotifyConnectionStateChanged();
            RefreshAccountMenu();
            RefreshAccountProfileCards();
            await ContactList.InitializeAsync();
            await AiPanel.InitializeAsync();
            if (ContactList.SelectedContact is { } selected)
            {
                await Chat.LoadContactAsync(selected);
                await AiPanel.BindContactAsync(selected.Id, selected.AccountId);
                RestoreAiCandidate(selected.Key);
            }
            else
            {
                Chat.ClearConversation();
                await AiPanel.BindContactAsync(string.Empty, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main window");
        }
    }

    private void RefreshAccountMenu()
    {
        AccountMenuItems.Clear();
        var accounts = _wechatService.GetAccounts();
        var connected = accounts.Count(a =>
            _wechatService.GetAccountConnectionState(a.AccountId) == WechatConnectionState.Connected);

        AccountMenuItems.Add(new AccountMenuItem
        {
            AccountId = null,
            Title = "全部账号",
            Subtitle = accounts.Count == 0 ? null : $"{connected}/{accounts.Count} 已连接",
            IsSelected = _wechatService.SelectedAccountId is null
        });
        foreach (var account in accounts)
        {
            var state = _wechatService.GetAccountConnectionState(account.AccountId);
            AccountMenuItems.Add(new AccountMenuItem
            {
                AccountId = account.AccountId,
                Title = account.DisplayName,
                Subtitle = string.IsNullOrWhiteSpace(account.Wxid)
                    ? FormatConnectionState(state)
                    : $"{account.Wxid} · {FormatConnectionState(state)}",
                IsSelected = string.Equals(_wechatService.SelectedAccountId, account.AccountId, StringComparison.Ordinal)
            });
        }

        AccountSwitcherText = AccountMenuItems.FirstOrDefault(a => a.IsSelected)?.Title ?? "全部账号";
    }

    private void RefreshAccountProfileCards()
    {
        AccountProfileCards.Clear();
        foreach (var profile in _accountManager.Profiles)
        {
            var session = _accountManager.GetSession(profile.ProfileId)
                          ?? (!string.IsNullOrWhiteSpace(profile.ExpectedAccountWxid)
                              ? _accountManager.GetSession(profile.ExpectedAccountWxid)
                              : null);
            var identity = session?.Identity;
            var accountId = identity?.AccountId ?? profile.ExpectedAccountWxid ?? profile.ProfileId;
            var state = _accountManager.GetAccountConnectionState(accountId);
            var portHint = ExtractApiPort(profile.BaseUrl);
            AccountProfileCards.Add(new AccountProfileCardItem
            {
                ProfileId = profile.ProfileId,
                Title = profile.DisplayName,
                PortsText = $"{portHint} / {profile.HttpCallbackPort} / {profile.TcpCallbackPort}",
                StatusText = profile.Enabled
                    ? FormatConnectionState(state)
                    : "已禁用",
                Wxid = identity?.Wxid ?? profile.ExpectedAccountWxid,
                IsEnabled = profile.Enabled,
                CanDelete = !string.Equals(profile.ProfileId, "default", StringComparison.Ordinal)
            });
        }
    }

    private static string ExtractApiPort(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && !uri.IsDefaultPort)
        {
            return uri.Port.ToString();
        }

        return "19088";
    }

    private static string FormatConnectionState(WechatConnectionState state) => state switch
    {
        WechatConnectionState.Connected => "● 已连接",
        WechatConnectionState.Degraded => "⚠ 降级",
        WechatConnectionState.Connecting => "◐ 连接中",
        WechatConnectionState.WaitingForLogin => "○ 未登录",
        WechatConnectionState.WechatNotRunning => "○ Hook 离线",
        WechatConnectionState.VersionUnsupported => "⚠ 版本不兼容",
        WechatConnectionState.BridgeError => "⚠ 异常",
        _ => "○ 未连接"
    };

    private void RestoreAiCandidate(ConversationKey key)
    {
        if (_aiCandidates.TryGet(key, out var content))
        {
            AiPanel.LatestGeneratedReply = content;
        }
    }

    [RelayCommand]
    private void ToggleAccountMenu() => IsAccountMenuOpen = !IsAccountMenuOpen;

    [RelayCommand]
    private async Task SelectAccountMenuItemAsync(AccountMenuItem? item)
    {
        if (item is null)
        {
            return;
        }

        IsAccountMenuOpen = false;
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
        var cts = new CancellationTokenSource();
        _selectionCts = cts;
        var version = Interlocked.Increment(ref _selectionVersion);

        try
        {
            await _wechatService.SelectAccountAsync(item.AccountId, cts.Token);
            if (version != _selectionVersion || cts.IsCancellationRequested)
            {
                return;
            }

            RefreshAccountMenu();
            await ContactList.InitializeAsync(cts.Token);
            if (version != _selectionVersion || cts.IsCancellationRequested)
            {
                return;
            }

            if (ContactList.SelectedContact is { } selected)
            {
                await Chat.LoadContactAsync(selected);
                await AiPanel.BindContactAsync(selected.Id, selected.AccountId);
                RestoreAiCandidate(selected.Key);
            }
            else
            {
                Chat.ClearConversation();
                await AiPanel.BindContactAsync(string.Empty, null);
            }
        }
        catch (OperationCanceledException)
        {
            // superseded selection
        }
    }

    public void Cleanup()
    {
        if (_contactSelectedHandler is not null)
        {
            ContactList.ContactSelected -= _contactSelectedHandler;
            _contactSelectedHandler = null;
        }

        if (_requestAiAssistHandler is not null)
        {
            Chat.RequestAiAssist -= _requestAiAssistHandler;
            _requestAiAssistHandler = null;
        }

        if (_contactPreviewHandler is not null)
        {
            Chat.ContactPreviewUpdated -= _contactPreviewHandler;
            _contactPreviewHandler = null;
        }

        if (_messageSentHandler is not null)
        {
            Chat.MessageSent -= _messageSentHandler;
            _messageSentHandler = null;
        }

        if (_messageReceivedHandler is not null)
        {
            _wechatService.MessageReceived -= _messageReceivedHandler;
            _messageReceivedHandler = null;
        }

        if (_connectionStateHandler is not null)
        {
            _wechatService.ConnectionStateChanged -= _connectionStateHandler;
            _connectionStateHandler = null;
        }

        if (_accountConnectionHandler is not null)
        {
            _wechatService.AccountConnectionStateChanged -= _accountConnectionHandler;
            _accountConnectionHandler = null;
        }

        if (_accountIdentityHandler is not null)
        {
            _wechatService.AccountIdentityChanged -= _accountIdentityHandler;
            _accountIdentityHandler = null;
        }

        if (_profilesChangedHandler is not null)
        {
            _accountManager.ProfilesChanged -= _profilesChangedHandler;
            _profilesChangedHandler = null;
        }

        if (_toastChangedHandler is not null)
        {
            _toast.Changed -= _toastChangedHandler;
            _toastChangedHandler = null;
        }

        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
        _selectionCts = null;

        foreach (var cts in _autoDebounce.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _autoDebounce.Clear();
        Chat.Cleanup();
        ContactList.Cleanup();
        AiPanel.CancelGenerationCommand.Execute(null);
        // App owns IWechatService Dispose — do not dispose here.
    }

    private void RefreshWechatStatus(WechatConnectionState state)
    {
        IsWechatConnected = state == WechatConnectionState.Connected;
        IsWechatWarning = state is WechatConnectionState.Degraded
            or WechatConnectionState.VersionUnsupported
            or WechatConnectionState.BridgeError;
        WechatStatusText = state switch
        {
            WechatConnectionState.Connected => "● 微信已连接",
            WechatConnectionState.Degraded => "⚠ Hook 已连接，消息回调不可用",
            WechatConnectionState.WechatNotRunning => "○ Hook API 未连接",
            WechatConnectionState.WaitingForLogin => "○ 微信未登录",
            WechatConnectionState.Connecting => "◐ 初始化中…",
            WechatConnectionState.VersionUnsupported => "⚠ 版本暂不兼容",
            WechatConnectionState.BridgeError => "⚠ 微信连接异常",
            _ => "○ 微信未连接"
        };
    }

    private async Task LoadWechatProviderSettingsAsync()
    {
        try
        {
            var raw = await _settingsStore.GetAsync(RoutingWechatService.ProviderSettingsKey);
            SettingsWechatProvider = string.Equals(raw, "Mock", StringComparison.OrdinalIgnoreCase)
                ? WechatProviderKind.Mock
                : WechatProviderKind.Real;
            if (_wechatService is RoutingWechatService routing)
            {
                await routing.SwitchProviderAsync(SettingsWechatProvider);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load WeChat provider settings");
        }
    }

    private bool EnsureWechatConnectedForSend(ConversationKey key)
    {
        if (_wechatService.CanSend(key))
        {
            return true;
        }

        var state = _wechatService.GetAccountConnectionState(key.AccountId);
        _ = _toast.ShowAsync(FormatConnectionState(state));
        return false;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
        {
            NavIndex = 3;
            LoadAiProviderSettingsAsync().SafeFireAndForget(_logger);
        }
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
        if (NavIndex == 3)
        {
            NavIndex = 0;
        }
    }

    [RelayCommand]
    private void SetTheme(string mode)
    {
        SelectedThemeMode = mode switch
        {
            "Light" => AppThemeMode.Light,
            "System" => AppThemeMode.System,
            _ => AppThemeMode.Dark
        };
        _themeService.SetTheme(SelectedThemeMode);
    }

    [RelayCommand]
    private void SelectNav(string indexText)
    {
        if (!int.TryParse(indexText, out var index))
        {
            return;
        }

        NavIndex = index;
        IsSettingsOpen = index == 3;
        if (IsSettingsOpen)
        {
            LoadAiProviderSettingsAsync().SafeFireAndForget(_logger);
        }

        if (index == 1)
        {
            ContactList.SelectedTabIndex = 1;
        }
        else if (index == 0)
        {
            ContactList.SelectedTabIndex = 0;
        }
    }

    [RelayCommand]
    private void ToggleAiPanel() => IsAiPanelVisible = !IsAiPanelVisible;

    [RelayCommand]
    private void CollapseAiPanel() => IsAiPanelVisible = false;

    [RelayCommand]
    private void SetAiProvider(string provider)
    {
        SettingsAiProvider = string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)
            ? AIProviderKind.DeepSeek
            : AIProviderKind.Mock;
    }

    [RelayCommand]
    private async Task SetWechatProviderAsync(string provider)
    {
        SettingsWechatProvider = string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase)
            ? WechatProviderKind.Mock
            : WechatProviderKind.Real;
        try
        {
            if (_wechatService is RoutingWechatService routing)
            {
                await routing.SwitchProviderAsync(SettingsWechatProvider);
            }
            else
            {
                await _settingsStore.SetAsync(
                    RoutingWechatService.ProviderSettingsKey,
                    SettingsWechatProvider.ToString());
            }

            RefreshWechatStatus(await _wechatService.GetConnectionStateAsync());
            Chat.NotifyConnectionStateChanged();
            Chat.ClearConversation();
            await ContactList.InitializeAsync();
            if (ContactList.SelectedContact is { } selected)
            {
                await Chat.LoadContactAsync(selected);
                await AiPanel.BindContactAsync(selected.Id, selected.AccountId);
                RestoreAiCandidate(selected.Key);
            }
            else
            {
                await AiPanel.BindContactAsync(string.Empty, null);
            }

            RefreshAccountMenu();
            RefreshAccountProfileCards();
            await _toast.ShowAsync(SettingsWechatProvider == WechatProviderKind.Mock
                ? "已切换到 Mock 微信"
                : "已切换到真实微信接入");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch wechat provider");
            await _toast.ShowAsync("切换失败");
        }
    }

    [RelayCommand]
    private async Task ReconnectWechatAsync()
    {
        try
        {
            WechatStatusText = "○ 正在重连…";
            await _wechatService.ReconnectAsync();
            RefreshWechatStatus(await _wechatService.GetConnectionStateAsync());
            Chat.NotifyConnectionStateChanged();
            await _toast.ShowAsync(IsWechatConnected ? "微信已连接" : WechatStatusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WeChat reconnect failed");
            await _toast.ShowAsync("重连失败");
        }
    }

    [RelayCommand]
    private void ToggleAiAdvanced() => IsAiAdvancedExpanded = !IsAiAdvancedExpanded;

    [RelayCommand]
    private void ToggleShowApiKey() => SettingsShowApiKey = !SettingsShowApiKey;

    [RelayCommand]
    private async Task SaveAiProviderSettingsAsync()
    {
        try
        {
            await _aiSettings.SaveProviderSettingsAsync(CaptureProviderSettings());
            if (!string.IsNullOrWhiteSpace(SettingsApiKey))
            {
                await _secrets.SetSecretAsync(DeepSeekAIService.ApiKeySecretName, SettingsApiKey.Trim());
            }

            await AiPanel.RefreshConnectionStateAsync();
            await _toast.ShowAsync("AI 设置已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI provider settings");
            await _toast.ShowAsync("保存失败");
        }
    }

    [RelayCommand]
    private async Task ClearApiKeyAsync()
    {
        try
        {
            await _secrets.DeleteSecretAsync(DeepSeekAIService.ApiKeySecretName);
            SettingsApiKey = string.Empty;
            await AiPanel.RefreshConnectionStateAsync(connectedHint: false);
            SettingsConnectionMessage = "API Key 已清除";
            await _toast.ShowAsync("API Key 已清除");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear API key");
            await _toast.ShowAsync("清除失败");
        }
    }

    [RelayCommand]
    private async Task TestAiConnectionAsync()
    {
        if (IsTestingConnection)
        {
            return;
        }

        IsTestingConnection = true;
        SettingsConnectionMessage = "测试中…";
        try
        {
            await _aiSettings.SaveProviderSettingsAsync(CaptureProviderSettings());
            if (SettingsAiProvider == AIProviderKind.DeepSeek && !string.IsNullOrWhiteSpace(SettingsApiKey))
            {
                await _secrets.SetSecretAsync(DeepSeekAIService.ApiKeySecretName, SettingsApiKey.Trim());
            }

            var result = await _aiService.TestConnectionAsync();
            SettingsConnectionMessage = result.Success
                ? $"{result.Message}（{result.LatencyMs} ms）"
                : result.Message;
            await AiPanel.RefreshConnectionStateAsync(connectedHint: result.Success);
            await _toast.ShowAsync(result.Success ? "连接成功" : result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connection test failed");
            SettingsConnectionMessage = "连接失败";
            await AiPanel.RefreshConnectionStateAsync(connectedHint: false);
            await _toast.ShowAsync("连接失败");
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task NotifyPhase2Async(string? feature)
    {
        var name = string.IsNullOrWhiteSpace(feature) ? "该功能" : feature;
        await _toast.ShowAsync($"{name}将在后续版本提供");
    }

    [RelayCommand]
    private async Task OpenAiContextPreviewAsync()
    {
        if (Chat.CurrentContact is null)
        {
            await _toast.ShowAsync("请先选择会话");
            return;
        }

        var contact = Chat.CurrentContact;
        await AiPanel.RefreshContextPreviewAsync(
            Chat.Messages.ToList(),
            contact.Id,
            contact.Name,
            contact.Type == ContactType.Group,
            contact.AccountId);
        AiPanel.IsContextPreviewOpen = true;
    }

    [RelayCommand]
    private void CloseAiContextPreview() => AiPanel.IsContextPreviewOpen = false;

    [RelayCommand]
    private async Task GenerateAiReplyAsync()
    {
        if (Chat.CurrentContact is null)
        {
            return;
        }

        if (AiPanel.ReplyMode == AIReplyMode.Off)
        {
            await _toast.ShowAsync("AI 已关闭");
            return;
        }

        var contact = Chat.CurrentContact;
        var draftRevision = Chat.DraftRevision;
        var key = contact.Key;
        var accountId = string.IsNullOrWhiteSpace(contact.AccountId)
            ? (_wechatService.SelectedAccountId ?? SqliteStore.LegacyAccountId)
            : contact.AccountId;
        var pins = await _aiSettings.GetPinnedIdsAsync(accountId, contact.Id);
        var request = new AIGenerationRequest
        {
            AccountId = accountId,
            ContactId = contact.Id,
            ContactName = contact.Name,
            ContextSnapshot = Chat.Messages.ToList(),
            ContextLength = AiPanel.ContextLength,
            ReplyMode = AiPanel.ReplyMode,
            IncludeOwnMessages = AiPanel.IncludeOwnMessages,
            ReplyStyle = AiPanel.ReplyStyle,
            ReplyLength = AiPanel.ReplyLength,
            TemporaryInstruction = string.IsNullOrWhiteSpace(AiPanel.TemporaryInstruction)
                ? null
                : AiPanel.TemporaryInstruction,
            PinnedMessageIds = pins,
            DraftRevisionAtStart = draftRevision,
            IsGroup = contact.Type == ContactType.Group,
            TemporarilyExcludedMessageIds = AiPanel.TemporarilyExcludedMessageIds.Count > 0
                ? new HashSet<string>(AiPanel.TemporarilyExcludedMessageIds, StringComparer.Ordinal)
                : null
        };

        var result = await AiPanel.GenerateForContactDetailedAsync(request);
        if (result is null || result.Status != AIGenerationStatus.Completed)
        {
            return;
        }

        if (AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            if (!EnsureWechatConnectedForSend(key))
            {
                _aiCandidates.Set(key, result.Content);
                AiPanel.LatestGeneratedReply = result.Content;
                return;
            }

            await Chat.SendAsync(accountId, contact.Id, result.Content, isFromAi: true);
            _aiCandidates.Clear(key);
        }
        else if (AiPanel.ReplyMode == AIReplyMode.ManualConfirm)
        {
            await ApplyManualConfirmResultAsync(key, result.Content, draftRevision);
        }
    }

    private AIProviderSettings CaptureProviderSettings() => new()
    {
        Provider = SettingsAiProvider,
        ModelId = string.IsNullOrWhiteSpace(SettingsAiModelId) ? "deepseek-v4-flash" : SettingsAiModelId.Trim(),
        BaseUrl = string.IsNullOrWhiteSpace(SettingsBaseUrl) ? "https://api.deepseek.com" : SettingsBaseUrl.Trim(),
        RequestTimeoutSeconds = SettingsTimeoutSeconds,
        MaxOutputTokens = SettingsMaxTokens,
        Temperature = SettingsTemperature,
        Streaming = SettingsStreaming
    };

    private async Task LoadAiProviderSettingsAsync()
    {
        try
        {
            var settings = await _aiSettings.GetProviderSettingsAsync();
            SettingsAiProvider = settings.Provider;
            SettingsAiModelId = settings.ModelId;
            SettingsBaseUrl = settings.BaseUrl;
            SettingsTimeoutSeconds = settings.RequestTimeoutSeconds;
            SettingsMaxTokens = settings.MaxOutputTokens;
            SettingsTemperature = settings.Temperature;
            SettingsStreaming = settings.Streaming;

            var key = await _secrets.GetSecretAsync(DeepSeekAIService.ApiKeySecretName);
            SettingsApiKey = key ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load AI provider settings");
        }
    }

    private async Task ApplyManualConfirmResultAsync(
        ConversationKey key,
        string reply,
        int draftRevisionAtStart)
    {
        _aiCandidates.Set(key, reply);

        if (Chat.CurrentContact?.Key != key)
        {
            return;
        }

        if (!Chat.TryApplyAiDraft(key, reply, draftRevisionAtStart))
        {
            AiPanel.LatestGeneratedReply = reply;
            await _toast.ShowAsync("已生成新候选");
            return;
        }

        AiPanel.LatestGeneratedReply = reply;
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (e.Message.IsSelf ||
            e.Message.IsFromAi ||
            e.Message.Source is MessageSource.LocalUserAI or MessageSource.LocalUserManual)
        {
            return;
        }

        if (!_wechatService.CanSend(new ConversationKey(e.AccountId, e.ContactId)) &&
            AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            // Auto must not send while this account is disconnected; Manual can still generate later.
            return;
        }

        if (AiPanel.IsAutoPaused)
        {
            return;
        }

        if (!AiPanel.AutoGenerateOnReceive)
        {
            return;
        }

        var contact = ContactList.FindContact(e.AccountId, e.ContactId) ??
                      (Chat.CurrentContact?.Id == e.ContactId &&
                       string.Equals(Chat.CurrentContact?.AccountId, e.AccountId, StringComparison.Ordinal)
                          ? Chat.CurrentContact
                          : null);

        if (contact?.Type == ContactType.Group)
        {
            if (!PassesGroupTrigger(AiPanel.GroupTriggerMode, e.Message))
            {
                return;
            }
        }

        if (AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            ScheduleAutoGenerate(e.AccountId, e.ContactId, e.Message.Id, autoSend: true);
        }
        else if (AiPanel.ReplyMode == AIReplyMode.ManualConfirm)
        {
            ScheduleAutoGenerate(e.AccountId, e.ContactId, e.Message.Id, autoSend: false);
        }
    }

    private static bool PassesGroupTrigger(GroupTriggerMode mode, ChatMessage message)
    {
        return mode switch
        {
            GroupTriggerMode.Off => false,
            GroupTriggerMode.AllMessages => true,
            GroupTriggerMode.MentionMeOnly => message.MentionsMe,
            GroupTriggerMode.QuoteMeOnly => message.QuotesMe,
            GroupTriggerMode.MentionOrQuoteMe => message.MentionsMe || message.QuotesMe,
            _ => true
        };
    }

    private void ScheduleAutoGenerate(string accountId, string contactId, string? triggerMessageId, bool autoSend)
    {
        var key = new ConversationKey(
            SqliteStore.NormalizeAccountId(accountId),
            contactId).StableKey;
        if (_autoDebounce.TryGetValue(key, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }

        var cts = new CancellationTokenSource();
        _autoDebounce[key] = cts;

        DebouncedAutoGenerateAsync(accountId, contactId, triggerMessageId, autoSend, cts.Token)
            .SafeFireAndForget(_logger);
    }

    private async Task DebouncedAutoGenerateAsync(
        string accountId,
        string contactId,
        string? triggerMessageId,
        bool autoSend,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(1200, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (AiPanel.IsAutoPaused)
            {
                return;
            }

            var resolvedAccountId = SqliteStore.NormalizeAccountId(accountId);
            var convKey = new ConversationKey(resolvedAccountId, contactId);
            var messages = await _wechatService.GetMessagesAsync(convKey, cancellationToken);
            var contact = ContactList.FindContact(resolvedAccountId, contactId);
            if (contact is null &&
                Chat.CurrentContact?.Id == contactId &&
                string.Equals(Chat.CurrentContact?.AccountId, resolvedAccountId, StringComparison.Ordinal))
            {
                contact = Chat.CurrentContact;
            }

            var contactName = contact?.Name ?? contactId;
            var isGroup = contact?.Type == ContactType.Group;
            var replyMode = autoSend ? AIReplyMode.Auto : AIReplyMode.ManualConfirm;
            var draftRevision =
                Chat.CurrentContact?.Key == convKey
                    ? Chat.DraftRevision
                    : (int?)null;
            var pins = await _aiSettings.GetPinnedIdsAsync(resolvedAccountId, contactId, cancellationToken);
            var effective = await _aiSettings.GetEffectiveAsync(resolvedAccountId, contactId, cancellationToken);

            var request = new AIGenerationRequest
            {
                AccountId = resolvedAccountId,
                ContactId = contactId,
                ContactName = contactName,
                TriggerAccountId = resolvedAccountId,
                TriggerConversationId = contactId,
                TriggerMessageId = triggerMessageId,
                ContextSnapshot = messages.ToList(),
                ContextLength = effective.ContextCount,
                ReplyMode = replyMode,
                IncludeOwnMessages = effective.IncludeOwnMessages,
                ReplyStyle = effective.ReplyStyle,
                ReplyLength = effective.ReplyLength,
                TemporaryInstruction = null,
                PinnedMessageIds = pins,
                DraftRevisionAtStart = draftRevision,
                IsGroup = isGroup
            };

            var result = await AiPanel.GenerateForContactDetailedAsync(request);
            if (result is null ||
                result.Status != AIGenerationStatus.Completed ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (autoSend)
            {
                if (!EnsureWechatConnectedForSend(convKey))
                {
                    _aiCandidates.Set(convKey, result.Content);
                    if (Chat.CurrentContact?.Key == convKey)
                    {
                        AiPanel.LatestGeneratedReply = result.Content;
                    }

                    await _toast.ShowAsync("微信未连接，回复已保留为候选");
                    return;
                }

                await Chat.SendAsync(resolvedAccountId, contactId, result.Content, isFromAi: true);
                _aiCandidates.Clear(convKey);
            }
            else if (draftRevision is int rev)
            {
                await ApplyManualConfirmResultAsync(convKey, result.Content, rev);
            }
            else
            {
                _aiCandidates.Set(convKey, result.Content);
                if (Chat.CurrentContact?.Key == convKey)
                {
                    AiPanel.LatestGeneratedReply = result.Content;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // debounce cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto AI reply failed for {AccountId}/{ContactId}", accountId, contactId);
        }
    }

    [RelayCommand]
    private void BeginAddAccountProfile()
    {
        var profiles = _accountManager.Profiles;
        var nextHttp = profiles.Count == 0 ? 5000 : profiles.Max(p => p.HttpCallbackPort) + 1;
        var nextTcp = profiles.Count == 0 ? 61108 : profiles.Max(p => p.TcpCallbackPort) + 1;
        var nextApi = 19088 + profiles.Count;
        EditingProfileId = null;
        EditProfileDisplayName = $"微信账号 {profiles.Count + 1}";
        EditProfileBaseUrl = $"http://127.0.0.1:{nextApi}";
        EditProfileHttpPort = nextHttp;
        EditProfileTcpPort = nextTcp;
        IsEditingAccountProfile = true;
    }

    [RelayCommand]
    private void BeginEditAccountProfile(AccountProfileCardItem? card)
    {
        if (card is null)
        {
            return;
        }

        var profile = _accountManager.Profiles.FirstOrDefault(p =>
            string.Equals(p.ProfileId, card.ProfileId, StringComparison.Ordinal));
        if (profile is null)
        {
            return;
        }

        EditingProfileId = profile.ProfileId;
        EditProfileDisplayName = profile.DisplayName;
        EditProfileBaseUrl = profile.BaseUrl;
        EditProfileHttpPort = profile.HttpCallbackPort;
        EditProfileTcpPort = profile.TcpCallbackPort;
        IsEditingAccountProfile = true;
    }

    [RelayCommand]
    private void CancelEditAccountProfile()
    {
        IsEditingAccountProfile = false;
        EditingProfileId = null;
    }

    [RelayCommand]
    private async Task SaveAccountProfileAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EditProfileDisplayName))
            {
                await _toast.ShowAsync("请填写账号名称");
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingProfileId))
            {
                var profile = new WechatAccountConnectionProfile(
                    Guid.NewGuid().ToString("N")[..8],
                    EditProfileDisplayName.Trim(),
                    string.IsNullOrWhiteSpace(EditProfileBaseUrl)
                        ? "http://127.0.0.1:19088"
                        : EditProfileBaseUrl.Trim(),
                    EditProfileHttpPort,
                    EditProfileTcpPort,
                    null,
                    true);
                await _accountManager.AddProfileAsync(profile);
                await _toast.ShowAsync("已添加账号");
            }
            else
            {
                var existing = _accountManager.Profiles.FirstOrDefault(p =>
                    string.Equals(p.ProfileId, EditingProfileId, StringComparison.Ordinal));
                if (existing is null)
                {
                    await _toast.ShowAsync("账号不存在");
                    return;
                }

                var updated = existing with
                {
                    DisplayName = EditProfileDisplayName.Trim(),
                    BaseUrl = string.IsNullOrWhiteSpace(EditProfileBaseUrl)
                        ? existing.BaseUrl
                        : EditProfileBaseUrl.Trim(),
                    HttpCallbackPort = EditProfileHttpPort,
                    TcpCallbackPort = EditProfileTcpPort
                };
                await _accountManager.UpdateProfileAsync(updated);
                await _toast.ShowAsync("已保存账号");
            }

            IsEditingAccountProfile = false;
            EditingProfileId = null;
            RefreshAccountProfileCards();
            RefreshAccountMenu();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save account profile");
            await _toast.ShowAsync(ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase)
                ? "端口冲突，请修改后重试"
                : "保存失败");
        }
    }

    [RelayCommand]
    private async Task DeleteAccountProfileAsync(AccountProfileCardItem? card)
    {
        if (card is null || !card.CanDelete)
        {
            return;
        }

        try
        {
            await _accountManager.DeleteProfileAsync(card.ProfileId);
            RefreshAccountProfileCards();
            RefreshAccountMenu();
            await ContactList.InitializeAsync();
            if (ContactList.SelectedContact is null)
            {
                Chat.ClearConversation();
            }

            await _toast.ShowAsync("已删除账号");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account profile");
            await _toast.ShowAsync("删除失败");
        }
    }

    [RelayCommand]
    private async Task ToggleAccountProfileEnabledAsync(AccountProfileCardItem? card)
    {
        if (card is null)
        {
            return;
        }

        try
        {
            await _accountManager.SetProfileEnabledAsync(card.ProfileId, !card.IsEnabled);
            RefreshAccountProfileCards();
            RefreshAccountMenu();
            await _toast.ShowAsync(card.IsEnabled ? "已禁用" : "已启用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle account profile");
            await _toast.ShowAsync("操作失败");
        }
    }
}

public sealed class AccountMenuItem
{
    public string? AccountId { get; init; }
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public bool IsSelected { get; init; }
}

public sealed class AccountProfileCardItem
{
    public string ProfileId { get; init; } = "";
    public string Title { get; init; } = "";
    public string PortsText { get; init; } = "";
    public string StatusText { get; init; } = "";
    public string? Wxid { get; init; }
    public bool IsEnabled { get; init; }
    public bool CanDelete { get; init; }
    public string ToggleEnabledLabel => IsEnabled ? "禁用" : "启用";
}
