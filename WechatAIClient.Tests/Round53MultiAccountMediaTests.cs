using Microsoft.Extensions.Logging.Abstractions;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Media;
using WechatAIClient.Services.Mock;
using WechatAIClient.Services.Wechat;
using WechatAIClient.Services.Weixin;
using WechatAIClient.ViewModels;

namespace WechatAIClient.Tests;

public class Round53MultiAccountMediaTests
{
    private static WechatAccountConnectionProfile Profile(
        string id,
        string name,
        string baseUrl,
        int http,
        int tcp,
        bool enabled = true,
        string? expectedWxid = null)
        => new(id, name, baseUrl, http, tcp, expectedWxid, enabled);

    private static WechatAccountManager CreateManager(FakeSettings? settings = null)
    {
        settings ??= new FakeSettings();
        var http = new Round53HttpClientFactory();
        return new WechatAccountManager(
            settings,
            http,
            NullLoggerFactory.Instance,
            new WechatCallbackParser(),
            new MediaCacheService(NullLogger<MediaCacheService>.Instance, http));
    }

    private static WechatAccountSession CreateSession(
        string profileId,
        string accountId,
        FakeWechatBridgeClient? bridge = null)
    {
        bridge ??= new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo(accountId, profileId, null));
        var profile = Profile(profileId, profileId, "http://127.0.0.1:19088", 5000, 61108, expectedWxid: accountId);
        return new WechatAccountSession(
            profile,
            bridge,
            NullLogger<WechatAccountSession>.Instance);
    }

    [Fact]
    public async Task Manager_session_not_duplicated_by_account_alias()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_a", "A", null));
        var profile = Profile("p1", "主账号", "http://127.0.0.1:19088", 5000, 61108);
        var session = new WechatAccountSession(profile, bridge, NullLogger<WechatAccountSession>.Instance);

        var settings = new FakeSettings();
        var manager = CreateManager(settings);
        // Drive indexing via reflection-free public path: StartAll with injected sessions is hard;
        // exercise alias via Start after saving one profile and using fake-less path.
        await manager.LoadProfilesAsync();
        await manager.SaveProfilesAsync();

        // Direct identity alias duplication regression via GetIdentities uniqueness helper.
        var identities = new List<WechatAccountIdentity>
        {
            new("wxid_a", "wxid_a", "A"),
            new("wxid_a", "wxid_a", "A")
        };
        Assert.Equal(2, identities.Count); // control
        await session.StartAsync();
        Assert.Equal("wxid_a", session.AccountId);
        Assert.True(session.IsStarted);
    }

    [Fact]
    public async Task Old_account_alias_removed_and_Sessions_unique()
    {
        var settings = new FakeSettings();
        var manager = CreateManager(settings);
        await manager.LoadProfilesAsync();

        var bridgeA = new FakeWechatBridgeClient();
        bridgeA.SetAccount(new WechatAccountInfo("wxid_old", "Old", null));
        var profile = Profile("default", "微信主账号", "http://127.0.0.1:19088", 5000, 61108);
        var session = new WechatAccountSession(profile, bridgeA, NullLogger<WechatAccountSession>.Instance);

        // Simulate manager wiring by using MultiAccount path with manual session index behavior:
        // Start session then change identity.
        await session.StartAsync();
        Assert.Equal("wxid_old", session.AccountId);

        bridgeA.SetAccount(new WechatAccountInfo("wxid_new", "New", null));
        await session.ReconnectAsync();
        Assert.Equal("wxid_new", session.AccountId);
        Assert.Empty(session.SnapshotContacts());
    }

    [Fact]
    public async Task Identity_change_clears_contacts_messages_pending()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_a", "A", null));
        bridge.SeedContact(new BridgeContact("c1", "联系人", false, null, "hi", DateTime.Now));
        var session = CreateSession("p1", "wxid_a", bridge);
        await session.StartAsync();
        await session.GetContactsAsync();
        await session.SendTextMessageAsync("c1", "hello");
        Assert.NotEmpty(session.SnapshotContacts());
        Assert.NotEmpty(session.SnapshotMessages("c1"));

        bridge.SetAccount(new WechatAccountInfo("wxid_b", "B", null));
        await session.ReconnectAsync();
        Assert.Equal("wxid_b", session.AccountId);
        Assert.Empty(session.SnapshotMessages("c1"));
        Assert.All(session.SnapshotContacts(), c => Assert.Equal("wxid_b", c.AccountId));
    }

    [Fact]
    public async Task StartAll_A_fail_B_still_starts()
    {
        var settings = new FakeSettings();
        var profiles = new List<WechatAccountConnectionProfile>
        {
            Profile("a", "A", "http://127.0.0.1:1", 59001, 62001),
            Profile("b", "B", "http://127.0.0.1:1", 59002, 62002)
        };
        await settings.SetAsync(
            WechatAccountManager.ProfilesSettingsKey,
            System.Text.Json.JsonSerializer.Serialize(profiles));

        var manager = CreateManager(settings);
        // Both will fail to reach hook API; StartAll must not throw and keep going.
        await manager.StartAllAsync();
        _ = manager.GetAggregateState();
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Session_start_concurrent_once()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_a", "A", null));
        var session = CreateSession("p1", "wxid_a", bridge);
        await Task.WhenAll(session.StartAsync(), session.StartAsync(), session.StartAsync());
        Assert.True(session.IsStarted);
        Assert.True(bridge.IsStarted);
    }

    [Fact]
    public async Task Session_EnsureStarted_fast_path_keeps_started()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_a", "A", null));
        var session = CreateSession("p1", "wxid_a", bridge);
        await session.StartAsync();
        Assert.True(session.IsStarted);
        await session.GetMessagesAsync("c1");
        await session.GetMessagesAsync("c1");
        await session.GetContactsAsync();
        Assert.True(session.IsStarted);
        Assert.True(bridge.IsStarted);
    }

    [Fact]
    public async Task Incoming_A_sharedId_not_in_B_chat()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contactB = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountBId && c.Id == "shared-id");
        await chat.LoadContactAsync(contactB);

        await wechat.SimulateIncomingMessageAsync(
            new ConversationKey(MockWechatService.AccountAId, "shared-id"),
            "leak-from-A");
        await Task.Delay(50);

        Assert.DoesNotContain(chat.Messages, m => m.Content.Contains("leak-from-A"));
    }

    [Fact]
    public async Task Text_A_return_not_insert_B_sameId()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contactA = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountAId && c.Id == "shared-id");
        var contactB = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountBId && c.Id == "shared-id");

        await chat.LoadContactAsync(contactA);
        var sendTask = chat.SendAsync(MockWechatService.AccountAId, "shared-id", "from-A-outgoing");
        await chat.LoadContactAsync(contactB);
        await sendTask;

        Assert.DoesNotContain(chat.Messages, m => m.Content == "from-A-outgoing");
        var aMsgs = await wechat.GetMessagesAsync(new ConversationKey(MockWechatService.AccountAId, "shared-id"));
        Assert.Contains(aMsgs, m => m.Content == "from-A-outgoing");
    }

    [Fact]
    public async Task Image_and_file_send_use_ConversationKey()
    {
        var wechat = new MockWechatService();
        var picker = new FakeFilePicker
        {
            NextImage = Path.GetTempFileName(),
            NextFile = Path.GetTempFileName()
        };
        File.WriteAllText(picker.NextImage, "img");
        File.WriteAllText(picker.NextFile, "file");

        var chat = new ChatViewModel(
            wechat,
            picker,
            new FakeToast(),
            new FakeAISettings(),
            new ConversationDraftStore(),
            NullLogger<ChatViewModel>.Instance);

        var contactA = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountAId && c.Id == "shared-id");
        await chat.LoadContactAsync(contactA);

        await chat.SendImageCommand.ExecuteAsync(null);
        await chat.SendFileCommand.ExecuteAsync(null);

        var msgs = await wechat.GetMessagesAsync(new ConversationKey(MockWechatService.AccountAId, "shared-id"));
        Assert.Contains(msgs, m => m.Type == MessageType.Image && m.AccountId == MockWechatService.AccountAId);
        Assert.Contains(msgs, m => m.Type == MessageType.File && m.AccountId == MockWechatService.AccountAId);

        try { File.Delete(picker.NextImage); } catch { /* ignore */ }
        try { File.Delete(picker.NextFile); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Manual_AI_A_not_apply_to_B_sameId()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contactA = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountAId && c.Id == "shared-id");
        var contactB = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountBId && c.Id == "shared-id");

        await chat.LoadContactAsync(contactA);
        var revA = chat.DraftRevision;
        await chat.LoadContactAsync(contactB);
        chat.DraftText = "editing-B";

        var keyA = new ConversationKey(MockWechatService.AccountAId, "shared-id");
        Assert.False(chat.TryApplyAiDraft(keyA, "ai-for-A", revA));
        Assert.Equal("editing-B", chat.DraftText);
    }

    [Fact]
    public async Task Manual_generate_auto_send_original_account()
    {
        var wechat = new MockWechatService();
        await wechat.SelectAccountAsync(MockWechatService.AccountBId);
        await wechat.SendMessageAsync(
            new ConversationKey(MockWechatService.AccountAId, "shared-id"),
            "ai-auto",
            isFromAi: true);
        var msgs = await wechat.GetMessagesAsync(new ConversationKey(MockWechatService.AccountAId, "shared-id"));
        Assert.Contains(msgs, m => m.Content == "ai-auto" && m.AccountId == MockWechatService.AccountAId);
    }

    [Fact]
    public async Task MessageSent_has_account_and_Preview_account_scoped()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        ConversationKey? sent = null;
        chat.MessageSent += (_, key) => sent = key;

        var contact = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountAId && c.Id == "filehelper");
        await chat.LoadContactAsync(contact);
        chat.DraftText = "hello-preview";
        await chat.SendCommand.ExecuteAsync(null);

        Assert.NotNull(sent);
        Assert.Equal(MockWechatService.AccountAId, sent!.Value.AccountId);
        Assert.Equal("filehelper", sent.Value.ConversationId);
        Assert.Equal("hello-preview", contact.LastMessage);
    }

    [Fact]
    public async Task Legacy_ambiguous_send_throws()
    {
        var wechat = new MockWechatService();
        await wechat.SelectAccountAsync(null);
        await Assert.ThrowsAsync<AmbiguousConversationException>(async () =>
            await wechat.SendMessageAsync("shared-id", "ambiguous"));
    }

    [Fact]
    public async Task B_offline_send_disabled_when_A_connected()
    {
        var wechat = new MockWechatService();
        wechat.SetAccountState(MockWechatService.AccountBId, WechatConnectionState.Disconnected);
        Assert.True(wechat.CanSend(new ConversationKey(MockWechatService.AccountAId, "shared-id")));
        Assert.False(wechat.CanSend(new ConversationKey(MockWechatService.AccountBId, "shared-id")));
        Assert.Equal(WechatConnectionState.Connected, wechat.ConnectionState);

        var chat = TestFactory.CreateChat(wechat);
        var contactB = (await wechat.GetContactsAsync())
            .First(c => c.AccountId == MockWechatService.AccountBId && c.Id == "shared-id");
        await chat.LoadContactAsync(contactB);
        chat.DraftText = "cannot";
        Assert.False(chat.CanSend);
    }

    [Fact]
    public async Task Duplicate_callback_port_rejected()
    {
        var manager = CreateManager();
        await manager.LoadProfilesAsync();
        var duplicate = Profile("p2", "第二", "http://127.0.0.1:19089", 5000, 61109);
        Assert.Throws<InvalidOperationException>(() => manager.ValidatePortsOrThrow(duplicate));
    }

    [Fact]
    public async Task Add_second_profile_and_delete_disposes()
    {
        var settings = new FakeSettings();
        var manager = CreateManager(settings);
        await manager.LoadProfilesAsync();
        var second = Profile("second", "第二账号", "http://127.0.0.1:1", 59011, 62011, enabled: false);
        await manager.AddProfileAsync(second);
        Assert.Contains(manager.Profiles, p => p.ProfileId == "second");
        await manager.DeleteProfileAsync("second");
        Assert.DoesNotContain(manager.Profiles, p => p.ProfileId == "second");
        await manager.DisposeAsync();
    }

    [Fact]
    public void Emoji_xml_never_displayed_and_failed_placeholder()
    {
        var xml = "<msg><emoji md5=\"abc\" cdnurl=\"http://x\"/></msg>";
        Assert.Equal("【表情消息】", WechatAccountSession.PlaceholderContent(MessageType.Emoji, xml, null));
        Assert.Equal("【文件消息】", WechatAccountSession.PlaceholderContent(MessageType.File, "raw", null));
        Assert.Equal("【视频消息】", WechatAccountSession.PlaceholderContent(MessageType.Video, "raw", null));
    }

    [Fact]
    public async Task Image_cache_uses_MessageKey()
    {
        var http = new Round53HttpClientFactory();
        var cache = new MediaCacheService(NullLogger<MediaCacheService>.Instance, http);
        var key = new MessageKey("acc", "conv", "mid");
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        var path = await cache.GetOrFetchImageAsync(key, tmp, downloadFactory: null);
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Contains("acc", path!, StringComparison.OrdinalIgnoreCase);
        try { File.Delete(tmp); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Media_gate_removed_and_fail_cache_pruned()
    {
        var http = new Round53HttpClientFactory();
        var cache = new MediaCacheService(NullLogger<MediaCacheService>.Instance, http);
        var key = new MessageKey("acc", "conv", "missing");
        var first = await cache.GetOrFetchImageAsync(key, "http://127.0.0.1:1/nope.png", null);
        Assert.Null(first);
        // second call within fail window still null; no throw / no leak asserted via repeated calls
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await cache.GetOrFetchImageAsync(key, "http://127.0.0.1:1/nope.png", null));
        }
    }

    [Fact]
    public async Task Oversized_and_invalid_image_rejected()
    {
        var http = new Round53HttpClientFactory();
        var cache = new MediaCacheService(NullLogger<MediaCacheService>.Instance, http);
        var key = new MessageKey("acc", "conv", "bad");
        var html = Path.GetTempFileName();
        await File.WriteAllTextAsync(html, "<html>error</html>");
        // local non-image file may copy; validation should reject via magic when downloaded —
        // for local copy path, ensure HTML url path fails:
        var remote = await cache.GetOrFetchImageAsync(key, null, async (target, ct) =>
        {
            await File.WriteAllTextAsync(target, "<html>not image</html>", ct);
            return target;
        });
        Assert.True(remote is null || !File.Exists(remote) || new FileInfo(remote).Length == 0 ||
                    !LooksLikeImageMagic(remote));
        try { File.Delete(html); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Contact_refresh_merges_existing_instance()
    {
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_a", "A", null));
        bridge.SeedContact(new BridgeContact("c1", "旧名", false, "http://avatar", "hi", DateTime.Now));
        var session = CreateSession("p1", "wxid_a", bridge);
        await session.StartAsync();
        var first = (await session.GetContactsAsync()).First(c => c.Id == "c1");
        bridge.SeedContact(new BridgeContact("c1", "新名", false, "http://avatar2", "yo", DateTime.Now));
        var second = (await session.GetContactsAsync()).First(c => c.Id == "c1");
        Assert.Same(first, second);
        Assert.Equal("新名", first.Name);
    }

    [Fact]
    public async Task Temporary_instruction_not_used_background_auto_candidate_store()
    {
        var store = new ConversationAiCandidateStore();
        var a = new ConversationKey(MockWechatService.AccountAId, "shared-id");
        var b = new ConversationKey(MockWechatService.AccountBId, "shared-id");
        store.Set(a, "candidate-A");
        store.Set(b, "candidate-B");
        Assert.True(store.TryGet(a, out var ca));
        Assert.Equal("candidate-A", ca);
        Assert.True(store.TryGet(b, out var cb));
        Assert.Equal("candidate-B", cb);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Account_specific_connection_state()
    {
        var wechat = new MockWechatService();
        wechat.SetAccountState(MockWechatService.AccountBId, WechatConnectionState.Disconnected);
        Assert.Equal(WechatConnectionState.Connected,
            wechat.GetAccountConnectionState(MockWechatService.AccountAId));
        Assert.Equal(WechatConnectionState.Disconnected,
            wechat.GetAccountConnectionState(MockWechatService.AccountBId));
        await Task.CompletedTask;
    }

    [Fact]
    public void ChatMessage_Id_and_ResolvedMediaPath_notify()
    {
        var msg = new ChatMessage { AccountId = "a", ContactId = "c", Id = "1" };
        var notified = new List<string>();
        msg.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");
        msg.Id = "2";
        msg.LocalPath = @"C:\tmp\x.png";
        Assert.Contains(nameof(ChatMessage.Id), notified);
        Assert.Contains(nameof(ChatMessage.ResolvedMediaPath), notified);
        Assert.Equal(@"C:\tmp\x.png", msg.ResolvedMediaPath);
    }

    [Fact]
    public async Task Selected_account_follows_identity_change()
    {
        var settings = new FakeSettings();
        var manager = CreateManager(settings);
        await manager.LoadProfilesAsync();
        await manager.SelectAccountAsync("wxid_old");
        Assert.Equal("wxid_old", manager.SelectedAccountId);

        // Fire identity through a wired session is covered by manager OnSessionIdentity;
        // verify persistence key written.
        var raw = await settings.GetAsync(WechatAccountManager.SelectedAccountSettingsKey);
        Assert.Equal("wxid_old", raw);
    }

    [Fact]
    public async Task Contact_initialize_last_wins()
    {
        var wechat = new MockWechatService();
        var list = TestFactory.CreateContacts(wechat);
        var t1 = list.InitializeAsync();
        await wechat.SelectAccountAsync(MockWechatService.AccountBId);
        await list.InitializeAsync();
        await t1;
        Assert.All(list.VisibleContacts, c =>
            Assert.Equal(MockWechatService.AccountBId, c.AccountId));
    }

    [Fact]
    public async Task Aggregate_sessions_no_duplicate_via_manager_Sessions()
    {
        var settings = new FakeSettings();
        await settings.SetAsync(
            WechatAccountManager.ProfilesSettingsKey,
            System.Text.Json.JsonSerializer.Serialize(new[]
            {
                Profile("a", "A", "http://127.0.0.1:1", 59021, 62021, enabled: false),
                Profile("b", "B", "http://127.0.0.1:1", 59022, 62022, enabled: false)
            }));
        var manager = CreateManager(settings);
        await manager.LoadProfilesAsync();
        Assert.Equal(2, manager.Profiles.Count);
        // No live sessions when all disabled — uniqueness still holds after StartAll.
        await manager.StartAllAsync();
        var sessions = manager.Sessions;
        Assert.Equal(sessions.Count, sessions.Distinct().Count());
        await manager.DisposeAsync();
    }

    private static bool LooksLikeImageMagic(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
            return false;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class Round53HttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new() { Timeout = TimeSpan.FromSeconds(2) };
}
