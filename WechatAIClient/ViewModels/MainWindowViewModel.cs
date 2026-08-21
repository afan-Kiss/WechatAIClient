using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ILogger<MainWindowViewModel> _logger;

    public MainWindowViewModel(
        ContactListViewModel contactList,
        ChatViewModel chat,
        AIPanelViewModel aiPanel,
        IThemeService themeService,
        ILogger<MainWindowViewModel> logger)
    {
        ContactList = contactList;
        Chat = chat;
        AiPanel = aiPanel;
        _themeService = themeService;
        _logger = logger;
        SelectedThemeMode = themeService.CurrentMode;

        ContactList.ContactSelected += async (_, contact) => await Chat.LoadContactAsync(contact);
        Chat.RequestAiAssist += async (_, _) => await GenerateAiReplyAsync();
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
    private int _navIndex;

    [ObservableProperty]
    private bool _isAiPanelVisible = true;

    public async Task InitializeAsync()
    {
        try
        {
            await ContactList.InitializeAsync();
            await AiPanel.InitializeAsync();
            if (ContactList.SelectedContact is not null)
            {
                await Chat.LoadContactAsync(ContactList.SelectedContact);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main window");
        }
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

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
        if (index == 3)
        {
            IsSettingsOpen = true;
        }
    }

    [RelayCommand]
    private void ToggleAiPanel() => IsAiPanelVisible = !IsAiPanelVisible;

    [RelayCommand]
    private async Task GenerateAiReplyAsync()
    {
        if (Chat.CurrentContact is null)
        {
            return;
        }

        var reply = await AiPanel.GenerateReplyAsync(Chat.Messages.ToList(), Chat.CurrentContact.Name);
        if (reply is null)
        {
            return;
        }

        if (AiPanel.ReplyMode == AIReplyMode.Auto)
        {
            Chat.DraftText = reply;
        }
    }
}
