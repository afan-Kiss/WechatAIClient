using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IWechatService _wechatService;
    private readonly ILogger<ChatViewModel> _logger;

    public ChatViewModel(IWechatService wechatService, ILogger<ChatViewModel> logger)
    {
        _wechatService = wechatService;
        _logger = logger;
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    private Contact? _currentContact;

    [ObservableProperty]
    private string _draftText = string.Empty;

    [ObservableProperty]
    private bool _isAiAssistantActive;

    public event EventHandler? MessagesUpdated;
    public event EventHandler? RequestAiAssist;

    public async Task LoadContactAsync(Contact contact)
    {
        CurrentContact = contact;
        Messages.Clear();
        try
        {
            var messages = await _wechatService.GetMessagesAsync(contact.Id);
            foreach (var message in messages)
            {
                Messages.Add(message);
            }

            MessagesUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages for {ContactId}", contact.Id);
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (CurrentContact is null || string.IsNullOrWhiteSpace(DraftText))
        {
            return;
        }

        try
        {
            var content = DraftText.Trim();
            DraftText = string.Empty;
            var message = await _wechatService.SendMessageAsync(CurrentContact.Id, content);
            Messages.Add(message);
            CurrentContact.LastMessage = content;
            CurrentContact.LastMessageTime = message.Timestamp;
            MessagesUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
        }
    }

    [RelayCommand]
    private void ToggleAiAssistant()
    {
        IsAiAssistantActive = !IsAiAssistantActive;
        RequestAiAssist?.Invoke(this, EventArgs.Empty);
    }

    public void AppendIncomingMock(string content)
    {
        if (CurrentContact is null)
        {
            return;
        }

        var message = new ChatMessage
        {
            ContactId = CurrentContact.Id,
            SenderName = CurrentContact.Name,
            SenderAvatarColor = CurrentContact.AvatarColor,
            SenderInitials = CurrentContact.AvatarInitials,
            Content = content,
            Timestamp = DateTime.Now
        };
        Messages.Add(message);
        MessagesUpdated?.Invoke(this, EventArgs.Empty);
    }
}
