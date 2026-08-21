using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Helpers;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IWechatService _wechatService;
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly IAISettingsService _aiSettings;
    private readonly ILogger<ChatViewModel> _logger;
    private CancellationTokenSource? _loadCts;
    private int _loadVersion;
    private int _draftRevision;
    private bool _suppressDraftRevision;
    private EventHandler<MessageReceivedEventArgs>? _messageReceivedHandler;
    private HashSet<string> _pinnedIds = new(StringComparer.Ordinal);

    public ChatViewModel(
        IWechatService wechatService,
        IFilePickerService filePicker,
        IToastService toast,
        IAISettingsService aiSettings,
        ILogger<ChatViewModel> logger)
    {
        _wechatService = wechatService;
        _filePicker = filePicker;
        _toast = toast;
        _aiSettings = aiSettings;
        _logger = logger;

        _messageReceivedHandler = OnMessageReceived;
        _wechatService.MessageReceived += _messageReceivedHandler;
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public int DraftRevision => _draftRevision;

    [ObservableProperty]
    private Contact? _currentContact;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private string _draftText = string.Empty;

    [ObservableProperty]
    private bool _isAiAssistantActive;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    [ObservableProperty]
    private bool _isNearBottom = true;

    [ObservableProperty]
    private bool _keepAtBottom = true;

    [ObservableProperty]
    private double _bubbleMaxWidth = 420;

    [ObservableProperty]
    private bool _isEmojiPickerOpen;

    public bool CanSend =>
        !IsSending &&
        CurrentContact is not null &&
        !string.IsNullOrWhiteSpace(DraftText) &&
        _wechatService.ConnectionState == WechatConnectionState.Connected;

    public void NotifyConnectionStateChanged() => NotifyCanSendChanged();

    public event EventHandler<ChatMessagesChangedEventArgs>? MessagesChanged;
    public event EventHandler? MessagesUpdated;
    public event EventHandler? RequestAiAssist;
    public event EventHandler<string>? MessageSent;
    public event EventHandler<Contact>? ContactPreviewUpdated;

    public async Task LoadContactAsync(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var version = Interlocked.Increment(ref _loadVersion);
        var contactId = contact.Id;

        CurrentContact = contact;
        contact.UnreadCount = 0;
        Messages.Clear();
        IsEmojiPickerOpen = false;
        NotifyCanSendChanged();
        await RefreshPinsAsync(contactId);

        try
        {
            var messages = await _wechatService.GetMessagesAsync(contactId, cts.Token);
            if (version != _loadVersion || CurrentContact?.Id != contactId || cts.IsCancellationRequested)
            {
                return;
            }

            Messages.Clear();
            foreach (var message in messages)
            {
                message.IsPinned = _pinnedIds.Contains(message.Id);
                Messages.Add(message);
            }

            RaiseMessagesChanged(contactId, forceScroll: true);
        }
        catch (OperationCanceledException)
        {
            // superseded load
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages for {ContactId}", contactId);
        }
    }

    public void SetDraftFromAi(string text)
    {
        _suppressDraftRevision = true;
        try
        {
            DraftText = text ?? string.Empty;
        }
        finally
        {
            _suppressDraftRevision = false;
        }
    }

    public bool TryApplyAiDraft(string text, int expectedRevision)
    {
        if (_draftRevision != expectedRevision)
        {
            return false;
        }

        SetDraftFromAi(text);
        return true;
    }

    public bool IsPinned(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        return _pinnedIds.Contains(messageId);
    }

    [RelayCommand]
    private async Task PinMessageAsync(ChatMessage? message)
    {
        if (message is null || CurrentContact is null)
        {
            return;
        }

        var wasPinned = IsPinned(message.Id);
        var pinned = await _aiSettings.TogglePinAsync(CurrentContact.Id, message.Id);
        await RefreshPinsAsync(CurrentContact.Id);
        if (!wasPinned && !pinned)
        {
            await _toast.ShowAsync("最多置顶 20 条");
        }
        else
        {
            await _toast.ShowAsync(pinned ? "已置顶" : "已取消置顶");
        }

        OnPropertyChanged(nameof(Messages));
    }

    [RelayCommand]
    private async Task UnpinMessageAsync(ChatMessage? message)
    {
        if (message is null || CurrentContact is null)
        {
            return;
        }

        if (!IsPinned(message.Id))
        {
            return;
        }

        await _aiSettings.TogglePinAsync(CurrentContact.Id, message.Id);
        await RefreshPinsAsync(CurrentContact.Id);
        await _toast.ShowAsync("已取消置顶");
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (CurrentContact is null || string.IsNullOrWhiteSpace(DraftText))
        {
            return;
        }

        var targetContactId = CurrentContact.Id;
        var pending = DraftText.Trim();
        DraftText = string.Empty;
        IsSending = true;

        try
        {
            if (_wechatService.ConnectionState != WechatConnectionState.Connected)
            {
                DraftText = pending;
                await _toast.ShowAsync("微信未连接，无法发送");
                return;
            }

            var result = await _wechatService.SendTextMessageAsync(targetContactId, pending);
            if (!result.Success)
            {
                if (CurrentContact?.Id == targetContactId && string.IsNullOrEmpty(DraftText))
                {
                    DraftText = pending;
                }

                await _toast.ShowAsync(result.ErrorMessage ?? "发送失败");
                return;
            }

            // Prefer message from service cache via GetMessages or build from result
            var message = new ChatMessage
            {
                Id = result.MessageId ?? Guid.NewGuid().ToString("N"),
                ContactId = targetContactId,
                ClientRequestId = result.ClientRequestId,
                SenderName = "我",
                IsSelf = true,
                Source = MessageSource.LocalUserManual,
                SenderAvatarColor = "#7C5CFF",
                SenderInitials = "我",
                Content = pending,
                Timestamp = result.Timestamp,
                SendStatus = MessageSendStatus.Sent
            };

            // RealWechatService already stores; avoid duplicate if event/path added it
            if (CurrentContact?.Id == targetContactId &&
                Messages.All(m => m.Id != message.Id &&
                                  !string.Equals(m.ClientRequestId, message.ClientRequestId, StringComparison.Ordinal)))
            {
                Messages.Add(message);
                RaiseMessagesChanged(targetContactId, forceScroll: true);
            }

            UpdateContactPreview(targetContactId, pending, "我", message.Timestamp);
            MessageSent?.Invoke(this, targetContactId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            if (CurrentContact?.Id == targetContactId && string.IsNullOrEmpty(DraftText))
            {
                DraftText = pending;
            }

            await _toast.ShowAsync("发送失败");
        }
        finally
        {
            IsSending = false;
        }
    }

    public async Task SendAsync(string contactId, string content, bool isFromAi = false)
    {
        if (string.IsNullOrWhiteSpace(contactId) || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            if (_wechatService.ConnectionState != WechatConnectionState.Connected)
            {
                await _toast.ShowAsync("微信未连接，无法发送");
                return;
            }

            var message = await _wechatService.SendMessageAsync(
                contactId,
                content.Trim(),
                MessageType.Text,
                isFromAi: isFromAi);

            if (message.SendStatus == MessageSendStatus.Failed)
            {
                await _toast.ShowAsync("发送失败");
                return;
            }

            if (CurrentContact?.Id == contactId &&
                Messages.All(m => m.Id != message.Id &&
                                  !string.Equals(m.ClientRequestId, message.ClientRequestId, StringComparison.Ordinal)))
            {
                Messages.Add(message);
                RaiseMessagesChanged(contactId, forceScroll: IsNearBottom || KeepAtBottom);
            }

            UpdateContactPreview(contactId, content.Trim(), message.SenderName, message.Timestamp);
            MessageSent?.Invoke(this, contactId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {ContactId}", contactId);
            throw;
        }
    }

    [RelayCommand]
    private async Task SendImageAsync()
    {
        if (CurrentContact is null)
        {
            return;
        }

        var path = await _filePicker.PickImageAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var targetContactId = CurrentContact.Id;
        var fileName = Path.GetFileName(path);
        var message = await _wechatService.SendMessageAsync(
            targetContactId,
            "[图片]",
            MessageType.Image,
            fileName: fileName,
            imagePath: path);

        if (CurrentContact?.Id == targetContactId)
        {
            Messages.Add(message);
            RaiseMessagesChanged(targetContactId, forceScroll: true);
        }

        UpdateContactPreview(targetContactId, "[图片]", "我", message.Timestamp);
        MessageSent?.Invoke(this, targetContactId);
    }

    [RelayCommand]
    private async Task SendFileAsync()
    {
        if (CurrentContact is null)
        {
            return;
        }

        var path = await _filePicker.PickFileAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var targetContactId = CurrentContact.Id;
        var fileName = Path.GetFileName(path);
        var fileInfo = new FileInfo(path);
        var sizeText = FormatSize(fileInfo.Exists ? fileInfo.Length : 0);
        var message = await _wechatService.SendMessageAsync(
            targetContactId,
            "[文件]",
            MessageType.File,
            fileName: fileName,
            fileSize: sizeText);

        if (CurrentContact?.Id == targetContactId)
        {
            Messages.Add(message);
            RaiseMessagesChanged(targetContactId, forceScroll: true);
        }

        UpdateContactPreview(targetContactId, $"文件已发送：{fileName}", "我", message.Timestamp);
        MessageSent?.Invoke(this, targetContactId);
    }

    [RelayCommand]
    private void ToggleEmojiPicker() => IsEmojiPickerOpen = !IsEmojiPickerOpen;

    [RelayCommand]
    private void InsertEmoji(string? emoji)
    {
        if (string.IsNullOrEmpty(emoji))
        {
            return;
        }

        DraftText += emoji;
        IsEmojiPickerOpen = false;
    }

    [RelayCommand]
    private void PickImage() => SendImageAsync().SafeFireAndForget(_logger);

    [RelayCommand]
    private void PickFile() => SendFileAsync().SafeFireAndForget(_logger);

    [RelayCommand]
    private void ToggleAiAssistant()
    {
        IsAiAssistantActive = !IsAiAssistantActive;
        RequestAiAssist?.Invoke(this, EventArgs.Empty);
    }

    public void Cleanup()
    {
        if (_messageReceivedHandler is not null)
        {
            _wechatService.MessageReceived -= _messageReceivedHandler;
            _messageReceivedHandler = null;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    partial void OnDraftTextChanged(string value)
    {
        if (!_suppressDraftRevision)
        {
            Interlocked.Increment(ref _draftRevision);
        }

        NotifyCanSendChanged();
    }

    partial void OnCurrentContactChanged(Contact? value) => NotifyCanSendChanged();

    private async Task RefreshPinsAsync(string contactId)
    {
        try
        {
            var pins = await _aiSettings.GetPinnedIdsAsync(contactId);
            _pinnedIds = new HashSet<string>(pins, StringComparer.Ordinal);
            foreach (var message in Messages)
            {
                message.IsPinned = _pinnedIds.Contains(message.Id);
            }

            // Force item rebind for IsPinned visibility
            OnPropertyChanged(nameof(Messages));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh pins for {ContactId}", contactId);
            _pinnedIds = new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private void NotifyCanSendChanged()
    {
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (CurrentContact?.Id != e.ContactId)
            {
                return;
            }

            CurrentContact.LastMessage = e.Message.Content;
            CurrentContact.LastSender = e.Message.SenderName;
            CurrentContact.LastMessageTime = e.Message.Timestamp;
            CurrentContact.UnreadCount = 0;
            ContactPreviewUpdated?.Invoke(this, CurrentContact);

            if (Messages.All(m => m.Id != e.Message.Id))
            {
                Messages.Add(e.Message);
                RaiseMessagesChanged(e.ContactId, forceScroll: IsNearBottom || KeepAtBottom);
            }
        });
    }

    private void UpdateContactPreview(string contactId, string content, string sender, DateTime timestamp)
    {
        if (CurrentContact?.Id == contactId)
        {
            CurrentContact.LastMessage = content;
            CurrentContact.LastSender = sender;
            CurrentContact.LastMessageTime = timestamp;
            ContactPreviewUpdated?.Invoke(this, CurrentContact);
        }
    }

    private void RaiseMessagesChanged(string contactId, bool forceScroll)
    {
        MessagesChanged?.Invoke(this, new ChatMessagesChangedEventArgs(contactId, forceScroll));
        MessagesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
