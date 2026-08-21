using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class AIPanelViewModel : ViewModelBase
{
    private const string ReplyModeKey = "ai.replyMode";
    private const string ContextLengthKey = "ai.contextLength";
    private const string AutoGenerateKey = "ai.autoGenerateOnReceive";

    private readonly IAIService _aiService;
    private readonly AIOrchestrator _orchestrator;
    private readonly IClipboardService _clipboard;
    private readonly IToastService _toast;
    private readonly ISettingsStore _settings;
    private readonly SqliteStore _sqlite;
    private readonly ILogger<AIPanelViewModel> _logger;

    public AIPanelViewModel(
        IAIService aiService,
        AIOrchestrator orchestrator,
        IClipboardService clipboard,
        IToastService toast,
        ISettingsStore settings,
        SqliteStore sqlite,
        ILogger<AIPanelViewModel> logger)
    {
        _aiService = aiService;
        _orchestrator = orchestrator;
        _clipboard = clipboard;
        _toast = toast;
        _settings = settings;
        _sqlite = sqlite;
        _logger = logger;
        ModelName = aiService.ModelName;
    }

    public ObservableCollection<AIReplyHistoryItem> ReplyHistory { get; } = [];

    [ObservableProperty]
    private string _modelName = "DeepSeek-V3";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAutoMode))]
    [NotifyPropertyChangedFor(nameof(IsManualMode))]
    [NotifyPropertyChangedFor(nameof(IsOffMode))]
    private AIReplyMode _replyMode = AIReplyMode.Auto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContext5))]
    [NotifyPropertyChangedFor(nameof(IsContext10))]
    [NotifyPropertyChangedFor(nameof(IsContext20))]
    [NotifyPropertyChangedFor(nameof(IsContext50))]
    private int _contextLength = 10;

    public bool IsAutoMode => ReplyMode == AIReplyMode.Auto;
    public bool IsManualMode => ReplyMode == AIReplyMode.ManualConfirm;
    public bool IsOffMode => ReplyMode == AIReplyMode.Off;
    public bool IsContext5 => ContextLength == 5;
    public bool IsContext10 => ContextLength == 10;
    public bool IsContext20 => ContextLength == 20;
    public bool IsContext50 => ContextLength == 50;

    [ObservableProperty]
    private bool _autoGenerateOnReceive = true;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _typingPreview = string.Empty;

    [ObservableProperty]
    private string? _latestGeneratedReply;

    public async Task InitializeAsync()
    {
        await RestoreSettingsAsync();
        await _aiService.ConnectAsync();
        IsConnected = _aiService.IsConnected;

        var history = await _sqlite.GetHistoryAsync(200);
        ReplyHistory.Clear();
        foreach (var item in history)
        {
            ReplyHistory.Add(item);
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            await _aiService.ConnectAsync();
            IsConnected = _aiService.IsConnected;
            if (IsConnected)
            {
                await _toast.ShowAsync("AI 已连接");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connect failed");
            await _toast.ShowAsync("连接失败");
        }
    }

    [RelayCommand]
    private void SetReplyMode(string mode)
    {
        ReplyMode = mode switch
        {
            "Manual" => AIReplyMode.ManualConfirm,
            "Off" => AIReplyMode.Off,
            _ => AIReplyMode.Auto
        };
    }

    [RelayCommand]
    private void SetContextLength(string value)
    {
        if (int.TryParse(value, out var length))
        {
            ContextLength = length;
        }
    }

    [RelayCommand]
    private async Task CopyReplyAsync(AIReplyHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        LatestGeneratedReply = item.Content;
        await _clipboard.SetTextAsync(item.Content);
        await _toast.ShowAsync("已复制");
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _sqlite.ClearHistoryAsync();
        ReplyHistory.Clear();
        await _toast.ShowAsync("历史已清空");
    }

    public Task<string?> GenerateReplyAsync(IReadOnlyList<ChatMessage> messages, string contactName)
        => GenerateForContactAsync(new AIGenerationRequest
        {
            ContactId = string.Empty,
            ContactName = contactName,
            ContextSnapshot = messages.ToList(),
            ContextLength = ContextLength,
            ReplyMode = ReplyMode
        });

    public async Task<string?> GenerateForContactAsync(AIGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ReplyMode == AIReplyMode.Off)
        {
            await _toast.ShowAsync("AI 已关闭");
            return null;
        }

        request.ContextLength = ContextLength;
        request.ReplyMode = ReplyMode;

        try
        {
            IsGenerating = true;
            TypingPreview = string.Empty;

            var result = await _orchestrator.GenerateAsync(
                request,
                chunk => Dispatcher.UIThread.Post(() => TypingPreview = chunk));

            if (result is null)
            {
                return null;
            }

            var history = new AIReplyHistoryItem
            {
                Timestamp = DateTime.Now,
                Status = ReplyMode == AIReplyMode.Auto ? "自动回复" : "手动确认",
                Content = result.Content,
                ContactName = request.ContactName,
                ContactId = request.ContactId
            };

            ReplyHistory.Insert(0, history);
            while (ReplyHistory.Count > 200)
            {
                ReplyHistory.RemoveAt(ReplyHistory.Count - 1);
            }

            await _sqlite.InsertHistoryAsync(history);
            LatestGeneratedReply = result.Content;
            return result.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI generate failed");
            await _toast.ShowAsync("生成失败");
            return null;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task RegenerateAsync()
    {
        var last = _orchestrator.LastRequest;
        if (last is null)
        {
            await _toast.ShowAsync("没有可重新生成的上下文");
            return;
        }

        var request = new AIGenerationRequest
        {
            GenerationId = Guid.NewGuid().ToString("N"),
            ContactId = last.ContactId,
            ContactName = last.ContactName,
            ContextSnapshot = last.ContextSnapshot.ToList(),
            ContextLength = ContextLength,
            ReplyMode = ReplyMode
        };

        var reply = await GenerateForContactAsync(request);
        if (reply is not null && ReplyHistory.Count > 0)
        {
            ReplyHistory[0].Status = "重新生成";
        }
    }

    partial void OnReplyModeChanged(AIReplyMode value)
        => _ = PersistAsync(ReplyModeKey, value.ToString());

    partial void OnContextLengthChanged(int value)
        => _ = PersistAsync(ContextLengthKey, value.ToString());

    partial void OnAutoGenerateOnReceiveChanged(bool value)
        => _ = PersistAsync(AutoGenerateKey, value ? "1" : "0");

    private async Task RestoreSettingsAsync()
    {
        try
        {
            var mode = await _settings.GetAsync(ReplyModeKey);
            if (Enum.TryParse<AIReplyMode>(mode, true, out var parsed))
            {
                ReplyMode = parsed;
            }

            var length = await _settings.GetAsync(ContextLengthKey);
            if (int.TryParse(length, out var contextLength))
            {
                ContextLength = contextLength;
            }

            var auto = await _settings.GetAsync(AutoGenerateKey);
            if (auto is "0" or "1")
            {
                AutoGenerateOnReceive = auto == "1";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore AI settings");
        }
    }

    private async Task PersistAsync(string key, string value)
    {
        try
        {
            await _settings.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist setting {Key}", key);
        }
    }
}
