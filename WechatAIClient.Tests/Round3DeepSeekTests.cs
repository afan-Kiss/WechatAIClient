using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.DeepSeek;
using WechatAIClient.Services.Mock;

namespace WechatAIClient.Tests;

public class Round3DeepSeekTests
{
    private static List<ChatMessage> BuildRemoteThread(string contactId, int count, DateTime start)
    {
        var list = new List<ChatMessage>();
        var t = start;
        for (var i = 0; i < count; i++)
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

        return list;
    }

    private static DeepSeekAIService CreateDeepSeek(
        RecordingHandler handler,
        FakeAISettings? settings = null,
        MemorySecretStore? secrets = null)
    {
        settings ??= new FakeAISettings();
        secrets ??= new MemorySecretStore();
        secrets.SetSecretAsync(DeepSeekAIService.ApiKeySecretName, "sk-test-key").GetAwaiter().GetResult();
        return new DeepSeekAIService(
            new SimpleFactory(handler),
            secrets,
            settings,
            NullLogger<DeepSeekAIService>.Instance);
    }

    private static AIRequest MinimalRequest(string contactId = "c1") => new()
    {
        ContactId = contactId,
        GenerationId = Guid.NewGuid().ToString("N"),
        Messages =
        [
            new AIContextMessage
            {
                MessageId = "m1",
                Role = "user",
                Content = "你好",
                Source = MessageSource.RemoteUser,
                ContactId = contactId,
                Timestamp = DateTime.UtcNow
            }
        ]
    };

    private static async Task<T> CollectStreamAsync<T>(
        IAsyncEnumerable<AIStreamEvent> stream,
        Func<List<AIStreamEvent>, T> map)
    {
        var list = new List<AIStreamEvent>();
        await foreach (var evt in stream)
        {
            list.Add(evt);
        }

        return map(list);
    }

    // --- 1 + 2: temporary exclude ---

