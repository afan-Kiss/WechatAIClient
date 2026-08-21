using System.Text.Json.Serialization;

namespace WechatAIClient.Services.Weixin;

public sealed class LocalApiResult<T>
{
    public bool Success { get; init; }
    public int HttpStatus { get; init; }
    public int? ApiCode { get; init; }
    public string? ApiMessage { get; init; }
    public T? Data { get; init; }
    public string? ExceptionType { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class SendTextRequest
{
    [JsonPropertyName("wxid")]
    public string Wxid { get; set; } = "";

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "";
}

public sealed class SendImageRequest
{
    [JsonPropertyName("wxid")]
    public string Wxid { get; set; } = "";

    [JsonPropertyName("filepath")]
    public string Filepath { get; set; } = "";
}

public sealed class SendFileRequest
{
    [JsonPropertyName("wxid")]
    public string Wxid { get; set; } = "";

    [JsonPropertyName("filepath")]
    public string Filepath { get; set; } = "";
}

public sealed class SendAtTextRequest
{
    [JsonPropertyName("wxids")]
    public string Wxids { get; set; } = "";

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "";

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";
}

public sealed class SendQuoteRequest
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";

    [JsonPropertyName("referContent")]
    public string ReferContent { get; set; } = "";

    [JsonPropertyName("fromUsr")]
    public string FromUsr { get; set; } = "";

    [JsonPropertyName("newmsgid")]
    public string NewMsgId { get; set; } = "";

    [JsonPropertyName("msgSource")]
    public string? MsgSource { get; set; }

    [JsonPropertyName("createTime")]
    public long CreateTime { get; set; }

    [JsonPropertyName("sendto")]
    public string SendTo { get; set; } = "";
}

public sealed class RoomMembersRequest
{
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = "";
}

public sealed class MemberNickRequest
{
    [JsonPropertyName("wxid")]
    public string Wxid { get; set; } = "";

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";
}

public sealed class ContactListResponse
{
    [JsonPropertyName("friend_count")]
    public int FriendCount { get; set; }

    [JsonPropertyName("friend_list")]
    public List<ContactDto>? FriendList { get; set; }
}

public sealed class ContactDto
{
    [JsonPropertyName("wxid")]
    public string? Wxid { get; set; }

    [JsonPropertyName("nick_name")]
    public string? NickName { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("small_head_url")]
    public string? SmallHeadUrl { get; set; }

    [JsonPropertyName("big_head_url")]
    public string? BigHeadUrl { get; set; }
}

public sealed class ChatroomListResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("data")]
    public List<ChatroomDto>? Data { get; set; }
}

public sealed class ChatroomDto
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("nick_name")]
    public string? NickName { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("small_head_url")]
    public string? SmallHeadUrl { get; set; }

    [JsonPropertyName("big_head_url")]
    public string? BigHeadUrl { get; set; }
}

public sealed class SimpleCodeResponse
{
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("errCode")]
    public int? ErrCode { get; set; }

    [JsonPropertyName("errMsg")]
    public string? ErrMsg { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public sealed class CheckLoginResponse
{
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("errCode")]
    public int? ErrCode { get; set; }

    [JsonPropertyName("errMsg")]
    public string? ErrMsg { get; set; }

    [JsonPropertyName("account_wxid")]
    public string? AccountWxid { get; set; }

    [JsonPropertyName("wxid")]
    public string? Wxid { get; set; }

    [JsonPropertyName("nick_name")]
    public string? NickName { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("data")]
    public CheckLoginData? Data { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}

public sealed class CheckLoginData
{
    [JsonPropertyName("wxid")]
    public string? Wxid { get; set; }

    [JsonPropertyName("nick_name")]
    public string? NickName { get; set; }

    [JsonPropertyName("nickName")]
    public string? NickNameAlt { get; set; }
}

public sealed class RoomMembersResponse
{
    [JsonPropertyName("chatroomUserName")]
    public string? ChatroomUserName { get; set; }

    [JsonPropertyName("newChatroomData")]
    public NewChatroomData? NewChatroomData { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("errCode")]
    public int? ErrCode { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}

public sealed class NewChatroomData
{
    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("chatRoomMember")]
    public List<RoomMemberDto>? ChatRoomMember { get; set; }
}

public sealed class RoomMemberDto
{
    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("nickName")]
    public string? NickName { get; set; }

    [JsonPropertyName("bigHeadImgUrl")]
    public string? BigHeadImgUrl { get; set; }

    [JsonPropertyName("smallHeadImgUrl")]
    public string? SmallHeadImgUrl { get; set; }

    [JsonPropertyName("inviterUserName")]
    public string? InviterUserName { get; set; }
}

public sealed class MemberNickResponse
{
    [JsonPropertyName("account_wxid")]
    public string? AccountWxid { get; set; }

    [JsonPropertyName("errCode")]
    public int? ErrCode { get; set; }

    [JsonPropertyName("errMsg")]
    public string? ErrMsg { get; set; }

    [JsonPropertyName("data")]
    public MemberNickData? Data { get; set; }
}

public sealed class MemberNickData
{
    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("nickName")]
    public string? NickName { get; set; }

    [JsonPropertyName("bigHeadImgUrl")]
    public string? BigHeadImgUrl { get; set; }

    [JsonPropertyName("smallHeadImgUrl")]
    public string? SmallHeadImgUrl { get; set; }
}

public sealed class DownloadImgRequest
{
    [JsonPropertyName("MsgId")]
    public string? MsgId { get; set; }

    [JsonPropertyName("from_user")]
    public string? FromUser { get; set; }

    [JsonPropertyName("FromUserName")]
    public string? FromUserName { get; set; }

    [JsonPropertyName("to_user")]
    public string? ToUser { get; set; }

    [JsonPropertyName("ToUserName")]
    public string? ToUserName { get; set; }

    [JsonPropertyName("start_pos")]
    public long? StartPos { get; set; }

    [JsonPropertyName("total_len")]
    public long? TotalLen { get; set; }

    [JsonPropertyName("data_len")]
    public long? DataLen { get; set; }

    [JsonPropertyName("compress_type")]
    public int? CompressType { get; set; }

    [JsonPropertyName("attachid")]
    public string? AttachId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}

public sealed class DownloadFileRequest
{
    [JsonPropertyName("from_user")]
    public string? FromUser { get; set; }

    [JsonPropertyName("total_len")]
    public long? TotalLen { get; set; }

    [JsonPropertyName("MsgId")]
    public string? MsgId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("attachid")]
    public string? AttachId { get; set; }

    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}
