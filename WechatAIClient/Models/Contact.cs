namespace WechatAIClient.Models;

public sealed class Contact
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#7C5CFF";
    public string AvatarInitials { get; set; } = "?";
    public ContactType Type { get; set; } = ContactType.Friend;
    public string LastMessage { get; set; } = string.Empty;
    public string LastSender { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; } = DateTime.Now;
    public int UnreadCount { get; set; }
    public bool IsOnline { get; set; }
    public int MemberCount { get; set; }
}
