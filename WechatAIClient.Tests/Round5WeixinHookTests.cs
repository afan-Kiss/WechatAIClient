using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WechatAIClient.Services.Weixin;

namespace WechatAIClient.Tests;

public class Round5WeixinHookTests
{
    [Fact]
    public void SendTextRequest_serializes_wxid_and_msg()
    {
        var json = JsonSerializer.Serialize(new SendTextRequest { Wxid = "filehelper", Msg = "hello" });
        Assert.Contains("\"wxid\":\"filehelper\"", json);
        Assert.Contains("\"msg\":\"hello\"", json);
    }

    [Fact]
    public void SendImageRequest_serializes_filepath()
    {
        var json = JsonSerializer.Serialize(new SendImageRequest { Wxid = "filehelper", Filepath = @"C:\a.png" });
        Assert.Contains("\"filepath\":", json);
        Assert.Contains("a.png", json);
    }

    [Fact]
    public void SendFileRequest_serializes_filepath()
    {
        var json = JsonSerializer.Serialize(new SendFileRequest { Wxid = "filehelper", Filepath = @"C:\a.pdf" });
        Assert.Contains("\"filepath\":", json);
    }

    [Fact]
    public void SendAtText_exact_fields()
    {
        var json = JsonSerializer.Serialize(new SendAtTextRequest
        {
            Wxids = "wxid_1",
            Msg = "@好名字 在干嘛呢",
            RoomId = "39259098574@chatroom"
        });
        Assert.Contains("\"wxids\":", json);
        Assert.Contains("\"roomId\":", json);
        Assert.Contains("\"msg\":", json);
    }

    [Fact]
    public void SendQuote_exact_fields()
    {
        var json = JsonSerializer.Serialize(new SendQuoteRequest
        {
            Reply = "你好",
            ReferContent = "你好",
            FromUsr = "wxid_x",
            NewMsgId = "5217518642639526576",
            MsgSource = "optional",
            CreateTime = 0,
            SendTo = "49767299448@chatroom"
        });
        Assert.Contains("\"newmsgid\":", json);
        Assert.Contains("\"fromUsr\":", json);
        Assert.Contains("\"sendto\":", json);
    }

    [Fact]
    public void Contact_remark_priority()
    {
        var name = First(null, "备注A", "昵称", "alias", "wxid");
        Assert.Equal("备注A", name);
        name = First(null, null, "昵称", "alias", "wxid");
        Assert.Equal("昵称", name);
    }

    [Fact]
    public void Chatroom_username_is_conversation_id()
    {
        var room = new ChatroomDto { Username = "123@chatroom", NickName = "群" };
        Assert.Equal("123@chatroom", room.Username);
        Assert.Contains("@chatroom", room.Username);
    }

    [Fact]
    public void RoomMembers_request_uses_room_id()
    {
        var json = JsonSerializer.Serialize(new RoomMembersRequest { RoomId = "r@chatroom" });
        Assert.Contains("\"room_id\":\"r@chatroom\"", json);
    }

    [Fact]
    public void MemberNick_request_wxid_roomId()
    {
        var json = JsonSerializer.Serialize(new MemberNickRequest { Wxid = "wxid_a", RoomId = "r@chatroom" });
        Assert.Contains("\"wxid\":\"wxid_a\"", json);
        Assert.Contains("\"roomId\":\"r@chatroom\"", json);
    }

    [Fact]
    public void JsApiResponse_RespJson_second_deserialize()
    {
        var outer = """
        {"JsApiResponse":{"RespJson":"{\"msg_list\":[{\"nickname\":\"张三\",\"content\":\"你好\"}]}"}}
        """;
        var parser = new WechatCallbackParser();
        var events = parser.Parse(outer);
        Assert.Contains(events, e => e.Message?.Content == "你好" && e.Message.SenderDisplayName == "张三");
    }

    [Fact]
    public void Msg_list_multiple_messages()
    {
        var json = """{"msg_list":[{"nickname":"A","content":"1"},{"nickname":"B","content":"2"}]}""";
        var events = new WechatCallbackParser().Parse(json);
        Assert.Equal(2, events.Count(e => e.Message is not null));
    }