    [Fact]
    public void TemporaryExcludedMessageIds_NotInFinalPayload()
    {
        var builder = new AIContextBuilder();
        var msgs = BuildRemoteThread("c1", 6, DateTime.Today);
        var excludeId = msgs[2].Id;

        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 10,
            IncludeOwnMessages = true,
            TemporarilyExcludedMessageIds = new HashSet<string>(StringComparer.Ordinal) { excludeId }
        });

        Assert.DoesNotContain(result.Messages, m => m.MessageId == excludeId);
        Assert.True(result.FilteredOutCount >= 1);
    }

    [Fact]
    public async Task TemporaryExclude_DoesNotRemovePinFromSqlite()
    {
        var db = Path.Combine(Path.GetTempPath(), "WechatAIClientTests", $"ai-pin-exclude-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(store, new FakeSettings(), NullLogger<AISettingsService>.Instance);

        var pinned = await settings.TogglePinAsync("c1", "pin-msg-1");
        Assert.True(pinned);

        var builder = new AIContextBuilder();
        var msgs = BuildRemoteThread("c1", 5, DateTime.Today);
        msgs[0].Id = "pin-msg-1";
        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 5,
            IncludeOwnMessages = true,
            PinnedMessageIds = ["pin-msg-1"],
            TemporarilyExcludedMessageIds = new HashSet<string>(StringComparer.Ordinal) { "pin-msg-1" }
        });

        Assert.DoesNotContain(result.Messages, m => m.MessageId == "pin-msg-1");

        var pinsAfter = await settings.GetPinnedIdsAsync("c1");
        Assert.Contains("pin-msg-1", pinsAfter);
    }

    // --- 3: token trim ---

    [Fact]
    public void TokenBudget_TrimsOrdinaryOldest_BeforePinned()
    {
        var builder = new AIContextBuilder();
        var msgs = BuildRemoteThread("c1", 8, DateTime.Today);
        foreach (var m in msgs)
        {
            m.Content = new string('字', 200);
        }

        var pinId = msgs[0].Id;
        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = msgs,
            ContextCount = 8,
            IncludeOwnMessages = true,
            PinnedMessageIds = [pinId],
            TokenBudget = 500
        });

        Assert.True(result.TrimmedByBudgetCount > 0);
        Assert.Contains(result.Messages, m => m.MessageId == pinId && m.IsPinned);
        Assert.Contains("裁剪", result.SummaryText);
        Assert.Contains("📌", result.SummaryText);
    }

    // --- 4: HTTP error mapping ---

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AIErrorKind.InvalidApiKey)]
    [InlineData(HttpStatusCode.TooManyRequests, AIErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, AIErrorKind.ProviderUnavailable)]
    public async Task GenerateStream_MapsHttpErrors(HttpStatusCode status, AIErrorKind expected)
    {
        var handler = new RecordingHandler
        {
            Impl = (_, _) => Task.FromResult(FakeHttpResponses.JsonStatus(status))
        };
        var svc = CreateDeepSeek(handler);

        var ex = await Assert.ThrowsAsync<AIServiceException>(async () =>
        {
            await foreach (var _ in svc.GenerateStreamAsync(MinimalRequest()))
            {
            }
        });

        Assert.Equal(expected, ex.Kind);
        Assert.Equal(expected, DeepSeekAIService.MapHttpError(status).Kind);
    }

    // --- 5: timeout ---

    [Fact]
    public async Task Timeout_MapsToTimeout_NotUserCancelled()
    {
        var settings = new FakeAISettings();
        await settings.SaveProviderSettingsAsync(new AIProviderSettings
        {
            Provider = AIProviderKind.DeepSeek,
            RequestTimeoutSeconds = 5,
            ModelId = "deepseek-v4-flash"
        });

        var handler = new RecordingHandler
        {
            Impl = async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return FakeHttpResponses.Sse(FakeHttpResponses.ChineseSse);
            }
        };
        var svc = CreateDeepSeek(handler, settings);

        using var userCts = new CancellationTokenSource();
        var ex = await Assert.ThrowsAsync<AIServiceException>(async () =>
        {
            await foreach (var _ in svc.GenerateStreamAsync(MinimalRequest(), userCts.Token))
            {
            }
        });

        Assert.Equal(AIErrorKind.Timeout, ex.Kind);
        Assert.False(userCts.IsCancellationRequested);
    }

    // --- 6 + 10: cancel mid-stream / auto partial ---

    [Fact]
    public async Task StreamMidwayCancel_ThrowsCancelledKind()
    {
        var handler = new RecordingHandler
        {
            Impl = (_, ct) =>
            {
                var prefix =
                    "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\n";
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new PrefixThenHangStream(prefix, ct))
                };
                response.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                return Task.FromResult(response);
            }
        };
        var svc = CreateDeepSeek(handler);

        using var cts = new CancellationTokenSource();
        var gotDelta = false;
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var evt in svc.GenerateStreamAsync(MinimalRequest(), cts.Token))
            {
                if (!string.IsNullOrEmpty(evt.DeltaContent))
                {
                    gotDelta = true;
                    cts.Cancel();
                }
            }
        });

        Assert.True(gotDelta);
        if (ex is AIServiceException aiEx)
        {
            Assert.Equal(AIErrorKind.Cancelled, aiEx.Kind);
        }
        else
        {
            Assert.True(ex is OperationCanceledException);
        }
    }

    [Fact]
    public async Task Orchestrator_AutoPath_PartialCancel_StatusCancelled_NotCompleted()
    {
        var ai = new ControllableAI { Delay = TimeSpan.FromMilliseconds(800), Reply = "完整回复不应落地" };
        var orch = TestFactory.CreateOrchestrator(ai);
        var request = new AIGenerationRequest
        {
            ContactId = "f1",
            ContactName = "测试",
            ReplyMode = AIReplyMode.Auto,
            ContextSnapshot = BuildRemoteThread("f1", 3, DateTime.Today),
            ContextLength = 10
        };

        var task = orch.GenerateAsync(request);
        await Task.Delay(80);
        orch.CancelAll();
        var result = await task;

        Assert.Null(result);
        Assert.Equal(AIGenerationStatus.Cancelled, orch.Status);
        Assert.NotEqual(AIGenerationStatus.Completed, orch.Status);
    }

    // --- 7 + 8: SSE Chinese + DONE ---

    [Fact]
    public async Task GenerateStream_ReassemblesChineseChunks_AndDone()
    {
        var handler = new RecordingHandler
        {
            Impl = (_, _) => Task.FromResult(FakeHttpResponses.Sse(FakeHttpResponses.ChineseSse))
        };
        var svc = CreateDeepSeek(handler);

        var events = await CollectStreamAsync(
            svc.GenerateStreamAsync(MinimalRequest()),
            list => list);

        var text = string.Concat(events.Where(e => e.DeltaContent is not null).Select(e => e.DeltaContent));
        Assert.Equal("你好", text);
        Assert.Contains(events, e => e.IsDone);
        Assert.Equal("你好", (await svc.GenerateAsync(MinimalRequest())).Content);
    }

    [Fact]
    public void SseParser_DoneCompletes()
    {
        var parser = new DeepSeekSseParser();
        var events = parser.Feed(FakeHttpResponses.ChineseSse).ToList();
        Assert.Equal("你", events[0].DeltaContent);
        Assert.Equal("好", events[1].DeltaContent);
        Assert.True(events[^1].IsDone);
        Assert.True(parser.IsDone);
    }

    // --- 9: draft revision ---

    [Fact]
    public async Task TryApplyAiDraft_ReturnsFalse_WhenRevisionChanged()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contact = (await wechat.GetRecentChatsAsync()).First();
        await chat.LoadContactAsync(contact);
        chat.SetDraftFromAi("AI候选");
        var rev = chat.DraftRevision;
        chat.DraftText = "用户改了";
        Assert.False(chat.TryApplyAiDraft("晚到的AI", rev));
        Assert.Equal("用户改了", chat.DraftText);
    }

    // --- 11: same contact double GenerateAsync ---

    [Fact]
    public async Task SameContact_SecondGenerate_CancelsPrevious()
    {
        var ai = new ControllableAI { Delay = TimeSpan.FromMilliseconds(600), Reply = "第二轮" };
        var orch = TestFactory.CreateOrchestrator(ai);
        var snapshot = BuildRemoteThread("f1", 2, DateTime.Today);

        var first = orch.GenerateAsync(new AIGenerationRequest
        {
            ContactId = "f1",
            ContactName = "A",
            ContextSnapshot = snapshot,
            ContextLength = 10,
            GenerationId = "g1"
        });
        await Task.Delay(60);
        var second = orch.GenerateAsync(new AIGenerationRequest
        {
            ContactId = "f1",
            ContactName = "A",
            ContextSnapshot = snapshot,
            ContextLength = 10,
            GenerationId = "g2"
        });

        var r1 = await first;
        var r2 = await second;

        Assert.Null(r1);
        Assert.NotNull(r2);
        Assert.Equal("第二轮", r2!.Content);
        Assert.True(ai.CallCount >= 2);
        Assert.True(ai.CancelledCount >= 1);
        Assert.Equal(AIGenerationStatus.Completed, orch.Status);
    }

    // --- 12: global concurrency ---

    [Fact]
    public async Task GlobalConcurrency_MaxConcurrentIsThree()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ai = new ControllableAI { HoldGate = hold, Reply = "ok" };
        var orch = TestFactory.CreateOrchestrator(ai);

        async Task<AIGenerationResult?> Start(string contactId) =>
            await orch.GenerateAsync(new AIGenerationRequest
            {
                ContactId = contactId,
                ContactName = contactId,
                ContextSnapshot = BuildRemoteThread(contactId, 1, DateTime.Today),
                ContextLength = 5
            });

        var tasks = new[]
        {
            Start("c1"), Start("c2"), Start("c3"), Start("c4")
        };

        await Task.Delay(150);
        Assert.True(ai.CallCount <= 3 || ai.MaxObservedActive <= 3);
        Assert.True(ai.MaxObservedActive <= 3);

        hold.TrySetResult();
        await Task.WhenAll(tasks);
        Assert.Equal(4, ai.CallCount);
        Assert.True(ai.MaxObservedActive <= 3);
    }

    // --- 13: API key not in SQLite ---

    [Fact]
    public async Task ApiKey_NotPersistedInSqliteFile()
    {
        var db = Path.Combine(Path.GetTempPath(), "WechatAIClientTests", $"ai-secret-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(store, new FakeSettings(), NullLogger<AISettingsService>.Instance);
        var secrets = new MemorySecretStore();
        const string secretKey = "sk-must-not-appear-in-sqlite-file-XYZ99";

        await settings.SaveProviderSettingsAsync(new AIProviderSettings
        {
            Provider = AIProviderKind.DeepSeek,
            ModelId = "deepseek-v4-flash",
            BaseUrl = "https://api.deepseek.com"
        });
        await secrets.SetSecretAsync(DeepSeekAIService.ApiKeySecretName, secretKey);

        Assert.Equal(secretKey, await secrets.GetSecretAsync(DeepSeekAIService.ApiKeySecretName));

        // Ensure provider row flushed
        await settings.GetProviderSettingsAsync();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await using (var fs = new FileStream(db, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var bytes = new byte[fs.Length];
            var read = await fs.ReadAsync(bytes);
            Assert.Equal(bytes.Length, read);
            var asText = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(secretKey, asText);
            Assert.DoesNotContain(secretKey, Encoding.Unicode.GetString(bytes));
        }
    }

    // --- 14: TestConnection does not touch draft ---

    [Fact]
    public async Task TestConnection_DoesNotMutateChatDraft()
    {
        var wechat = new MockWechatService();
        var chat = TestFactory.CreateChat(wechat);
        var contact = (await wechat.GetRecentChatsAsync()).First();
        await chat.LoadContactAsync(contact);
        chat.DraftText = "用户草稿应保持";
        var rev = chat.DraftRevision;

        var handler = new RecordingHandler
        {
            Impl = (_, _) => Task.FromResult(FakeHttpResponses.JsonStatus(HttpStatusCode.OK, """{"id":"x","choices":[{"message":{"content":"pong"}}]}"""))
        };
        var svc = CreateDeepSeek(handler);
        var result = await svc.TestConnectionAsync();

        Assert.True(result.Success);
        Assert.Equal("用户草稿应保持", chat.DraftText);
        Assert.Equal(rev, chat.DraftRevision);
    }

    // --- 15: pin limit 20 ---

    [Fact]
    public async Task PinLimit_21stFailsReturnsFalse()
    {
        var db = Path.Combine(Path.GetTempPath(), "WechatAIClientTests", $"ai-pins-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(store, new FakeSettings(), NullLogger<AISettingsService>.Instance);

        for (var i = 0; i < 20; i++)
        {
            Assert.True(await settings.TogglePinAsync("c1", $"m{i}"));
        }

        var pins = await settings.GetPinnedIdsAsync("c1");
        Assert.Equal(20, pins.Count);

        Assert.False(await settings.TogglePinAsync("c1", "m20"));
        pins = await settings.GetPinnedIdsAsync("c1");
        Assert.Equal(20, pins.Count);
        Assert.DoesNotContain("m20", pins);
        Assert.Contains("m0", pins);
    }

    // --- 16: preview / request same source ---

    [Fact]
    public async Task Orchestrator_LastBuildResult_Messages_Equals_LastAiRequest()
    {
        var ai = new ControllableAI { Delay = TimeSpan.FromMilliseconds(10), Reply = "ok" };
        var orch = TestFactory.CreateOrchestrator(ai);
        await orch.GenerateAsync(new AIGenerationRequest
        {
            ContactId = "c1",
            ContactName = "C",
            ContextSnapshot = BuildRemoteThread("c1", 5, DateTime.Today),
            ContextLength = 4,
            IncludeOwnMessages = true,
            TemporaryInstruction = "短一点"
        });

        Assert.NotNull(orch.LastBuildResult);
        Assert.NotNull(orch.LastAiRequest);
        Assert.Equal(
            orch.LastBuildResult!.Messages.Select(m => (m.MessageId, m.Role, m.Content)),
            orch.LastAiRequest!.Messages.Select(m => (m.MessageId, m.Role, m.Content)));
        Assert.Same(orch.LastBuildResult.Messages, orch.LastAiRequest.Messages);
    }

    // --- 17: IncludeOwnMessages=false ---

    [Fact]
    public void IncludeOwnFalse_ReturnsTenRemoteOnly()
    {
        var builder = new AIContextBuilder();
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

        var result = builder.Build(new AIContextBuildInput
        {
            ContactId = "c1",
            Messages = list,
            ContextCount = 10,
            IncludeOwnMessages = false
        });

        var history = result.Messages.Where(m => m.Role is "user" or "assistant").ToList();
        Assert.Equal(10, history.Count);
        Assert.Equal(0, result.OwnCount);
        Assert.All(history, m => Assert.Equal(MessageSource.RemoteUser, m.Source));
    }
}
