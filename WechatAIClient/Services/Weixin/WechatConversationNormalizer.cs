namespace WechatAIClient.Services.Weixin;

/// <summary>
/// Account-aware conversation id normalization. Parser keeps From/To; Bridge applies this.
/// </summary>
public static class WechatConversationNormalizer
{
    public static void Apply(WechatIncomingMessage msg, string? accountWxid)
    {
        if (msg.IsGroup ||
            (!string.IsNullOrWhiteSpace(msg.RoomId) &&
             msg.RoomId.Contains("@chatroom", StringComparison.OrdinalIgnoreCase)))
        {
            var room = msg.RoomId ?? msg.GroupId ?? msg.ConversationId;
            msg.IsGroup = true;
            msg.RoomId = room;
            msg.GroupId = room;
            msg.ConversationId = room ?? msg.ConversationId;
            return;
        }

        var from = msg.FromWxid ?? msg.SenderId;
        var to = msg.ToWxid;

        if (!string.IsNullOrWhiteSpace(accountWxid) &&
            string.Equals(from, accountWxid, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(to) &&
            !to.Contains("@chatroom", StringComparison.OrdinalIgnoreCase))
        {
            msg.ConversationId = to;
            msg.IsFromMe = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(from) &&
            (string.IsNullOrWhiteSpace(accountWxid) ||
             !string.Equals(from, accountWxid, StringComparison.Ordinal)))
        {
            msg.ConversationId = from;
            return;
        }

        if (!string.IsNullOrWhiteSpace(to) &&
            !to.Contains("@chatroom", StringComparison.OrdinalIgnoreCase))
        {
            msg.ConversationId = to;
        }
    }
}
