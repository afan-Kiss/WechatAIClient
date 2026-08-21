using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Mock;
using WechatAIClient.ViewModels;

namespace WechatAIClient.Tests;

internal static class TestFactory
{
    public static MockWechatService CreateWechat() => new();

    public static ChatViewModel CreateChat(MockWechatService? wechat = null)
    {
        wechat ??= CreateWechat();
        return new ChatViewModel(
            wechat,
            new FakeFilePicker(),
            new FakeToast(),
            new FakeAISettings(),
            NullLogger<ChatViewModel>.Instance);
    }

    public static ContactListViewModel CreateContacts(MockWechatService? wechat = null)
    {
        wechat ??= CreateWechat();
        return new ContactListViewModel(wechat, NullLogger<ContactListViewModel>.Instance);
    }

    public static AIOrchestrator CreateOrchestrator(IAIService? ai = null)
        => new(
            ai ?? new MockAIService(NullLogger<MockAIService>.Instance),
            new AIContextBuilder(),
            NullLogger<AIOrchestrator>.Instance);
}

internal sealed class FakeToast : IToastService
{
    public string Message { get; private set; } = string.Empty;
    public bool IsVisible { get; private set; }
    public event EventHandler? Changed;
    public List<string> Messages { get; } = [];

    public Task ShowAsync(string message, int durationMs = 2200)
    {
        Message = message;
        IsVisible = true;
        Messages.Add(message);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

internal sealed class FakeClipboard : IClipboardService
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Text);
}

internal sealed class FakeFilePicker : IFilePickerService
{
    public string? NextImage { get; set; }
    public string? NextFile { get; set; }

    public Task<string?> PickImageAsync(CancellationToken cancellationToken = default) => Task.FromResult(NextImage);
    public Task<string?> PickFileAsync(CancellationToken cancellationToken = default) => Task.FromResult(NextFile);
}

internal sealed class FakeSettings : ISettingsStore
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_map.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _map[key] = value;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAISettings : IAISettingsService
{
    private AIGlobalSettings _global = new();
    private AIProviderSettings _provider = new();
    private readonly Dictionary<string, AIContactOverride> _overrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _pins = new(StringComparer.Ordinal);
    private DateTime? _pausedUntil;

    public Task<AIGlobalSettings> GetGlobalAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_global);

    public Task SaveGlobalAsync(AIGlobalSettings settings, CancellationToken cancellationToken = default)
    {
        _global = settings;
        return Task.CompletedTask;
    }

    public Task<AIContactOverride?> GetOverrideAsync(string contactId, CancellationToken cancellationToken = default)
        => Task.FromResult(_overrides.TryGetValue(contactId, out var o) ? o : null);

    public Task SaveOverrideAsync(AIContactOverride overrideSettings, CancellationToken cancellationToken = default)
    {
        _overrides[overrideSettings.ContactId] = overrideSettings;
        return Task.CompletedTask;
    }

    public Task ClearOverrideAsync(string contactId, CancellationToken cancellationToken = default)
    {
        _overrides.Remove(contactId);
        return Task.CompletedTask;
    }

    public async Task<EffectiveAISettings> GetEffectiveAsync(string contactId, CancellationToken cancellationToken = default)
    {
        var g = await GetGlobalAsync(cancellationToken);
        var ov = await GetOverrideAsync(contactId, cancellationToken);
        var use = ov is { UseOverride: true };
        return new EffectiveAISettings
        {
            ContactId = contactId,
            IsUsingOverride = use,
            ReplyMode = use && ov!.ReplyMode is { } rm ? rm : g.ReplyMode,
            ContextCount = use && ov!.ContextCount is { } cc ? cc : g.ContextCount,
            IncludeOwnMessages = use && ov!.IncludeOwnMessages is { } iom ? iom : g.IncludeOwnMessages,
            ReplyStyle = use && ov!.ReplyStyle is { } rs ? rs : g.ReplyStyle,
            ReplyLength = use && ov!.ReplyLength is { } rl ? rl : g.ReplyLength,
            AutoGenerateOnReceive = use && ov!.AutoGenerateOnReceive is { } ag ? ag : g.AutoGenerateOnReceive,
            GroupTriggerMode = use && ov!.GroupTriggerMode is { } gtm ? gtm : g.GroupTriggerMode
        };
    }

    public Task<IReadOnlyList<string>> GetPinnedIdsAsync(string contactId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(
            _pins.TryGetValue(contactId, out var list) ? list.ToList() : Array.Empty<string>());

    public Task SetPinnedIdsAsync(string contactId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
    {
        _pins[contactId] = messageIds.Take(8).ToList();
        return Task.CompletedTask;
    }

    public async Task<bool> TogglePinAsync(string contactId, string messageId, CancellationToken cancellationToken = default)
    {
        var pins = (await GetPinnedIdsAsync(contactId, cancellationToken)).ToList();
        if (pins.Remove(messageId))
        {
            await SetPinnedIdsAsync(contactId, pins, cancellationToken);
            return false;
        }

        pins.Add(messageId);
        await SetPinnedIdsAsync(contactId, pins, cancellationToken);
        return true;
    }

    public Task<DateTime?> GetAutoPausedUntilAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_pausedUntil);

    public Task SetAutoPausedUntilAsync(DateTime? until, CancellationToken cancellationToken = default)
    {
        _pausedUntil = until;
        return Task.CompletedTask;
    }

    public Task<AIProviderSettings> GetProviderSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_provider);

    public Task SaveProviderSettingsAsync(AIProviderSettings settings, CancellationToken cancellationToken = default)
    {
        _provider = settings;
        return Task.CompletedTask;
    }
}

internal sealed class ControllableAI : IAIService
{
    private int _activeCount;

    public string ModelName => "TestAI";
    public bool IsConnected { get; private set; } = true;
    public AIProviderKind ProviderKind => AIProviderKind.Mock;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    public string Reply { get; set; } = "AI回复内容";
    public int CallCount { get; private set; }
    public int CancelledCount { get; private set; }
    public int MaxObservedActive { get; private set; }
    public TaskCompletionSource? HoldGate { get; set; }
    public IReadOnlyList<AIContextMessage>? LastMessages { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public async Task<AIResponse> GenerateAsync(AIRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastMessages = request.Messages.ToList();
        await WaitAsync(cancellationToken);
        return new AIResponse
        {
            Content = Reply,
            GenerationId = request.GenerationId,
            ContactId = request.ContactId,
            Status = AIGenerationStatus.Completed
        };
    }

    public async IAsyncEnumerable<AIStreamEvent> GenerateStreamAsync(
        AIRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastMessages = request.Messages.ToList();
        await WaitAsync(cancellationToken);
        yield return new AIStreamEvent(Reply, IsDone: false);
        yield return new AIStreamEvent(null, IsDone: true);
    }

    public Task<AIConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AIConnectionTestResult(true, "ok", 1, AIErrorKind.None));

    private async Task WaitAsync(CancellationToken cancellationToken)
    {
        var active = Interlocked.Increment(ref _activeCount);
        MaxObservedActive = Math.Max(MaxObservedActive, active);
        try
        {
            if (HoldGate is not null)
            {
                await HoldGate.Task.WaitAsync(cancellationToken);
            }
            else
            {
                await Task.Delay(Delay, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            CancelledCount++;
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _activeCount);
        }
    }
}
