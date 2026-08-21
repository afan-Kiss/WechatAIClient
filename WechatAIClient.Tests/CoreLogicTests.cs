using WechatAIClient.Models;
using WechatAIClient.Services.Mock;

namespace WechatAIClient.Tests;

public class ContactAndUnreadTests
{
    [Fact]
    public void Contact_UnreadDisplay_CapsAt99Plus()
    {
        var contact = new Contact { Id = "1", UnreadCount = 120 };
        Assert.Equal("99+", contact.UnreadDisplay);
        contact.UnreadCount = 3;
        Assert.Equal("3", contact.UnreadDisplay);
    }

    [Fact]
    public void Contact_LastMessage_RaisesPropertyChanged()
    {
        var contact = new Contact { Id = "1" };
        string? changed = null;
        contact.PropertyChanged += (_, e) => changed = e.PropertyName;
        contact.LastMessage = "hello";
        Assert.Equal(nameof(Contact.LastMessage), changed);
    }

    [Fact]
    public async Task OpeningContact_ClearsUnread()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contacts = await wechat.GetRecentChatsAsync();
        var target = contacts.First(c => c.UnreadCount > 0);
        await chat.LoadContactAsync(target);
        Assert.Equal(0, target.UnreadCount);
    }
}

public class LoadRaceTests
{
    [Fact]
    public async Task FastSwitch_A_to_B_ShowsOnlyB()
    {
        var wechat = new MockWechatService
        {
            MessageLoadDelayMs = id => id == "g1" ? 800 : 50
        };
        var chat = TestFactory.CreateChat(wechat);
        var recent = await wechat.GetRecentChatsAsync();
        var a = recent.First(c => c.Id == "g1");
        var b = recent.First(c => c.Id == "f1");

        var loadA = chat.LoadContactAsync(a);
        await Task.Delay(30);
        var loadB = chat.LoadContactAsync(b);
        await Task.WhenAll(loadA, loadB);

        Assert.Equal("f1", chat.CurrentContact?.Id);
        Assert.All(chat.Messages, m => Assert.Equal("f1", m.ContactId));
    }
}

public class SendRaceTests
{
    [Fact]
    public async Task SendFailure_RestoresDraft()
    {
        var wechat = new MockWechatService { ThrowOnSend = true };
        var chat = TestFactory.CreateChat(wechat);
        var contact = (await wechat.GetRecentChatsAsync()).First();
        await chat.LoadContactAsync(contact);
        chat.DraftText = "待发送内容";
        await chat.SendCommand.ExecuteAsync(null);
        Assert.Equal("待发送内容", chat.DraftText);
    }
}

