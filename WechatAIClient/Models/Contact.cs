using CommunityToolkit.Mvvm.ComponentModel;

namespace WechatAIClient.Models;

public partial class Contact : ObservableObject
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Owning WeChat account wxid / AccountId.</summary>
    public string AccountId { get; set; } = string.Empty;

    public string? AccountDisplayName { get; set; }

    public ConversationKey Key => new(AccountId, Id);

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _avatarColor = "#7C5CFF";

    [ObservableProperty]
    private string _avatarInitials = "?";

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private string? _avatarLocalPath;

    [ObservableProperty]
    private AvatarLoadState _avatarLoadState;

    [ObservableProperty]
    private ContactType _type = ContactType.Friend;

    [ObservableProperty]
    private string _lastMessage = string.Empty;

    [ObservableProperty]
    private string _lastSender = string.Empty;

    [ObservableProperty]
    private DateTime _lastMessageTime = DateTime.MinValue;

    [ObservableProperty]
    private bool _hasLastActivity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnreadDisplay))]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private int _memberCount;

    public string UnreadDisplay => UnreadCount > 99 ? "99+" : UnreadCount.ToString();
}
