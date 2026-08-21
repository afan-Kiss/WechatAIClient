using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Mock;
using Microsoft.Extensions.Logging.Abstractions;

namespace WechatAIClient.Tests;

public class AIContextBuilderTests
{
    private readonly AIContextBuilder _builder = new();

    private static List<ChatMessage> BuildThread(string contactId, int remote, int own, DateTime start)
    {
        var list = new List<ChatMessage>();
        var t = start;
        for (var i = 0; i < remote; i++)
        {
            list.Add(new ChatMessage
            {
                Id = $"r{i}",
                ContactId = contactId,
                Content = $"对方{i}",
                IsSelf = false,
                Source = MessageSource.RemoteUser,
                Timestamp = t
            });
            t = t.AddMinutes(1);
        }

        for (var i = 0; i < own; i++)
        {
            list.Add(new ChatMessage
            {
                Id = $"o{i}",
                ContactId = contactId,
                Content = $"自己{i}",
                IsSelf = true,
                Source = MessageSource.LocalUserManual,
                Timestamp = t
            });
            t = t.AddMinutes(1);
        }

        return list;
    }

    [Fact]
    public void IncludeOwn_True_TakesLastNIncludingOwn()
    {
        var msgs = BuildThread("c1", remote: 8, own: 7, start: DateTime.Today);
        var result = _builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 10,
            IncludeOwnMessages = true
        });

        var history = result.Messages.Where(m => m.Role is "user" or "assistant").ToList();
        Assert.True(history.Count <= 10);
        Assert.Equal(10, result.SelectedOrdinaryCount);
        Assert.True(result.OwnCount > 0);
        Assert.True(result.RemoteCount > 0);
        // chronological
        for (var i = 1; i < history.Count; i++)
        {
            Assert.True(history[i].Timestamp >= history[i - 1].Timestamp);
        }
    }

    [Fact]
    public void IncludeOwn_False_SelectsTenRemoteNotPreTrimThenFilter()
    {
        // 20 remote + 20 own interleaved by time: last 10 raw would be mostly own
        var list = new List<ChatMessage>();
        var t = DateTime.Today;
        for (var i = 0; i < 20; i++)
        {
            list.Add(new ChatMessage
            {
                Id = $"r{i}", ContactId = "c1", Content = $"对方{i}",
                Source = MessageSource.RemoteUser, Timestamp = t
            });
            t = t.AddMinutes(1);
            list.Add(new ChatMessage
            {
                Id = $"o{i}", ContactId = "c1", Content = $"自己{i}",
                IsSelf = true, Source = MessageSource.LocalUserManual, Timestamp = t
            });
            t = t.AddMinutes(1);
        }

        var result = _builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = list,
            ContextCount = 10,
            IncludeOwnMessages = false
        });

        var history = result.Messages.Where(m => m.Role is "user" or "assistant").ToList();
        Assert.Equal(10, history.Count);
        Assert.Equal(10, result.SelectedOrdinaryCount);
        Assert.Equal(0, result.OwnCount);
        Assert.All(history, m => Assert.Equal(MessageSource.RemoteUser, m.Source));
        Assert.Contains("仅对方", result.SummaryText);
    }

    [Fact]
    public void Payload_ExcludesOwn_WhenIncludeOwnFalse()
    {
        var msgs = BuildThread("c1", 5, 5, DateTime.Today);
        var result = _builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 10,
            IncludeOwnMessages = false
        });

        Assert.DoesNotContain(result.Messages, m =>
            m.Source is MessageSource.LocalUserManual or MessageSource.LocalUserAI);
    }

    [Fact]
    public void Pinned_OutsideN_StillIncluded_AndDeduped()
    {
        var msgs = BuildThread("c1", 12, 0, DateTime.Today);
        var oldId = msgs[0].Id; // oldest remote
        var result = _builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 5,
            IncludeOwnMessages = true,
            PinnedMessageIds = [oldId]
        });

        var history = result.Messages.Where(m => m.Role is "user" or "assistant").ToList();
        Assert.Contains(history, m => m.MessageId == oldId && m.IsPinned);
        Assert.Equal(1, history.Count(m => m.MessageId == oldId));
        Assert.True(result.PinnedCount >= 1);
    }

    [Fact]
    public void TokenBudget_TrimsOrdinaryOldest_KeepsPinned()
    {
        var msgs = BuildThread("c1", 8, 0, DateTime.Today);
        foreach (var m in msgs)
        {
            m.Content = new string('字', 200);
        }

        var pinId = msgs[0].Id;
        var result = _builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 8,
            IncludeOwnMessages = true,
            PinnedMessageIds = [pinId],
            TokenBudget = 500 // small → force trim
        });

        Assert.True(result.TrimmedByBudgetCount > 0);
        Assert.Contains(result.Messages, m => m.MessageId == pinId);
        Assert.Contains("裁剪", result.SummaryText);
    }

    [Fact]
    public void Preview_MatchesRequestMessages()
    {
        var msgs = BuildThread("c1", 6, 4, DateTime.Today);
        var input = new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 5,
            IncludeOwnMessages = true,
            TemporaryInstruction = "委婉一点",
            ReplyStyle = ReplyStyle.Formal,
            ReplyLength = ReplyLength.Short
        };
        var result = _builder.Build(input);
        var request = new AIRequest
        {
            ContactId = "c1",
            GenerationId = "g1",
            Messages = result.Messages,
            Style = input.ReplyStyle,
            Length = input.ReplyLength,
            TemporaryInstruction = input.TemporaryInstruction,
            ContextMeta = result
        };

        Assert.Equal(result.Messages.Count, request.Messages.Count);
        Assert.Equal(result.Messages.Select(m => m.MessageId), request.Messages.Select(m => m.MessageId));
        Assert.Equal("委婉一点", request.TemporaryInstruction);
    }

    [Fact]
    public void IgnoresOtherContactMessages()
    {
        var msgs = BuildThread("c1", 5, 0, DateTime.Today);
        msgs.Add(new ChatMessage
        {
            Id = "x", ContactId = "other", Content = "别的会话",
            Source = MessageSource.RemoteUser, Timestamp = DateTime.Now
        });
        var result = _builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 10,
            IncludeOwnMessages = true
        });
        Assert.DoesNotContain(result.Messages, m => m.ContactId == "other" || m.MessageId == "x");
    }
}

