using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Mock;
using WechatAIClient.Services.Wechat;

namespace WechatAIClient.Tests;

public class Round52MultiAccountTests
{
    [Fact]
    public void ConversationKey_and_MessageKey_stable()
    {
        var c = new ConversationKey("a1", "filehelper");
        Assert.Equal("a1::filehelper", c.StableKey);
        Assert.Equal(c, ConversationKey.Parse(c.StableKey));

        var m = new MessageKey("a1", "filehelper", "mid");
        Assert.Equal("a1::filehelper::mid", m.StableKey);
        Assert.Equal(c, m.Conversation);
    }

    [Fact]
    public async Task Mock_dual_accounts_same_conversation_id_isolated()
    {
        var mock = new MockWechatService();
        await mock.SelectAccountAsync(MockWechatService.AccountAId);
        await mock.SendTextMessageAsync(
            new ConversationKey(MockWechatService.AccountAId, "shared-id"),
            "from-A");
        await mock.SelectAccountAsync(MockWechatService.AccountBId);
        await mock.SendTextMessageAsync(
            new ConversationKey(MockWechatService.AccountBId, "shared-id"),
            "from-B");

        var aMsgs = await mock.GetMessagesAsync(new ConversationKey(MockWechatService.AccountAId, "shared-id"));
        var bMsgs = await mock.GetMessagesAsync(new ConversationKey(MockWechatService.AccountBId, "shared-id"));
        Assert.Contains(aMsgs, m => m.Content == "from-A");
        Assert.DoesNotContain(aMsgs, m => m.Content == "from-B");
        Assert.Contains(bMsgs, m => m.Content == "from-B");
        Assert.DoesNotContain(bMsgs, m => m.Content == "from-A");
    }

    [Fact]
    public async Task Mock_accounts_filter_contacts()
    {
        var mock = new MockWechatService();
        await mock.SelectAccountAsync(MockWechatService.AccountAId);
        var a = await mock.GetContactsAsync();
        Assert.All(a, c => Assert.Equal(MockWechatService.AccountAId, c.AccountId));

        await mock.SelectAccountAsync(MockWechatService.AccountBId);
        var b = await mock.GetContactsAsync();
        Assert.All(b, c => Assert.Equal(MockWechatService.AccountBId, c.AccountId));

        await mock.SelectAccountAsync(null);
        var all = await mock.GetContactsAsync();
        Assert.Contains(all, c => c.AccountId == MockWechatService.AccountAId);
        Assert.Contains(all, c => c.AccountId == MockWechatService.AccountBId);
    }

    [Fact]
    public async Task Mock_same_message_id_across_accounts_not_shared()
    {
        var mock = new MockWechatService();
        await mock.SimulateIncomingMessageAsync(
            new ConversationKey(MockWechatService.AccountAId, "filehelper"),
            "A-msg");
        await mock.SimulateIncomingMessageAsync(
            new ConversationKey(MockWechatService.AccountBId, "filehelper"),
            "B-msg");

        var a = await mock.GetMessagesAsync(new ConversationKey(MockWechatService.AccountAId, "filehelper"));
        var b = await mock.GetMessagesAsync(new ConversationKey(MockWechatService.AccountBId, "filehelper"));
        Assert.Contains(a, m => m.Content.Contains("A-msg"));
        Assert.DoesNotContain(a, m => m.Content.Contains("B-msg"));
        Assert.Contains(b, m => m.Content.Contains("B-msg"));
    }

    [Fact]
    public async Task Draft_store_isolated_by_conversation_key()
    {
        var drafts = new ConversationDraftStore();
        var a = new ConversationKey("acc_a", "c1");
        var b = new ConversationKey("acc_b", "c1");
        drafts.SetDraft(a, "draft-a");
        drafts.SetDraft(b, "draft-b");
        Assert.Equal("draft-a", drafts.GetDraft(a));
        Assert.Equal("draft-b", drafts.GetDraft(b));
        drafts.Clear(a);
        Assert.Null(drafts.GetDraft(a));
        Assert.Equal("draft-b", drafts.GetDraft(b));
        await Task.CompletedTask;
    }

    [Fact]
    public void Deduper_message_key_isolates_accounts()
    {
        var d = new MessageDeduplicator();
        Assert.True(d.TryAdd("acc_a::friend", "mid1"));
        Assert.True(d.TryAdd("acc_b::friend", "mid1"));
        Assert.False(d.TryAdd("acc_a::friend", "mid1"));
    }

    [Fact]
    public async Task Auto_send_uses_trigger_account_not_selected()
    {
        var mock = new MockWechatService();
        await mock.SelectAccountAsync(MockWechatService.AccountBId);
        var key = new ConversationKey(MockWechatService.AccountAId, "filehelper");
        var result = await mock.SendTextMessageAsync(key, "fixed-A", isFromAi: true);
        Assert.True(result.Success);
        var msgs = await mock.GetMessagesAsync(key);
        Assert.Contains(msgs, m => m.Content == "fixed-A" && m.AccountId == MockWechatService.AccountAId);
    }

    [Fact]
    public void Pending_tracker_clear_empties()
    {
        var t = new PendingOutgoingTracker();
        t.Register("c1", "conv", "hi", false);
        Assert.True(t.Count > 0);
        t.Clear();
        Assert.Equal(0, t.Count);
    }

    [Fact]
    public void Media_placeholder_content_helpers()
    {
        Assert.Equal(MessageType.Video, Enum.Parse<MessageType>("Video"));
        Assert.Equal(MessageType.Unknown, Enum.Parse<MessageType>("Unknown"));
        var msg = new ChatMessage { Type = MessageType.File, Content = "【文件消息】", MediaLoadState = MediaLoadState.None };
        Assert.True(msg.IsMediaPlaceholder);
        msg.MediaLoadState = MediaLoadState.Loaded;
        Assert.True(msg.IsMediaLoaded);
    }

    [Fact]
    public async Task Search_all_accounts_keeps_distinct_same_name()
    {
        var mock = new MockWechatService();
        await mock.SelectAccountAsync(null);
        var hits = await mock.SearchAsync("文件传输助手", null);
        var accounts = hits.Select(h => h.Contact.AccountId).Distinct().ToList();
        Assert.True(accounts.Count >= 2);
    }
}
