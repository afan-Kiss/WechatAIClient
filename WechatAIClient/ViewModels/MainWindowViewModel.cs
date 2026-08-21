using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Helpers;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IWechatService _wechatService;
    private readonly IToastService _toast;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _autoDebounce = new(StringComparer.Ordinal);
    private EventHandler<MessageReceivedEventArgs>? _messageReceivedHandler;
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
        IToastService toast,
        ILogger<MainWindowViewModel> logger)
    {
        ContactList = contactList;
        Chat = chat;
        AiPanel = aiPanel;
        _themeService = themeService;
        _wechatService = wechatService;
        _toast = toast;
        _logger = logger;
        SelectedThemeMode = themeService.CurrentMode;

        _contactSelectedHandler = (_, contact) =>
            Chat.LoadContactAsync(contact).SafeFireAndForget(_logger);
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

    public async Task InitializeAsync()
    {
        try
        {
            await _themeService.RestoreAsync();
            SelectedThemeMode = _themeService.CurrentMode;
            await ContactList.InitializeAsync();
            await AiPanel.InitializeAsync();
            // SelectedContact set during ContactList.Initialize fires ContactSelected -> LoadContactAsync.
            // Do not call Chat.LoadContactAsync again here.
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
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
        {
            NavIndex = 3;
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
    private async Task NotifyPhase2Async(string? feature)
    {
        var name = string.IsNullOrWhiteSpace(feature) ? "该功能" : feature;
        await _toast.ShowAsync($"{name}将在后续版本提供");
    }

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
        var request = new AIGenerationRequest
        {
            ContactId = contact.Id,
            ContactName = contact.Name,
            ContextSnapshot = Chat.Messages.ToList(),
            ContextLength = AiPanel.ContextLength,
            ReplyMode = AiPanel.ReplyMode
        };

        var reply = await AiPanel.GenerateForContactAsync(request);
        if (reply is null)
        {
            return;
        }

        if (AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            await Chat.SendAsync(contact.Id, reply, isFromAi: true);
        }
        else if (AiPanel.ReplyMode == AIReplyMode.ManualConfirm)
        {
            if (Chat.CurrentContact?.Id == contact.Id)
            {
                Chat.DraftText = reply;
            }
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (e.Message.IsSelf || e.Message.IsFromAi)
        {
            return;
        }

        if (!AiPanel.AutoGenerateOnReceive)
        {
            return;
        }

        if (AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            ScheduleAutoGenerate(e.ContactId, autoSend: true);
        }
        else if (AiPanel.ReplyMode == AIReplyMode.ManualConfirm)
        {
            // Draft only — do not send.
            ScheduleAutoGenerate(e.ContactId, autoSend: false);
        }
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
            await Task.Delay(400, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
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
            var replyMode = autoSend ? AIReplyMode.Auto : AIReplyMode.ManualConfirm;
            var request = new AIGenerationRequest
            {
                ContactId = contactId,
                ContactName = contactName,
                ContextSnapshot = messages.ToList(),
                ContextLength = AiPanel.ContextLength,
                ReplyMode = replyMode
            };

            var reply = await AiPanel.GenerateForContactAsync(request);
            if (reply is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (autoSend)
            {
                await Chat.SendAsync(contactId, reply, isFromAi: true);
            }
            else if (Chat.CurrentContact?.Id == contactId)
            {
                Chat.DraftText = reply;
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
        finally
        {
            if (_autoDebounce.TryGetValue(contactId, out var cts) && cts.IsCancellationRequested == false)
            {
                // keep until superseded
            }
        }
    }
}