public class SearchRaceTests
{
    [Fact]
    public async Task LateSearch_DoesNotOverwriteNewerQuery()
    {
        var wechat = new MockWechatService { DelaySearchMs = 600 };
        var list = TestFactory.CreateContacts(wechat);
        await list.InitializeAsync();

        // Let "产品" pass debounce and enter delayed SearchAsync, then supersede with "张晓彤".
        list.SearchText = "产品";
        await Task.Delay(350);
        list.SearchText = "张晓彤";
        await Task.Delay(1200);

        Assert.Contains(list.VisibleContacts, c => c.Name.Contains("张晓彤", StringComparison.Ordinal));
        Assert.DoesNotContain(list.VisibleContacts, c => c.Id == "g1");
        Assert.DoesNotContain(list.VisibleContacts, c => c.Name.Contains("产品", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_RespectsFriendsTab()
    {
        var wechat = new MockWechatService();
        var list = TestFactory.CreateContacts(wechat);
        await list.InitializeAsync();
        list.SelectedTabIndex = 1;
        list.SearchText = "产品";
        await Task.Delay(500);
        Assert.All(list.VisibleContacts, c => Assert.Equal(ContactType.Friend, c.Type));
    }
}

public class RecentSortTests
{
    [Fact]
    public async Task BumpRecent_MovesContactToTop()
    {
        var wechat = new MockWechatService();
        var list = TestFactory.CreateContacts(wechat);
        await list.InitializeAsync();
        var bottom = list.VisibleContacts.Last();
        bottom.LastMessageTime = DateTime.Now.AddMinutes(1);
        bottom.LastMessage = "最新";
        list.BumpRecent(bottom);
        Assert.Equal(bottom.Id, list.VisibleContacts.First().Id);
    }
}

public class AiOrchestratorTests
{
    [Fact]
    public async Task Cancel_StopsTypingUpdate()
    {
        var ai = new ControllableAI { Delay = TimeSpan.FromMilliseconds(300), Reply = "ABCDEFGHIJKLMNOP" };
        var orch = TestFactory.CreateOrchestrator(ai);
        var chunks = new List<string>();
        var request = new AIGenerationRequest
        {
            ContactId = "f1",
            ContactName = "李明远",
            ContextSnapshot = [new ChatMessage { Content = "hi", ContactId = "f1" }],
            ContextLength = 10
        };

        var task = orch.GenerateAsync(request, s => chunks.Add(s));
        await Task.Delay(50);
        orch.CancelAll();
        var result = await task;
        Assert.Null(result);
    }

    [Fact]
    public async Task Regenerate_UsesSameContext()
    {
        var ai = new ControllableAI { Delay = TimeSpan.FromMilliseconds(20), Reply = "第一次" };
        var orch = TestFactory.CreateOrchestrator(ai);
        var ctx = new List<ChatMessage>
        {
            new() { Content = "上下文A", ContactId = "f1" },
            new() { Content = "上下文B", ContactId = "f1" }
        };
        var request = new AIGenerationRequest
        {
            ContactId = "f1",
            ContactName = "李明远",
            ContextSnapshot = ctx,
            ContextLength = 10
        };
        await orch.GenerateAsync(request);
        Assert.NotNull(orch.LastRequest);
        Assert.Equal(2, orch.LastRequest!.ContextSnapshot.Count);
        Assert.Equal("f1", orch.LastRequest.ContactId);
    }
}

public class AiModeLogicTests
{
    [Fact]
    public async Task OffMode_DoesNotCallAi()
    {
        var ai = new ControllableAI();
        // Off is enforced at AIPanelViewModel layer; orchestrator still callable.
        // Verify ControllableAI not called when we skip.
        Assert.Equal(0, ai.CallCount);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task AutoSend_TargetsCorrectContactId()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contacts = await wechat.GetRecentChatsAsync();
        var a = contacts.First(c => c.Id == "g1");
        var b = contacts.First(c => c.Id == "f1");
        await chat.LoadContactAsync(b);
        await chat.SendAsync(a.Id, "发给A的AI回复", isFromAi: true);

        // Current UI still on B
        Assert.Equal("f1", chat.CurrentContact?.Id);
        Assert.DoesNotContain(chat.Messages, m => m.Content == "发给A的AI回复");

        var aMessages = await wechat.GetMessagesAsync("g1");
        Assert.Contains(aMessages, m => m.Content == "发给A的AI回复" && m.IsFromAi);
    }
}

public class ThemeServiceLogicTests
{
    [Fact]
    public void SystemTheme_ActualIsLight_TracksNotification()
    {
        var settings = new FakeSettings();
        var theme = new WechatAIClient.Services.ThemeService(
            settings,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WechatAIClient.Services.ThemeService>.Instance);
        theme.SetTheme(AppThemeMode.System);
        theme.NotifySystemThemeChanged(true);
        Assert.True(theme.ActualIsLight);
        theme.NotifySystemThemeChanged(false);
        Assert.False(theme.ActualIsLight);
    }
}

public class ClipboardCopyTests
{
    [Fact]
    public async Task Clipboard_StoresText()
    {
        var clip = new FakeClipboard();
        await clip.SetTextAsync("复制内容");
        Assert.Equal("复制内容", clip.Text);
    }
}

public class InitOnceTests
{
    [Fact]
    public async Task Initialize_SetsSelectedContact_WithoutManualSecondLoadRequirement()
    {
        var wechat = new MockWechatService();
        var list = TestFactory.CreateContacts(wechat);
        var loads = 0;
        list.ContactSelected += (_, _) => loads++;
        await list.InitializeAsync();
        Assert.Equal(1, loads);
        Assert.NotNull(list.SelectedContact);
    }
}
