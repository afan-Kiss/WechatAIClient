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
            NullLogger<ChatViewModel>.Instance);
    }

    public static ContactListViewModel CreateContacts(MockWechatService? wechat = null)
    {
        wechat ??= CreateWechat();
        return new ContactListViewModel(wechat, NullLogger<ContactListViewModel>.Instance);
    }

    public static AIOrchestrator CreateOrchestrator(IAIService? ai = null)
        => new(ai ?? new MockAIService(NullLogger<MockAIService>.Instance), NullLogger<AIOrchestrator>.Instance);
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

internal sealed class ControllableAI : IAIService
{
    public string ModelName => "TestAI";
    public bool IsConnected { get; private set; } = true;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    public string Reply { get; set; } = "AI回复内容";
    public int CallCount { get; private set; }
    public IReadOnlyList<ChatMessage>? LastContext { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public async Task<string> GenerateReplyAsync(IReadOnlyList<ChatMessage> context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastContext = context.ToList();
        await Task.Delay(Delay, cancellationToken);
        return Reply;
    }
}
