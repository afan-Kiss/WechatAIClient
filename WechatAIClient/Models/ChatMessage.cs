namespace WechatAIClient.Models;

public sealed class ChatMessage : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isPinned;
    private MessageSendStatus _sendStatus = MessageSendStatus.None;
    private string? _imageUrl;
    private string? _localPath;
    private string? _emojiUrl;
    private string? _senderAvatarUrl;
    private string? _senderAvatarPath;
    private MediaLoadState _mediaLoadState;
    private string? _mediaError;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AccountId { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public ConversationKey Conversation => new(AccountId, ContactId);
    public MessageKey Key => new(AccountId, ContactId, Id);

    public string SenderName { get; set; } = string.Empty;
    public string SenderAvatarColor { get; set; } = "#7C5CFF";
    public string SenderInitials { get; set; } = "?";
    public bool IsSelf { get; set; }
    public bool IsFromAi { get; set; }
    public MessageSource Source { get; set; } = MessageSource.RemoteUser;
    public MessageType Type { get; set; } = MessageType.Text;
    public string Content { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public string? QuoteSender { get; set; }
    public string? QuoteContent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool ShowTimeSeparator { get; set; }
    public string? TimeSeparatorText { get; set; }
    public bool MentionsMe { get; set; }
    public bool QuotesMe { get; set; }
    public string? ClientRequestId { get; set; }
    public string? SenderId { get; set; }
    public string? ReplyToMessageId { get; set; }
    public string? RawMessageType { get; set; }

    public string? ImageUrl
    {
        get => _imageUrl;
        set => SetField(ref _imageUrl, value, nameof(ImageUrl));
    }

    public string? LocalPath
    {
        get => _localPath;
        set => SetField(ref _localPath, value, nameof(LocalPath));
    }

    public string? EmojiUrl
    {
        get => _emojiUrl;
        set => SetField(ref _emojiUrl, value, nameof(EmojiUrl));
    }

    public string? SenderAvatarUrl
    {
        get => _senderAvatarUrl;
        set => SetField(ref _senderAvatarUrl, value, nameof(SenderAvatarUrl));
    }

    public string? SenderAvatarPath
    {
        get => _senderAvatarPath;
        set => SetField(ref _senderAvatarPath, value, nameof(SenderAvatarPath));
    }

    public MediaLoadState MediaLoadState
    {
        get => _mediaLoadState;
        set
        {
            if (_mediaLoadState == value)
            {
                return;
            }

            _mediaLoadState = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MediaLoadState)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsMediaLoading)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsMediaFailed)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsMediaLoaded)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsMediaPlaceholder)));
        }
    }

    public string? MediaError
    {
        get => _mediaError;
        set => SetField(ref _mediaError, value, nameof(MediaError));
    }

    public bool IsMediaLoading => MediaLoadState == MediaLoadState.Loading;
    public bool IsMediaFailed => MediaLoadState == MediaLoadState.Failed;
    public bool IsMediaLoaded => MediaLoadState == MediaLoadState.Loaded;
    public bool IsMediaPlaceholder => MediaLoadState != MediaLoadState.Loaded;

    public MessageSendStatus SendStatus
    {
        get => _sendStatus;
        set
        {
            if (_sendStatus == value)
            {
                return;
            }

            _sendStatus = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SendStatus)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSendFailed)));
        }
    }

    public bool IsSendFailed => SendStatus == MessageSendStatus.Failed;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value)
            {
                return;
            }

            _isPinned = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsPinned)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, string name)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