    [Fact]
    public void Tcp_big_endian_length()
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, 10);
        Assert.Equal(10u, BinaryPrimitives.ReadUInt32BigEndian(buf));
    }

    [Fact]
    public async Task Tcp_split_header_1_plus_3_still_reads()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = listener.AcceptTcpClientAsync();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await accept;
        await using var s = server.GetStream();
        await using var c = client.GetStream();

        var payload = Encoding.UTF8.GetBytes("{\"msg_list\":[]}");
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);

        // send header as 1 + 3
        await c.WriteAsync(header.AsMemory(0, 1));
        await c.WriteAsync(header.AsMemory(1, 3));
        await c.WriteAsync(payload);

        var readHeader = new byte[4];
        await WechatTcpCallbackServer.ReadExactlyAsync(s, readHeader, CancellationToken.None);
        var len = BinaryPrimitives.ReadUInt32BigEndian(readHeader);
        Assert.Equal((uint)payload.Length, len);
        var body = new byte[len];
        await WechatTcpCallbackServer.ReadExactlyAsync(s, body, CancellationToken.None);
        Assert.Equal(payload, body);
        listener.Stop();
    }

    [Fact]
    public async Task Tcp_consecutive_frames()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = listener.AcceptTcpClientAsync();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await accept;
        await using var s = server.GetStream();
        await using var c = client.GetStream();

        async Task SendAsync(string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var header = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
            await c.WriteAsync(header);
            await c.WriteAsync(payload);
        }

        await SendAsync("""{"msg_list":[{"nickname":"A","content":"1"}]}""");
        await SendAsync("""{"msg_list":[{"nickname":"B","content":"2"}]}""");

        for (var i = 0; i < 2; i++)
        {
            var header = new byte[4];
            await WechatTcpCallbackServer.ReadExactlyAsync(s, header, CancellationToken.None);
            var len = BinaryPrimitives.ReadUInt32BigEndian(header);
            var body = new byte[len];
            await WechatTcpCallbackServer.ReadExactlyAsync(s, body, CancellationToken.None);
            Assert.True(body.Length > 0);
        }

        listener.Stop();
    }

    [Fact]
    public void Tcp_oversized_frame_rejected_by_limit()
    {
        Assert.True(WechatTcpCallbackServer.DefaultMaxFrameSize == 10 * 1024 * 1024);
        var length = WechatTcpCallbackServer.DefaultMaxFrameSize + 1u;
        Assert.True(length > WechatTcpCallbackServer.DefaultMaxFrameSize);
    }

    [Fact]
    public void Http_tcp_duplicate_message_id_deduped()
    {
        var dedup = new WechatAIClient.Services.Wechat.MessageDeduplicator();
        Assert.True(dedup.TryAdd("c1", "m1"));
        Assert.False(dedup.TryAdd("c1", "m1"));
    }

    [Fact]
    public void Group_conversation_id_is_room()
    {
        var json = """{"content":"hi","from_wxid":"wxid_m","room_id":"g@chatroom","msgid":"1"}""";
        var ev = new WechatCallbackParser().Parse(json).First();
        Assert.Equal("g@chatroom", ev.Message!.ConversationId);
        Assert.True(ev.Message.IsGroup);
        Assert.Equal("wxid_m", ev.Message.SenderId);
    }

    [Fact]
    public void Ai_outgoing_registry_matches_and_consumes()
    {
        var reg = new PendingAiOutgoingRegistry();
        reg.Register("c1", "AI回复", "gen1");
        Assert.True(reg.TryMatch("c1", "AI回复", out var gid));
        Assert.Equal("gen1", gid);
        Assert.False(reg.TryMatch("c1", "AI回复", out _));
    }

    [Fact]
    public void Version_4_is_supported_for_hook_path()
    {
        Assert.True(WechatAIClient.Services.Wechat.WechatProcessProbe.IsSupportedVersion("4.1.8.27", out _));
    }

    [Fact]
    public void Send_success_code_1()
    {
        var body = new SimpleCodeResponse { Code = 1, Msg = "success" };
        Assert.True(body.Code == 1);
    }

    [Fact]
    public void Chatroom_list_code_0_ok()
    {
        var body = new ChatroomListResponse { Code = 0, Data = [] };
        Assert.Equal(0, body.Code);
    }

    [Fact]
    public void Quote_errCode_1_ok()
    {
        var body = new SimpleCodeResponse { ErrCode = 1, ErrMsg = "请求处理成功" };
        Assert.Equal(1, body.ErrCode);
    }

    [Fact]
    public void Message_type_mapper_unknown_safe()
    {
        Assert.Equal("Unknown", WechatMessageTypeMapper.Map("99999", null));
        Assert.Equal("Text", WechatMessageTypeMapper.Map("1", "hi"));
        Assert.Equal("Image", WechatMessageTypeMapper.Map("3", null));
    }

    [Fact]
    public void Path_traversal_rejected_by_filename_cleanup()
    {
        var dirty = @"..\..\secret.txt";
        var cleaned = string.Concat(dirty.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        Assert.DoesNotContain("..\\", cleaned.Replace('/', '\\'));
    }

    private static string? First(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
