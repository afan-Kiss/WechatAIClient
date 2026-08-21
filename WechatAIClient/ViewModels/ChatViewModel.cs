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
    private readonly IConversationDraftStore _drafts;
    private readonly ILogger<ChatViewModel> _logger;
    private readonly Dictionary<string, int> _draftRevisions = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCts;
    private int _loadVersion;
    private bool _suppressDraftRevision;
    private EventHandler<MessageReceivedEventArgs>? _messageReceivedHandler;
    private EventHandler<AccountConnectionStateChangedEventArgs>? _accountStateHandler;
    private HashSet<string> _pinnedIds = new(StringComparer.Ordinal);

    public ChatViewModel(
        IWechatService wechatService,
        IFilePickerService filePicker,
        IToastService toast,
        IAISettingsService aiSettings,
        IConversationDraftStore drafts,
        ILogger<ChatViewModel> logger)
    {
        _wechatService = wechatService;
        _filePicker = filePicker;
        _toast = toast;
        _aiSettings = aiSettings;
        _drafts = drafts;
        _logger = logger;

        _messageReceivedHandler = OnMessageReceived;
        _wechatService.MessageReceived += _messageReceivedHandler;
        _accountStateHandler = (_, _) =>
            Dispatcher.UIThread.Post(NotifyCanSendChanged);
        _wechatService.AccountConnectionStateChanged += _accountStateHandler;
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public int DraftRevision
    {
        get
        {
            if (CurrentContact is null)
            {
                return 0;
            }

            return _draftRevisions.TryGetValue(CurrentContact.Key.StableKey, out var rev) ? rev : 0;
        }
    }

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
        _wechatService.CanManualSend(CurrentContact.Key);

    public void NotifyConnectionStateChanged() => NotifyCanSendChanged();

    public event EventHandler<ChatMessagesChangedEventArgs>? MessagesChanged;
    public event EventHandler? MessagesUpdated;
    public event EventHandler? RequestAiAssist;
    public event EventHandler<ConversationKey>? MessageSent;
    public event EventHandler<Contact>? ContactPreviewUpdated;

    public void ClearConversation()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        Interlocked.Increment(ref _loadVersion);

        if (CurrentContact is { } previous)
        {
            _drafts.SetDraft(previous.Key, DraftText);
        }

        CurrentContact = null;
        Messages.Clear();
        IsEmojiPickerOpen = false;
        _pinnedIds = new HashSet<string>(StringComparer.Ordinal);
        _suppressDraftRevision = true;
        DraftText = string.Empty;
        _suppressDraftRevision = false;
        NotifyCanSendChanged();
        OnPropertyChanged(nameof(DraftRevision));
        RaiseMessagesChanged(string.Empty, string.Empty, forceScroll: false);
    }

    public async Task LoadContactAsync(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        if (CurrentContact is { } previous)
        {
            _drafts.SetDraft(previous.Key, DraftText);
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var version = Interlocked.Increment(ref _loadVersion);
        var key = contact.Key;

        CurrentContact = contact;
        contact.UnreadCount = 0;
        Messages.Clear();
        IsEmojiPickerOpen = false;
        _suppressDraftRevision = true;
        DraftText = _drafts.GetDraft(key) ?? string.Empty;
        _suppressDraftRevision = false;
        OnPropertyChanged(nameof(DraftRevision));
        NotifyCanSendChanged();
        await RefreshPinsAsync(key, version);

        try
        {
            var messages = await _wechatService.GetMessagesAsync(key, cts.Token);
            if (version != _loadVersion ||
                CurrentContact?.Key != key ||
                cts.IsCancellationRequested)
            {
                return;
            }

            Messages.Clear();
            foreach (var message in messages)
            {
                message.IsPinned = _pinnedIds.Contains(message.Id);
                Messages.Add(message);
            }

            RaiseMessagesChanged(key.AccountId, key.ConversationId, forceScroll: true);
        }
        catch (OperationCanceledException)
        {
            // superseded load
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages for {Key}", key);
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

    public bool TryApplyAiDraft(ConversationKey target, string text, int expectedRevision)
    {
        if (CurrentContact?.Key != target)
        {
            return false;
        }

        var key = target.StableKey;
        var current = _draftRevisions.TryGetValue(key, out var rev) ? rev : 0;
        if (current != expectedRevision)
        {
            return false;
        }

        SetDraftFromAi(text);
        return true;
    }

    public bool TryApplyAiDraft(string text, int expectedRevision)
    {
        if (CurrentContact is null)
        {
            return false;
        }

        return TryApplyAiDraft(CurrentContact.Key, text, expectedRevision);
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

        var key = CurrentContact.Key;
        var version = _loadVersion;
        var wasPinned = IsPinned(message.Id);
        var pinned = await _aiSettings.TogglePinAsync(key.AccountId, key.ConversationId, message.Id);
        await RefreshPinsAsync(key, version);
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

        var key = CurrentContact.Key;
        var version = _loadVersion;
        await _aiSettings.TogglePinAsync(key.AccountId, key.ConversationId, message.Id);
        await RefreshPinsAsync(key, version);
        await _toast.ShowAsync("已取消置顶");
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (CurrentContact is null || string.IsNullOrWhiteSpace(DraftText))
        {
            return;
        }

        var target = CurrentContact.Key;
        var pending = DraftText.Trim();
        DraftText = string.Empty;
        IsSending = true;

        try
        {
            if (!_wechatService.CanSend(target))
            {
                DraftText = pending;
                await _toast.ShowAsync("微信未连接，无法发送");
                return;
            }

            var result = await _wechatService.SendTextMessageAsync(target, pending);
            _drafts.Clear(target);
            if (!result.Success)
            {
                if (CurrentContact?.Key == target && string.IsNullOrEmpty(DraftText))
                {
                    DraftText = pending;
                }

                await _toast.ShowAsync(result.ErrorMessage ?? "发送失败");
                return;
            }

            var message = new ChatMessage
            {
                Id = result.MessageId ?? Guid.NewGuid().ToString("N"),
                AccountId = target.AccountId,
                ContactId = target.ConversationId,
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

            if (CurrentContact?.Key == target &&
                Messages.All(m => m.Id != message.Id &&
                                  !string.Equals(m.ClientRequestId, message.ClientRequestId, StringComparison.Ordinal)))
            {
                Messages.Add(message);
                RaiseMessagesChanged(target.AccountId, target.ConversationId, forceScroll: true);
            }

            UpdateContactPreview(target, pending, "我", message.Timestamp);
            MessageSent?.Invoke(this, target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            if (CurrentContact?.Key == target && string.IsNullOrEmpty(DraftText))
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
        string accountId;
        if (CurrentContact is not null &&
            string.Equals(CurrentContact.Id, contactId, StringComparison.Ordinal))
        {
            accountId = CurrentContact.AccountId;
        }
        else if (!string.IsNullOrWhiteSpace(_wechatService.SelectedAccountId))
        {
            accountId = _wechatService.SelectedAccountId!;
        }
        else
        {
            var pool = (await _wechatService.GetRecentChatsAsync())
                .Concat(await _wechatService.GetContactsAsync())
                .Concat(await _wechatService.GetGroupsAsync());
            accountId = pool.FirstOrDefault(c => string.Equals(c.Id, contactId, StringComparison.Ordinal))
                            ?.AccountId
                        ?? _wechatService.GetAccounts().FirstOrDefault()?.AccountId
                        ?? SqliteStore.LegacyAccountId;
        }

        await SendAsync(accountId, contactId, content, isFromAi);
    }

    public async Task SendAsync(string accountId, string contactId, string content, bool isFromAi = false)
    {
        if (string.IsNullOrWhiteSpace(contactId) || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var key = new ConversationKey(
            SqliteStore.NormalizeAccountId(accountId),
            contactId);

        try
        {
            if (!_wechatService.CanSend(key))
            {
                await _toast.ShowAsync("微信未连接，无法发送");
                return;
            }

            var message = await _wechatService.SendMessageAsync(
                key,
                content.Trim(),
                MessageType.Text,
                isFromAi: isFromAi);

            if (message.SendStatus == MessageSendStatus.Failed)
            {
                await _toast.ShowAsync("发送失败");
                return;
            }

            if (CurrentContact?.Key == key &&
                Messages.All(m => m.Id != message.Id &&
                                  !string.Equals(m.ClientRequestId, message.ClientRequestId, StringComparison.Ordinal)))
            {
                Messages.Add(message);
                RaiseMessagesChanged(key.AccountId, key.ConversationId, forceScroll: IsNearBottom || KeepAtBottom);
            }

            UpdateContactPreview(key, content.Trim(), message.SenderName, message.Timestamp);
            MessageSent?.Invoke(this, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {Key}", key);
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

        var target = CurrentContact.Key;
        var fileName = Path.GetFileName(path);
        var message = await _wechatService.SendMessageAsync(
            target,
            "[图片]",
            MessageType.Image,
            fileName: fileName,
            imagePath: path);

        if (CurrentContact?.Key == target)
        {
            Messages.Add(message);
            RaiseMessagesChanged(target.AccountId, target.ConversationId, forceScroll: true);
        }

        UpdateContactPreview(target, "[图片]", "我", message.Timestamp);
        MessageSent?.Invoke(this, target);
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

        var target = CurrentContact.Key;
        var fileName = Path.GetFileName(path);
        var fileInfo = new FileInfo(path);
        var sizeText = FormatSize(fileInfo.Exists ? fileInfo.Length : 0);
        var message = await _wechatService.SendMessageAsync(
            target,
            "[文件]",
            MessageType.File,
            fileName: fileName,
            fileSize: sizeText);

        if (CurrentContact?.Key == target)
        {
            Messages.Add(message);
            RaiseMessagesChanged(target.AccountId, target.ConversationId, forceScroll: true);
        }

        UpdateContactPreview(target, $"文件已发送：{fileName}", "我", message.Timestamp);
        MessageSent?.Invoke(this, target);
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

        if (_accountStateHandler is not null)
        {
            _wechatService.AccountConnectionStateChanged -= _accountStateHandler;
            _accountStateHandler = null;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    partial void OnDraftTextChanged(string value)
    {
        if (!_suppressDraftRevision && CurrentContact is { } contact)
        {
            var key = contact.Key.StableKey;
            _draftRevisions.TryGetValue(key, out var rev);
            _draftRevisions[key] = rev + 1;
            OnPropertyChanged(nameof(DraftRevision));
        }

        if (CurrentContact is { } c)
        {
            _drafts.SetDraft(c.Key, value);
        }

        NotifyCanSendChanged();
    }

    partial void OnCurrentContactChanged(Contact? value) => NotifyCanSendChanged();

    private async Task RefreshPinsAsync(ConversationKey key, int loadVersion)
    {
        try
        {
            var pins = await _aiSettings.GetPinnedIdsAsync(key.AccountId, key.ConversationId);
            if (loadVersion != _loadVersion || CurrentContact?.Key != key)
            {
                return;
            }

            _pinnedIds = new HashSet<string>(pins, StringComparer.Ordinal);
            foreach (var message in Messages)
            {
                message.IsPinned = _pinnedIds.Contains(message.Id);
            }

            OnPropertyChanged(nameof(Messages));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh pins for {Key}", key);
            if (loadVersion == _loadVersion && CurrentContact?.Key == key)
            {
                _pinnedIds = new HashSet<string>(StringComparer.Ordinal);
            }
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
            if (CurrentContact is null || CurrentContact.Key != e.Conversation)
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
                RaiseMessagesChanged(e.AccountId, e.ContactId, forceScroll: IsNearBottom || KeepAtBottom);
            }
        });
    }

    private void UpdateContactPreview(ConversationKey key, string content, string sender, DateTime timestamp)
    {
        if (CurrentContact?.Key == key)
        {
            CurrentContact.LastMessage = content;
            CurrentContact.LastSender = sender;
            CurrentContact.LastMessageTime = timestamp;
            ContactPreviewUpdated?.Invoke(this, CurrentContact);
        }
    }

    private void RaiseMessagesChanged(string accountId, string contactId, bool forceScroll)
    {
        MessagesChanged?.Invoke(this, new ChatMessagesChangedEventArgs(accountId, contactId, forceScroll));
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
