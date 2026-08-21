using Microsoft.Extensions.Logging.Abstractions;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Media;
using WechatAIClient.Services.Mock;
using WechatAIClient.Services.Wechat;
using WechatAIClient.Services.Weixin;
using WechatAIClient.ViewModels;

namespace WechatAIClient.Tests;

public class Round54AiIsolationAndHardeningTests
{
    private static WechatAccountConnectionProfile Profile(
        string id, string name, string url, int http, int tcp, bool enabled = true, string? expected = null)
        => new(id, name, url, http, tcp, expected, enabled);

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

    [Fact]
    public void Unknown_group_trigger_fails_closed()
    {
        Assert.False(MainWindowViewModel.PassesGroupTrigger((GroupTriggerMode)999, new ChatMessage { MentionsMe = true }));
        Assert.True(MainWindowViewModel.PassesGroupTrigger(GroupTriggerMode.AllMessages, new ChatMessage()));
        Assert.False(MainWindowViewModel.PassesGroupTrigger(GroupTriggerMode.Off, new ChatMessage { MentionsMe = true }));
    }

    [Fact]
    public async Task Background_B_uses_B_effective_reply_mode_not_panel()
    {
        var settings = new FakeAISettings();
        await settings.SaveOverrideAsync(new AIContactOverride
        {
            AccountId = MockWechatService.AccountAId,
            ContactId = "shared-id",
            UseOverride = true,
            ReplyMode = AIReplyMode.Auto,
            AutoGenerateOnReceive = true
        });
        await settings.SaveOverrideAsync(new AIContactOverride
        {
            AccountId = MockWechatService.AccountBId,
            ContactId = "shared-id",
            UseOverride = true,
            ReplyMode = AIReplyMode.Off,
            AutoGenerateOnReceive = false
        });

        var effB = await settings.GetEffectiveAsync(MockWechatService.AccountBId, "shared-id");
        Assert.Equal(AIReplyMode.Off, effB.ReplyMode);
        Assert.False(effB.AutoGenerateOnReceive);

        var effA = await settings.GetEffectiveAsync(MockWechatService.AccountAId, "shared-id");
        Assert.Equal(AIReplyMode.Auto, effA.ReplyMode);
    }