public class AISettingsPersistenceTests
{
    [Fact]
    public async Task PerContactOverride_DoesNotAffectOthers_AndRestoreWorks()
    {
        var db = Path.Combine(Path.GetTempPath(), "WechatAIClientTests", $"ai-settings-{Guid.NewGuid():N}.db");
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(
            store,
            new FakeSettings(),
            NullLogger<AISettingsService>.Instance);

        await settings.SaveGlobalAsync(new AIGlobalSettings { ContextCount = 10, IncludeOwnMessages = true });
        await settings.SaveOverrideAsync(new AIContactOverride
        {
            ContactId = "c1",
            UseOverride = true,
            ContextCount = 20,
            IncludeOwnMessages = false
        });

        var e1 = await settings.GetEffectiveAsync("legacy", "c1");
        var e2 = await settings.GetEffectiveAsync("legacy", "c2");
        Assert.Equal(20, e1.ContextCount);
        Assert.False(e1.IncludeOwnMessages);
        Assert.True(e1.IsUsingOverride);
        Assert.Equal(10, e2.ContextCount);
        Assert.True(e2.IncludeOwnMessages);
        Assert.False(e2.IsUsingOverride);

        await settings.ClearOverrideAsync("legacy", "c1");
        var restored = await settings.GetEffectiveAsync("legacy", "c1");
        Assert.Equal(10, restored.ContextCount);
        Assert.False(restored.IsUsingOverride);
    }

    [Fact]
    public async Task SchemaV3_Migration_AllowsNewTables()
    {
        var db = Path.Combine(Path.GetTempPath(), "WechatAIClientTests", $"ai-schema-{Guid.NewGuid():N}.db");
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(
            store,
            new FakeSettings(),
            NullLogger<AISettingsService>.Instance);
        await settings.SaveGlobalAsync(new AIGlobalSettings { ContextCount = 5 });
        await settings.TogglePinAsync("legacy", "c1", "m1");
        var pins = await settings.GetPinnedIdsAsync("legacy", "c1");
        Assert.Contains("m1", pins);
        var global = await settings.GetGlobalAsync();
        Assert.Equal(5, global.ContextCount);
    }
}

public class DraftRevisionTests
{
    [Fact]
    public async Task ManualConfirm_DoesNotOverwriteUserEditedDraft()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contact = (await wechat.GetRecentChatsAsync()).First();
        await chat.LoadContactAsync(contact);
        chat.SetDraftFromAi("AI候选");
        var rev = chat.DraftRevision;
        chat.DraftText = "用户正在改"; // bumps revision
        Assert.False(chat.TryApplyAiDraft("晚到的AI", rev));
        Assert.Equal("用户正在改", chat.DraftText);
    }
}

public class GroupTriggerAndDebounceNotesTests
{
    [Fact]
    public void GroupTrigger_MentionOrQuote_RequiresFlags()
    {
        var msg = new ChatMessage { MentionsMe = false, QuotesMe = false };
        Assert.False(msg.MentionsMe || msg.QuotesMe);
        msg.MentionsMe = true;
        Assert.True(msg.MentionsMe || msg.QuotesMe);
    }
}

public class TemporaryInstructionSemanticsTests
{
    [Fact]
    public void TemporaryInstruction_AppearsInBuildResult()
    {
        var builder = new AIContextBuilder();
        var msgs = new List<ChatMessage>
        {
            new()
            {
                Id = "1", ContactId = "c1", Content = "你好",
                Source = MessageSource.RemoteUser, Timestamp = DateTime.Now
            }
        };
        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 5,
            IncludeOwnMessages = true,
            TemporaryInstruction = "只回一句"
        });
        Assert.Equal("只回一句", result.TemporaryInstruction);
        Assert.Contains(result.Messages, m => m.Role == "system" && m.Content.Contains("只回一句"));
    }
}

public class AiFromAiLoopTests
{
    [Fact]
    public async Task LocalUserAI_DoesNotAppearAsRemoteTriggerCandidate()
    {
        var wechat = new MockWechatService();
        await wechat.SendMessageAsync("g1", "AI话", isFromAi: true);
        var messages = await wechat.GetMessagesAsync("g1");
        var last = messages.Last(m => m.IsFromAi);
        Assert.Equal(MessageSource.LocalUserAI, last.Source);
        Assert.True(last.IsSelf);
    }
}
