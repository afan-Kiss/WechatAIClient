namespace WechatAIClient.Models;

public sealed class ChatMessage : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private bool _isPinned;
    private MessageSendStatus _sendStatus = MessageSendStatus.None;
    private string? _imageUrl;
    private string? _localPath;
    private string? _emojiUrl;
    private string? _senderAvatarUrl;
    private string? _senderAvatarPath;
    private MediaLoadState _mediaLoadState;
    private string? _mediaError;

    public string Id
    {
        get => _id;
        set
        {
            if (string.Equals(_id, value, StringComparison.Ordinal))
            {
                return;
            }

            _id = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Id)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Key)));
        }
    }

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

    public string? CdnUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public string? Md5 { get; set; }
    public string? RawXml { get; set; }
    public string? FromUserName { get; set; }
    public string? ToUserName { get; set; }
    public long? TotalLen { get; set; }
    public int? CompressType { get; set; }
    public string? AttachId { get; set; }
    public string? MediaMsgId { get; set; }

    public string? ImageUrl
    {
        get => _imageUrl;
        set
        {
            if (SetField(ref _imageUrl, value, nameof(ImageUrl)))
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ResolvedMediaPath)));
            }
        }
    }

    public string? LocalPath
    {
        get => _localPath;
        set
        {
            if (SetField(ref _localPath, value, nameof(LocalPath)))
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ResolvedMediaPath)));
            }
        }
    }

    public string? EmojiUrl
    {
        get => _emojiUrl;
        set
        {
            if (SetField(ref _emojiUrl, value, nameof(EmojiUrl)))
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ResolvedMediaPath)));
            }
        }
    }

    /// <summary>Preferred local path for UI image binding (LocalPath, then local ImageUrl/EmojiUrl).</summary>
    public string? ResolvedMediaPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_localPath))
            {
                return _localPath;
            }

            if (IsUsableLocalPath(_imageUrl))
            {
                return _imageUrl;
            }

            if (IsUsableLocalPath(_emojiUrl))
            {
                return _emojiUrl;
            }

            return _localPath ?? _imageUrl ?? _emojiUrl;
        }
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

    private bool SetField<T>(ref T field, T value, string name)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        return true;
    }

    private static bool IsUsableLocalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}