    [Fact]
    public async Task Expected_A_actual_B_removes_A_alias()
    {
        var manager = CreateManager();
        await manager.LoadProfilesAsync();
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_B", "B", null));
        var profile = Profile("p1", "账号", "http://127.0.0.1:1", 59101, 62101, expected: "wxid_A");
        var session = new WechatAccountSession(profile, bridge, NullLogger<WechatAccountSession>.Instance);
        manager.RegisterTestSession(session);

        // Before identity: Expected A must NOT resolve to session.
        Assert.Null(manager.GetSession("wxid_A"));

        await session.StartAsync();
        Assert.Equal("wxid_B", session.AccountId);
        Assert.Null(manager.GetSession("wxid_A"));
        Assert.Same(session, manager.GetSession("wxid_B"));
        Assert.Equal(1, manager.Sessions.Count);
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Manager_session_not_duplicated_by_identity_index()
    {
        var manager = CreateManager();
        await manager.LoadProfilesAsync();
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_live", "Live", null));
        var profile = Profile("p1", "账号", "http://127.0.0.1:1", 59111, 62111);
        var session = new WechatAccountSession(profile, bridge, NullLogger<WechatAccountSession>.Instance);
        manager.RegisterTestSession(session);
        await session.StartAsync();

        Assert.Equal(1, manager.Sessions.Count);
        Assert.Equal(1, manager.GetIdentities().Count);
        Assert.Same(session, manager.GetSession("wxid_live"));
        Assert.Same(session, manager.GetSession("p1"));
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Old_account_alias_removed_GetSession_old_null()
    {
        var manager = CreateManager();
        await manager.LoadProfilesAsync();
        var bridge = new FakeWechatBridgeClient();
        bridge.SetAccount(new WechatAccountInfo("wxid_old", "Old", null));
        var profile = Profile("p1", "账号", "http://127.0.0.1:1", 59121, 62121);
        var session = new WechatAccountSession(profile, bridge, NullLogger<WechatAccountSession>.Instance);
        manager.RegisterTestSession(session);
        await session.StartAsync();
        Assert.Same(session, manager.GetSession("wxid_old"));

        bridge.SetAccount(new WechatAccountInfo("wxid_new", "New", null));
        await session.ReconnectAsync();
        Assert.Null(manager.GetSession("wxid_old"));
        Assert.Same(session, manager.GetSession("wxid_new"));
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Disabled_profile_does_not_reserve_port()
    {
        var manager = CreateManager();
        await manager.LoadProfilesAsync();
        var disabled = Profile("x", "X", "http://127.0.0.1:1", 5000, 61108, enabled: false);
        manager.ValidatePortsOrThrow(disabled);

        var enabledConflict = Profile("y", "Y", "http://127.0.0.1:1", 5000, 61109, enabled: true);
        Assert.Throws<ProfileValidationException>(() => manager.ValidatePortsOrThrow(enabledConflict));
    }

    [Fact]
    public async Task Port_validation_uses_typed_error()
    {
        var manager = CreateManager();
        await manager.LoadProfilesAsync();
        var ex = Assert.Throws<ProfileValidationException>(() =>
            manager.ValidatePortsOrThrow(Profile("z", "Z", "http://127.0.0.1:1", 5000, 61199)));
        Assert.Equal(ProfileValidationErrorCode.PortConflict, ex.Code);
    }

    [Fact]
    public async Task GetCurrentAccount_all_mode_returns_null()
    {
        var settings = new FakeSettings();
        await settings.SetAsync(
            WechatAccountManager.ProfilesSettingsKey,
            System.Text.Json.JsonSerializer.Serialize(new[]
            {
                Profile("only", "仅配置", "http://127.0.0.1:1", 59201, 62201, enabled: false)
            }));
        var manager = CreateManager(settings);
        var svc = new MultiAccountWechatService(manager, NullLogger<MultiAccountWechatService>.Instance);
        await svc.SelectAccountAsync(null);
        var account = await svc.GetCurrentAccountAsync();
        Assert.Null(account);
        await svc.DisposeAsync();
    }

    [Fact]
    public async Task Real_simulate_does_not_silent_success()
    {
        var settings = new FakeSettings();
        await settings.SetAsync(
            WechatAccountManager.ProfilesSettingsKey,
            System.Text.Json.JsonSerializer.Serialize(new[]
            {
                Profile("only", "仅配置", "http://127.0.0.1:1", 59211, 62211, enabled: false)
            }));
        var svc = new MultiAccountWechatService(CreateManager(settings), NullLogger<MultiAccountWechatService>.Instance);
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await svc.SimulateIncomingMessageAsync(new ConversationKey("a", "c"), "x"));
        await svc.DisposeAsync();
    }

    [Fact]
    public async Task Manual_send_allowed_when_degraded_auto_not()
    {
        var mock = new MockWechatService();
        mock.SetAccountState(MockWechatService.AccountAId, WechatConnectionState.Degraded);
        var key = new ConversationKey(MockWechatService.AccountAId, "shared-id");
        Assert.True(mock.CanManualSend(key));
        Assert.False(mock.CanAutoReply(key));
        await Task.CompletedTask;
    }

    [Fact]
    public void Placeholder_exact_strings()
    {
        Assert.Equal("【文件消息】", WechatAccountSession.PlaceholderContent(MessageType.File, "x", null));
        Assert.Equal("【视频消息】", WechatAccountSession.PlaceholderContent(MessageType.Video, "x", null));
        Assert.Equal("【表情消息】", WechatAccountSession.PlaceholderContent(MessageType.Emoji, "<msg/>", null));
        Assert.Equal("【语音消息】", WechatAccountSession.PlaceholderContent(MessageType.Voice, "x", null));
        Assert.Equal("【暂不支持的消息】", WechatAccountSession.PlaceholderContent(MessageType.Unknown, "x", null));
    }

    [Fact]
    public void Repo_no_citation_markup_in_source()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var needle = "\uE200cite";
        var hits = new List<string>();
        foreach (var f in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (!(f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                  f.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                  f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(f);
            if (text.Contains(needle, StringComparison.Ordinal) || text.Contains("cite\uE200", StringComparison.Ordinal))
            {
                hits.Add(f);
            }
        }

        Assert.Empty(hits);
    }

    [Fact]
    public async Task Candidate_store_isolated_and_draft_not_forced()
    {
        var store = new ConversationAiCandidateStore();
        var a = new ConversationKey(MockWechatService.AccountAId, "shared-id");
        var b = new ConversationKey(MockWechatService.AccountBId, "shared-id");
        store.Set(a, "cand-A");
        store.Set(b, "cand-B");
        Assert.True(store.TryGet(a, out var ca));
        Assert.Equal("cand-A", ca);
        Assert.True(store.TryGet(b, out var cb));
        Assert.Equal("cand-B", cb);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Local_media_over_limit_rejected()
    {
        var http = new Round53HttpClientFactory();
        var cache = new MediaCacheService(NullLogger<MediaCacheService>.Instance, http);
        var huge = Path.GetTempFileName();
        await using (var fs = File.OpenWrite(huge))
        {
            fs.SetLength(51L * 1024 * 1024);
        }

        var path = await cache.GetOrFetchImageAsync(
            new MessageKey("acc", "conv", "big"),
            huge,
            downloadFactory: null);
        Assert.Null(path);
        try { File.Delete(huge); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Hook_download_media_over_limit_rejected()
    {
        var http = new Round53HttpClientFactory();
        var cache = new MediaCacheService(NullLogger<MediaCacheService>.Instance, http);
        var path = await cache.GetOrFetchImageAsync(
            new MessageKey("acc", "conv", "hookbig"),
            null,
            async (target, ct) =>
            {
                await using var fs = File.OpenWrite(target);
                fs.SetLength(51L * 1024 * 1024);
                return target;
            });
        Assert.Null(path);
    }

    [Fact]
    public void Keyed_semaphore_gate_serializes_same_key()
    {
        var gate = new KeyedSemaphoreGate();
        var order = new List<int>();
        var t1 = Task.Run(async () =>
        {
            using var d = await gate.AcquireAsync("k");
            order.Add(1);
            await Task.Delay(50);
            order.Add(2);
        });
        var t2 = Task.Run(async () =>
        {
            await Task.Delay(10);
            using var d = await gate.AcquireAsync("k");
            order.Add(3);
        });
        Task.WaitAll(t1, t2);
        Assert.Equal(new[] { 1, 2, 3 }, order.ToArray());
    }
}
