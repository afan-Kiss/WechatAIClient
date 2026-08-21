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
    private readonly IAISettingsService _aiSettings;
    private readonly ISecretStore _secrets;
    private readonly IAIService _aiService;
    private readonly IToastService _toast;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _autoDebounce = new(StringComparer.Ordinal);
    private EventHandler<MessageReceivedEventArgs>? _messageReceivedHandler;
    private EventHandler<WechatConnectionState>? _connectionStateHandler;
    private EventHandler? _toastChangedHandler;
    private EventHandler<Contact>? _contactSelectedHandler;
    private EventHandler? _requestAiAssistHandler;
    private EventHandler<Contact>? _contactPreviewHandler;
    private EventHandler<string>? _messageSentHandler;

    public MainWindowViewModel(
        ContactListViewModel contactList,
        ChatViewModel chat,
        AIPanelViewModel aiPanel,
        IThemeService themeService,
        IWechatService wechatService,
        IAISettingsService aiSettings,
        ISecretStore secrets,
        IAIService aiService,
        IToastService toast,
        ISettingsStore settingsStore,
        ILogger<MainWindowViewModel> logger)
    {
        ContactList = contactList;
        Chat = chat;
        AiPanel = aiPanel;
        _themeService = themeService;
        _wechatService = wechatService;
        _aiSettings = aiSettings;
        _secrets = secrets;
        _aiService = aiService;
        _toast = toast;
        _settingsStore = settingsStore;
        _logger = logger;
        SelectedThemeMode = themeService.CurrentMode;
        RefreshWechatStatus(_wechatService.ConnectionState);

        _contactSelectedHandler = (_, contact) =>
        {
            Chat.LoadContactAsync(contact).SafeFireAndForget(_logger);
            AiPanel.BindContactAsync(contact.Id).SafeFireAndForget(_logger);
        };
        ContactList.ContactSelected += _contactSelectedHandler;

        _requestAiAssistHandler = (_, _) =>
            GenerateAiReplyAsync().SafeFireAndForget(_logger);
        Chat.RequestAiAssist += _requestAiAssistHandler;

        _contactPreviewHandler = (_, contact) => ContactList.BumpRecent(contact);
        Chat.ContactPreviewUpdated += _contactPreviewHandler;

        _messageSentHandler = (_, contactId) =>
        {
            var contact = ContactList.FindContact(contactId);
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
            });
        _wechatService.ConnectionStateChanged += _connectionStateHandler;

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

    public async Task InitializeAsync()
    {
        try
        {
            await _themeService.RestoreAsync();
            SelectedThemeMode = _themeService.CurrentMode;
            await LoadAiProviderSettingsAsync();
            await LoadWechatProviderSettingsAsync();
            RefreshWechatStatus(await _wechatService.GetConnectionStateAsync());
            Chat.NotifyConnectionStateChanged();
            await ContactList.InitializeAsync();
            await AiPanel.InitializeAsync();
            if (ContactList.SelectedContact is { } selected)
            {
                await AiPanel.BindContactAsync(selected.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main window");
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

        if (_toastChangedHandler is not null)
        {
            _toast.Changed -= _toastChangedHandler;
            _toastChangedHandler = null;
        }

        foreach (var cts in _autoDebounce.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _autoDebounce.Clear();
        Chat.Cleanup();
        ContactList.Cleanup();
        AiPanel.CancelGenerationCommand.Execute(null);
        if (_wechatService is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void RefreshWechatStatus(WechatConnectionState state)
    {
        IsWechatConnected = state == WechatConnectionState.Connected;
        IsWechatWarning = state is WechatConnectionState.VersionUnsupported
            or WechatConnectionState.BridgeError;
        WechatStatusText = state switch
        {
            WechatConnectionState.Connected => "● 微信已连接",
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

    private bool EnsureWechatConnectedForSend()
    {
        if (_wechatService.ConnectionState == WechatConnectionState.Connected)
        {
            return true;
        }

        _ = _toast.ShowAsync(WechatStatusText);
        if (!string.IsNullOrWhiteSpace(AiPanel.LatestGeneratedReply))
        {
            // keep candidate visible
        }

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
            await ContactList.InitializeAsync();
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
            contact.Type == ContactType.Group);
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
        var pins = await _aiSettings.GetPinnedIdsAsync(contact.Id);
        var request = new AIGenerationRequest
        {
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
            if (!EnsureWechatConnectedForSend())
            {
                return;
            }

            await Chat.SendAsync(contact.Id, result.Content, isFromAi: true);
        }
        else if (AiPanel.ReplyMode == AIReplyMode.ManualConfirm)
        {
            await ApplyManualConfirmResultAsync(contact.Id, result.Content, draftRevision);
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

    private async Task ApplyManualConfirmResultAsync(string contactId, string reply, int draftRevisionAtStart)
    {
        if (Chat.CurrentContact?.Id != contactId)
        {
            AiPanel.LatestGeneratedReply = reply;
            return;
        }

        if (!Chat.TryApplyAiDraft(reply, draftRevisionAtStart))
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

        if (_wechatService.ConnectionState != WechatConnectionState.Connected &&
            AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            // Auto must not send while disconnected; Manual can still generate later.
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

        var contact = ContactList.FindContact(e.ContactId) ??
                      (Chat.CurrentContact?.Id == e.ContactId ? Chat.CurrentContact : null);

        if (contact?.Type == ContactType.Group)
        {
            if (!PassesGroupTrigger(AiPanel.GroupTriggerMode, e.Message))
            {
                return;
            }
        }

        if (AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            ScheduleAutoGenerate(e.ContactId, autoSend: true);
        }
        else if (AiPanel.ReplyMode == AIReplyMode.ManualConfirm)
        {
            ScheduleAutoGenerate(e.ContactId, autoSend: false);
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

    private void ScheduleAutoGenerate(string contactId, bool autoSend)
    {
        if (_autoDebounce.TryGetValue(contactId, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }

        var cts = new CancellationTokenSource();
        _autoDebounce[contactId] = cts;

        DebouncedAutoGenerateAsync(contactId, autoSend, cts.Token).SafeFireAndForget(_logger);
    }

    private async Task DebouncedAutoGenerateAsync(string contactId, bool autoSend, CancellationToken cancellationToken)
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

            var messages = await _wechatService.GetMessagesAsync(contactId, cancellationToken);
            var contact = ContactList.FindContact(contactId);
            if (contact is null && Chat.CurrentContact?.Id == contactId)
            {
                contact = Chat.CurrentContact;
            }

            var contactName = contact?.Name ?? contactId;
            var isGroup = contact?.Type == ContactType.Group;
            var replyMode = autoSend ? AIReplyMode.Auto : AIReplyMode.ManualConfirm;
            var draftRevision = Chat.CurrentContact?.Id == contactId ? Chat.DraftRevision : (int?)null;
            var pins = await _aiSettings.GetPinnedIdsAsync(contactId, cancellationToken);
            var effective = await _aiSettings.GetEffectiveAsync(contactId, cancellationToken);

            var request = new AIGenerationRequest
            {
                ContactId = contactId,
                ContactName = contactName,
                ContextSnapshot = messages.ToList(),
                ContextLength = effective.ContextCount,
                ReplyMode = replyMode,
                IncludeOwnMessages = effective.IncludeOwnMessages,
                ReplyStyle = effective.ReplyStyle,
                ReplyLength = effective.ReplyLength,
                TemporaryInstruction = string.IsNullOrWhiteSpace(AiPanel.TemporaryInstruction)
                    ? null
                    : AiPanel.TemporaryInstruction,
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
                if (!EnsureWechatConnectedForSend())
                {
                    AiPanel.LatestGeneratedReply = result.Content;
                    await _toast.ShowAsync("微信未连接，回复已保留为候选");
                    return;
                }

                await Chat.SendAsync(contactId, result.Content, isFromAi: true);
            }
            else if (draftRevision is int rev)
            {
                await ApplyManualConfirmResultAsync(contactId, result.Content, rev);
            }
            else
            {
                AiPanel.LatestGeneratedReply = result.Content;
            }
        }
        catch (OperationCanceledException)
        {
            // debounce cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto AI reply failed for {ContactId}", contactId);
        }
    }
}
