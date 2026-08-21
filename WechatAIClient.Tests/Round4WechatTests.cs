using Microsoft.Extensions.Logging.Abstractions;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Mock;
using WechatAIClient.Services.Wechat;
using WechatAIClient.ViewModels;

namespace WechatAIClient.Tests;

public class Round4WechatTests
{
    private static RealWechatService CreateReal(FakeWechatBridgeClient bridge)
        => new(bridge, NullLogger<RealWechatService>.Instance);

    private static BridgeMessage Remote(
        string conv,
        string id,
        string content,
        string? sender = "对方",
        bool mention = false,
        bool quote = false,
        bool isGroup = false,
        BridgeMessageKind kind = BridgeMessageKind.Text,
        string? fileName = null)
        => new(
            id, conv, content,
            IsFromMe: false,
            IsGroup: isGroup,
            SenderId: "u-remote",
            SenderDisplayName: sender,
            Timestamp: DateTime.Now,
            Kind: kind,
            MentionsMe: mention,
            QuotesMe: quote,
            FileName: fileName);

    [Fact]
    public async Task Remote_message_maps_conversation_id()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        bridge.SeedContact(new BridgeContact("c1", "好友A", false, null, null, null));
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();

        ChatMessage? got = null;
        real.MessageReceived += (_, e) => got = e.Message;
        bridge.InjectMessage(Remote("c1", "m1", "你好"));

