using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class AIPanelViewModel : ViewModelBase
{
    private readonly IAIService _aiService;
    private readonly ILogger<AIPanelViewModel> _logger;

    public AIPanelViewModel(IAIService aiService, ILogger<AIPanelViewModel> logger)
    {
        _aiService = aiService;
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
        await _aiService.ConnectAsync();
        IsConnected = _aiService.IsConnected;
        ReplyHistory.Add(new AIReplyHistoryItem
        {
            Timestamp = DateTime.Today.AddHours(10).AddMinutes(18),
            Status = "自动回复",
            Content = "收到，这个方案整体方向很清晰，我建议先把核心交互流程定下来。",
            ContactName = "产品设计交流组"
        });
        ReplyHistory.Add(new AIReplyHistoryItem
        {
            Timestamp = DateTime.Today.AddHours(9).AddMinutes(52),
            Status = "手动确认",
            Content = "可以的。我稍后整理一版更简洁的回复，方便你直接发送。",
            ContactName = "李明远"
        });
        ReplyHistory.Add(new AIReplyHistoryItem
        {
            Timestamp = DateTime.Today.AddHours(9).AddMinutes(20),
            Status = "自动回复",
            Content = "理解你的意思了。当前上下文里大家都认可深色玻璃拟态。",
            ContactName = "前端研发小队"
        });
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
    private void CopyReply(AIReplyHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        LatestGeneratedReply = item.Content;
        _logger.LogInformation("Copied AI reply {Id}", item.Id);
    }

    public async Task<string?> GenerateReplyAsync(IReadOnlyList<ChatMessage> messages, string contactName)
    {
        if (ReplyMode == AIReplyMode.Off)
        {
            return null;
        }

        try
        {
            IsGenerating = true;
            TypingPreview = string.Empty;
            var context = messages.TakeLast(ContextLength).ToList();
            var reply = await _aiService.GenerateReplyAsync(context);
            await AnimateTypingAsync(reply);

            var history = new AIReplyHistoryItem
            {
                Timestamp = DateTime.Now,
                Status = ReplyMode == AIReplyMode.Auto ? "自动回复" : "手动确认",
                Content = reply,
                ContactName = contactName
            };
            ReplyHistory.Insert(0, history);
            LatestGeneratedReply = reply;
            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI generate failed");
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
        if (LatestGeneratedReply is null)
        {
            return;
        }

        IsGenerating = true;
        try
        {
            var reply = await _aiService.GenerateReplyAsync([]);
            await AnimateTypingAsync(reply);
            LatestGeneratedReply = reply;
            ReplyHistory.Insert(0, new AIReplyHistoryItem
            {
                Timestamp = DateTime.Now,
                Status = "重新生成",
                Content = reply,
                ContactName = "当前会话"
            });
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task AnimateTypingAsync(string text)
    {
        TypingPreview = string.Empty;
        foreach (var ch in text)
        {
            TypingPreview += ch;
            await Task.Delay(12);
        }
    }
}