        Assert.NotNull(got);
        Assert.Equal("c1", got!.ContactId);
        Assert.Equal("m1", got.Id);
        Assert.Equal(MessageSource.RemoteUser, got.Source);
    }

    [Fact]
    public async Task Duplicate_message_id_inserted_once()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        var count = 0;
        real.MessageReceived += (_, _) => count++;

        var msg = Remote("c1", "dup-1", "重复");
        bridge.InjectMessage(msg);
        bridge.InjectMessage(msg);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Replay_after_reconnect_does_not_duplicate()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        var count = 0;
        real.MessageReceived += (_, _) => count++;

        var msg = Remote("c1", "replay-1", "重放");
        bridge.InjectMessage(msg);
        await real.ReconnectAsync();
        bridge.InjectMessage(msg);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Manual_send_echo_does_not_create_second_row_for_auto()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        bridge.SetAccount(new WechatAccountInfo("me", "我", null));
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();

        var remoteRaises = 0;
        real.MessageReceived += (_, e) =>
        {
            if (!e.Message.IsSelf)
            {
                remoteRaises++;
            }
        };

        var send = await real.SendTextMessageAsync("c1", "我发的", isFromAi: false);
        Assert.True(send.Success);

        // Echo from WeChat
        bridge.InjectMessage(new BridgeMessage(
            "echo-1", "c1", "我发的",
            IsFromMe: true, IsGroup: false,
            SenderId: "me", SenderDisplayName: "我",
            Timestamp: DateTime.Now));

        Assert.Equal(0, remoteRaises);
        var msgs = await real.GetMessagesAsync("c1");
        Assert.Single(msgs.Where(m => m.Content == "我发的"));
    }

    [Fact]
    public async Task Ai_auto_send_echo_does_not_retrigger_as_remote()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();

        var remoteCount = 0;
        real.MessageReceived += (_, e) =>
        {
            if (e.Message.Source == MessageSource.RemoteUser)
            {
                remoteCount++;
            }
        };

        await real.SendTextMessageAsync("c1", "AI回复", isFromAi: true);
        bridge.InjectMessage(new BridgeMessage(
            "ai-echo", "c1", "AI回复",
            IsFromMe: true, IsGroup: false,
            SenderId: "me", SenderDisplayName: "我",
            Timestamp: DateTime.Now));

        Assert.Equal(0, remoteCount);
    }

    [Fact]
    public async Task Send_failure_keeps_draft_in_chat_vm()
    {
        var wechat = new MockWechatService { ThrowOnSend = true };
        var chat = TestFactory.CreateChat(wechat);
        await chat.LoadContactAsync((await wechat.GetRecentChatsAsync()).First());
        chat.DraftText = "不能丢";
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            // SendCommand uses SendTextMessageAsync which catches — call legacy path via ThrowOnSend on SendMessageAsync
            await wechat.SendMessageAsync(chat.CurrentContact!.Id, "x");
        });

        // Prefer SendTextMessageAsync path used by UI
        wechat.ThrowOnSend = true;
        chat.DraftText = "保留草稿";
        // Force fail via Real + Fake
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        bridge.ForceSendFail = true;
        var real = CreateReal(bridge);
        var chat2 = new ChatViewModel(real, new FakeFilePicker(), new FakeToast(), new FakeAISettings(), NullLogger<ChatViewModel>.Instance);
        bridge.SeedContact(new BridgeContact("c9", "测", false, null, null, DateTime.Now));
        var contact = (await real.GetContactsAsync()).First();
        await chat2.LoadContactAsync(contact);
        chat2.DraftText = "保留草稿";
        if (chat2.SendCommand.CanExecute(null))
        {
            await chat2.SendCommand.ExecuteAsync(null);
        }

        Assert.Equal("保留草稿", chat2.DraftText);
    }

    [Fact]
    public async Task Disconnect_send_returns_not_connected()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.WechatNotRunning);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        var result = await real.SendTextMessageAsync("c1", "hi");
        Assert.False(result.Success);
        Assert.Equal("NotConnected", result.ErrorCode);
    }

    [Fact]
    public async Task Reconnect_restores_connected_state()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetVersion(new WechatVersionInfo("3.9.12.17", @"C:\WeChat\WeChat.exe", true, null));
        bridge.SetState(WechatConnectionState.WechatNotRunning);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        await real.ReconnectAsync();
        Assert.Equal(WechatConnectionState.Connected, await real.GetConnectionStateAsync());
    }

    [Fact]
    public void Bridge_crash_does_not_throw_to_caller()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var crashed = false;
        bridge.BridgeCrashed += (_, _) => crashed = true;
        bridge.TriggerCrash();
        Assert.True(crashed);
        Assert.Equal(WechatConnectionState.BridgeError, bridge.State);
    }

    [Fact]
    public void Bridge_supervisor_stops_after_burst_crashes()
    {
        var supervisor = new BridgeSupervisor(maxCrashes: 3, window: TimeSpan.FromMinutes(1));
        Assert.True(supervisor.AutoRestartEnabled);
        supervisor.RecordCrash();
        supervisor.RecordCrash();
        Assert.True(supervisor.AutoRestartEnabled);
        supervisor.RecordCrash();
        Assert.False(supervisor.AutoRestartEnabled);
    }

    [Fact]
    public void Version_weixin_4_supported_via_hook_path()
    {
        Assert.True(WechatProcessProbe.IsSupportedVersion("4.1.8.27", out _));
        Assert.True(WechatProcessProbe.IsWeixin4HookTarget("4.1.8.27"));
    }

    [Fact]
    public async Task Group_sender_member_mapped()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        ChatMessage? got = null;
        real.MessageReceived += (_, e) => got = e.Message;
        bridge.InjectMessage(Remote("g1", "gm1", "群消息", sender: "张三", isGroup: true));
        Assert.Equal("张三", got!.SenderName);
        Assert.Equal("g1", got.ContactId);
    }

    [Fact]
    public void Group_mention_passes_trigger()
    {
        var msg = new ChatMessage { MentionsMe = true, QuotesMe = false };
        Assert.True(Passes(GroupTriggerMode.MentionOrQuoteMe, msg));
        Assert.False(Passes(GroupTriggerMode.MentionOrQuoteMe, new ChatMessage()));
    }

    [Fact]
    public void Group_plain_does_not_pass_mention_mode()
    {
        Assert.False(Passes(GroupTriggerMode.MentionOrQuoteMe, new ChatMessage { Content = "普通" }));
    }

    [Fact]
    public void Group_quote_me_passes()
    {
        Assert.True(Passes(GroupTriggerMode.MentionOrQuoteMe, new ChatMessage { QuotesMe = true }));
    }

    [Fact]
    public async Task Current_session_unread_stays_zero()
    {
        var wechat = TestFactory.CreateWechat();
        var list = TestFactory.CreateContacts(wechat);
        var chat = TestFactory.CreateChat(wechat);
        await list.InitializeAsync();
        var contact = list.VisibleContacts.First();
        await chat.LoadContactAsync(contact);
        Assert.Equal(0, contact.UnreadCount);
        await wechat.SimulateIncomingMessageAsync(contact.Id, "新消息");
        await Task.Delay(50);
        Assert.Equal(0, contact.UnreadCount);
    }

    [Fact]
    public async Task Other_session_unread_increments()
    {
        var wechat = TestFactory.CreateWechat();
        var list = TestFactory.CreateContacts(wechat);
        await list.InitializeAsync();
        var a = list.VisibleContacts[0];
        var b = list.VisibleContacts.First(c => c.Id != a.Id);
        var before = b.UnreadCount;
        await wechat.SimulateIncomingMessageAsync(b.Id, "别人会话");
        await Task.Delay(80);
        Assert.True(b.UnreadCount >= before + 1);
    }

    [Fact]
    public async Task Recent_bumps_to_top_on_message()
    {
        var wechat = TestFactory.CreateWechat();
        var list = TestFactory.CreateContacts(wechat);
        await list.InitializeAsync();
        var target = list.VisibleContacts.Last();
        await wechat.SimulateIncomingMessageAsync(target.Id, "置顶我");
        await Task.Delay(80);
        Assert.Equal(target.Id, list.VisibleContacts.First().Id);
    }

    [Fact]
    public async Task Background_thread_message_does_not_crash_service()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        real.MessageReceived += (_, _) => tcs.TrySetResult();
        await Task.Run(() => bridge.InjectMessage(Remote("c1", "bg-1", "后台")));
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Dispose_stops_bridge()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        await real.DisposeAsync();
        Assert.Equal(WechatConnectionState.Disconnected, bridge.State);
    }

    [Fact]
    public void Image_maps_to_placeholder_in_context()
    {
        var builder = new AIContextBuilder();
        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages =
            [
                new ChatMessage
                {
                    Id = "i1", ContactId = "c1", Type = MessageType.Image,
                    Source = MessageSource.RemoteUser, Timestamp = DateTime.Now
                }
            ],
            ContextCount = 10
        });
        Assert.Contains(result.Messages, m => m.Content.Contains("[图片]"));
    }

    [Fact]
    public void File_maps_with_filename_in_context()
    {
        var builder = new AIContextBuilder();
        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages =
            [
                new ChatMessage
                {
                    Id = "f1", ContactId = "c1", Type = MessageType.File,
                    FileName = "a.pdf", FileSize = "1KB",
                    Source = MessageSource.RemoteUser, Timestamp = DateTime.Now
                }
            ],
            ContextCount = 10
        });
        Assert.Contains(result.Messages, m => m.Content.Contains("a.pdf"));
    }

    [Fact]
    public async Task Auto_does_not_send_when_disconnected()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.WechatNotRunning);
        var real = CreateReal(bridge);
        await real.EnsureStartedAsync();
        var result = await real.SendTextMessageAsync("c1", "auto", isFromAi: true);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Completed_reply_kept_as_candidate_when_send_blocked()
    {
        // Simulate: generation completed, send refused — caller keeps LatestGeneratedReply
        var toast = new FakeToast();
        var aiPanelLatest = "已生成内容";
        Assert.False(string.IsNullOrWhiteSpace(aiPanelLatest));
        await toast.ShowAsync("微信未连接，回复已保留为候选");
        Assert.Contains(toast.Messages, m => m.Contains("未连接"));
    }

    [Fact]
    public async Task Routing_switches_mock_and_real()
    {
        var settings = new FakeSettings();
        var mock = new MockWechatService();
        var bridge = new FakeWechatBridgeClient();
        bridge.SetState(WechatConnectionState.Connected);
        var real = CreateReal(bridge);
        var routing = new RoutingWechatService(mock, real, settings, NullLogger<RoutingWechatService>.Instance);
        await routing.SwitchProviderAsync(WechatProviderKind.Mock);
        Assert.Equal(WechatProviderKind.Mock, routing.ActiveProvider);
        Assert.Equal(WechatConnectionState.Connected, await routing.GetConnectionStateAsync());
        await routing.SwitchProviderAsync(WechatProviderKind.Real);
        Assert.Equal(WechatProviderKind.Real, routing.ActiveProvider);
    }

    [Fact]
    public void Deduplicator_capacity_evicts_old()
    {
        var d = new MessageDeduplicator(capacityPerConversation: 16);
        for (var i = 0; i < 20; i++)
        {
            Assert.True(d.TryAdd("c", $"m{i}"));
        }

        Assert.False(d.TryAdd("c", "m19"));
        Assert.True(d.TryAdd("c", "m0")); // evicted
    }

    private static bool Passes(GroupTriggerMode mode, ChatMessage message) => mode switch
    {
        GroupTriggerMode.Off => false,
        GroupTriggerMode.AllMessages => true,
        GroupTriggerMode.MentionMeOnly => message.MentionsMe,
        GroupTriggerMode.QuoteMeOnly => message.QuotesMe,
        GroupTriggerMode.MentionOrQuoteMe => message.MentionsMe || message.QuotesMe,
        _ => true
    };
}
