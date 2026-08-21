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
    private readonly IAIService _aiService;
    private readonly AIOrchestrator _orchestrator;
    private readonly IAIContextBuilder _contextBuilder;
    private readonly IAISettingsService _aiSettings;
    private readonly IClipboardService _clipboard;
    private readonly IToastService _toast;
    private readonly SqliteStore _sqlite;
    private readonly ILogger<AIPanelViewModel> _logger;
    private string? _boundContactId;
    private bool _suppressPersist;
    private AIContextBuildResult? _lastBuildResult;

    public AIPanelViewModel(
        IAIService aiService,
        AIOrchestrator orchestrator,
        IAIContextBuilder contextBuilder,
        IAISettingsService aiSettings,
        IClipboardService clipboard,
        IToastService toast,
        SqliteStore sqlite,
        ILogger<AIPanelViewModel> logger)
    {
        _aiService = aiService;
        _orchestrator = orchestrator;
        _contextBuilder = contextBuilder;
        _aiSettings = aiSettings;
        _clipboard = clipboard;
        _toast = toast;
        _sqlite = sqlite;
        _logger = logger;
        ModelName = aiService.ModelName;
    }

    public ObservableCollection<AIReplyHistoryItem> ReplyHistory { get; } = [];
    public ObservableCollection<AIContextMessage> PreviewMessages { get; } = [];

    public AIContextBuildResult? LastBuildResult
    {
        get => _lastBuildResult;
        private set
        {
            if (SetProperty(ref _lastBuildResult, value))
            {
                ContextSummaryText = value?.SummaryText ?? string.Empty;
            }
        }
    }

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
    [NotifyPropertyChangedFor(nameof(ContextCount))]
    private int _contextLength = 10;

    /// <summary>Alias for ContextLength (UI/compat).</summary>
    public int ContextCount
    {
        get => ContextLength;
        set => ContextLength = value;
    }

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
    private bool _includeOwnMessages = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStyleNatural))]
    [NotifyPropertyChangedFor(nameof(IsStyleConcise))]
    [NotifyPropertyChangedFor(nameof(IsStyleFormal))]
    [NotifyPropertyChangedFor(nameof(IsStyleHumorous))]
    private ReplyStyle _replyStyle = ReplyStyle.Natural;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLengthShort))]
    [NotifyPropertyChangedFor(nameof(IsLengthMedium))]
    [NotifyPropertyChangedFor(nameof(IsLengthLong))]
    private ReplyLength _replyLength = ReplyLength.Medium;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTriggerAll))]
    [NotifyPropertyChangedFor(nameof(IsTriggerMentionOrQuote))]
    [NotifyPropertyChangedFor(nameof(IsTriggerOff))]
    private GroupTriggerMode _groupTriggerMode = GroupTriggerMode.MentionOrQuoteMe;

    public bool IsStyleNatural => ReplyStyle == ReplyStyle.Natural;
    public bool IsStyleConcise => ReplyStyle == ReplyStyle.Concise;
    public bool IsStyleFormal => ReplyStyle == ReplyStyle.Formal;
    public bool IsStyleHumorous => ReplyStyle == ReplyStyle.Humorous;
    public bool IsLengthShort => ReplyLength == ReplyLength.Short;
    public bool IsLengthMedium => ReplyLength == ReplyLength.Medium;
    public bool IsLengthLong => ReplyLength == ReplyLength.Long;
    public bool IsTriggerAll => GroupTriggerMode == GroupTriggerMode.AllMessages;
    public bool IsTriggerMentionOrQuote => GroupTriggerMode == GroupTriggerMode.MentionOrQuoteMe;
    public bool IsTriggerOff => GroupTriggerMode == GroupTriggerMode.Off;

    [ObservableProperty]
    private string _temporaryInstruction = string.Empty;

    [ObservableProperty]
    private string _contextSummaryText = string.Empty;

    [ObservableProperty]
    private string _pinnedCountText = "置顶 0";

    [ObservableProperty]
    private bool _isMoreSettingsExpanded;

    [ObservableProperty]
    private bool _isUsingContactOverride;

    [ObservableProperty]
    private bool _isContextPreviewOpen;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _typingPreview = string.Empty;

    [ObservableProperty]
    private string? _latestGeneratedReply;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAutoPaused))]
    private DateTime? _autoPausedUntil;

    public bool IsAutoPaused => AutoPausedUntil is { } until && until > DateTime.Now;

    public async Task InitializeAsync()
    {
        await BindContactAsync(null);
        await _aiService.ConnectAsync();
        IsConnected = _aiService.IsConnected;

        var history = await _sqlite.GetHistoryAsync(200);
        ReplyHistory.Clear();
        foreach (var item in history)
        {
            ReplyHistory.Add(item);
        }

        AutoPausedUntil = await _aiSettings.GetAutoPausedUntilAsync();
    }

    public async Task BindContactAsync(string? contactId)
    {
        _boundContactId = string.IsNullOrWhiteSpace(contactId) ? null : contactId;
        _suppressPersist = true;
        try
        {
            if (_boundContactId is null)
            {
                var global = await _aiSettings.GetGlobalAsync();
                ApplyEffective(new EffectiveAISettings
                {
                    ContactId = string.Empty,
                    IsUsingOverride = false,
                    ReplyMode = global.ReplyMode,
                    ContextCount = global.ContextCount,
                    IncludeOwnMessages = global.IncludeOwnMessages,
                    ReplyStyle = global.ReplyStyle,
                    ReplyLength = global.ReplyLength,
                    AutoGenerateOnReceive = global.AutoGenerateOnReceive,
                    GroupTriggerMode = global.GroupTriggerMode
                });
            }
            else
            {
                var effective = await _aiSettings.GetEffectiveAsync(_boundContactId);
                ApplyEffective(effective);
                var pins = await _aiSettings.GetPinnedIdsAsync(_boundContactId);
                PinnedCountText = $"置顶 {pins.Count}";
            }
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    public EffectiveAISettings CaptureEffectiveSnapshot() => new()
    {
        ContactId = _boundContactId ?? string.Empty,
        IsUsingOverride = IsUsingContactOverride,
        ReplyMode = ReplyMode,
        ContextCount = ContextLength,
        IncludeOwnMessages = IncludeOwnMessages,
        ReplyStyle = ReplyStyle,
        ReplyLength = ReplyLength,
        AutoGenerateOnReceive = AutoGenerateOnReceive,
        GroupTriggerMode = GroupTriggerMode
    };

    public async Task RefreshContextPreviewAsync(
        IReadOnlyList<ChatMessage> messages,
        string contactId,
        string contactName,
        bool isGroup)
    {
        var pins = await _aiSettings.GetPinnedIdsAsync(contactId);
        var input = new AIContextBuildInput
        {
            ContactId = contactId,
            ContactName = contactName,
            IsGroup = isGroup,
            Messages = messages,
            ContextCount = ContextLength,
            IncludeOwnMessages = IncludeOwnMessages,
            PinnedMessageIds = pins,
            TemporaryInstruction = string.IsNullOrWhiteSpace(TemporaryInstruction) ? null : TemporaryInstruction,
            ReplyStyle = ReplyStyle,
            ReplyLength = ReplyLength
        };
        var result = _contextBuilder.Build(input);
        LastBuildResult = result;
        PreviewMessages.Clear();
        foreach (var msg in result.Messages)
        {
            PreviewMessages.Add(msg);
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
    private void SetReplyStyle(string value)
    {
        if (Enum.TryParse<ReplyStyle>(value, true, out var style))
        {
            ReplyStyle = style;
        }
    }

    [RelayCommand]
    private void SetReplyLength(string value)
    {
        if (Enum.TryParse<ReplyLength>(value, true, out var length))
        {
            ReplyLength = length;
        }
    }

    [RelayCommand]
    private void SetGroupTrigger(string value)
    {
        if (Enum.TryParse<GroupTriggerMode>(value, true, out var mode))
        {
            GroupTriggerMode = mode;
        }
    }

    [RelayCommand]
    private void ToggleMoreSettings() => IsMoreSettingsExpanded = !IsMoreSettingsExpanded;

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

    [RelayCommand]
    private async Task RestoreGlobalDefaultsAsync()
    {
        if (string.IsNullOrWhiteSpace(_boundContactId))
        {
            await _toast.ShowAsync("当前为全局设置");
            return;
        }

        await _aiSettings.ClearOverrideAsync(_boundContactId);
        await BindContactAsync(_boundContactId);
        await _toast.ShowAsync("已恢复全局默认");
    }

    [RelayCommand]
    private async Task ToggleContextPreviewAsync()
    {
        if (!IsContextPreviewOpen)
        {
            // Opening: caller should have refreshed; still flip open.
            IsContextPreviewOpen = true;
        }
        else
        {
            IsContextPreviewOpen = false;
        }

        await Task.CompletedTask;
    }

    public void OpenPreviewWith(AIContextBuildResult result)
    {
        LastBuildResult = result;
        PreviewMessages.Clear();
        foreach (var msg in result.Messages)
        {
            PreviewMessages.Add(msg);
        }

        IsContextPreviewOpen = true;
    }

    [RelayCommand]
    private async Task PauseAutoMinutesAsync(string? minutesText)
    {
        var minutes = 30;
        if (int.TryParse(minutesText, out var parsed) && parsed > 0)
        {
            minutes = parsed;
        }

        AutoPausedUntil = DateTime.Now.AddMinutes(minutes);
        await _aiSettings.SetAutoPausedUntilAsync(AutoPausedUntil);
        OnPropertyChanged(nameof(IsAutoPaused));
        await _toast.ShowAsync($"已暂停自动回复 {minutes} 分钟");
    }

    [RelayCommand]
    private async Task ResumeAutoAsync()
    {
        AutoPausedUntil = null;
        await _aiSettings.SetAutoPausedUntilAsync(null);
        OnPropertyChanged(nameof(IsAutoPaused));
        await _toast.ShowAsync("已恢复自动回复");
    }

    [RelayCommand]
    private void CancelGeneration()
    {
        _orchestrator.CancelCurrentGeneration();
        IsGenerating = false;
        TypingPreview = string.Empty;
    }

    [RelayCommand]
    private async Task RegenerateSameContextAsync()
    {
        var last = _orchestrator.LastRequest;
        if (last is null)
        {
            await _toast.ShowAsync("没有可重新生成的上下文");
            return;
        }

        var request = CloneRequest(last);
        var reply = await GenerateForContactAsync(request);
        if (reply is not null && ReplyHistory.Count > 0)
        {
            ReplyHistory[0].Status = "重新生成";
        }
    }

    [RelayCommand]
    private async Task RegenerateFreshContextAsync()
    {
        var last = _orchestrator.LastRequest;
        if (last is null)
        {
            await _toast.ShowAsync("没有可重新生成的上下文");
            return;
        }

        // Fresh: keep contact/settings but new GenerationId; caller should refresh snapshot.
        // If snapshot already on last request, still regenerate with current panel settings.
        var request = CloneRequest(last);
        request.ContextLength = ContextLength;
        request.IncludeOwnMessages = IncludeOwnMessages;
        request.ReplyStyle = ReplyStyle;
        request.ReplyLength = ReplyLength;
        request.TemporaryInstruction = string.IsNullOrWhiteSpace(TemporaryInstruction) ? null : TemporaryInstruction;
        var reply = await GenerateForContactAsync(request);
        if (reply is not null && ReplyHistory.Count > 0)
        {
            ReplyHistory[0].Status = "刷新上下文";
        }
    }

    [RelayCommand]
    private async Task RegenerateAsync() => await RegenerateSameContextAsync();

    public Task<string?> GenerateReplyAsync(IReadOnlyList<ChatMessage> messages, string contactName)
        => GenerateForContactAsync(new AIGenerationRequest
        {
            ContactId = _boundContactId ?? string.Empty,
            ContactName = contactName,
            ContextSnapshot = messages.ToList(),
            ContextLength = ContextLength,
            ReplyMode = ReplyMode,
            IncludeOwnMessages = IncludeOwnMessages,
            ReplyStyle = ReplyStyle,
            ReplyLength = ReplyLength,
            TemporaryInstruction = string.IsNullOrWhiteSpace(TemporaryInstruction) ? null : TemporaryInstruction
        });

    public async Task<AIGenerationResult?> GenerateForContactDetailedAsync(AIGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ReplyMode == AIReplyMode.Off)
        {
            await _toast.ShowAsync("AI 已关闭");
            return null;
        }

        // Fill from panel effective settings when not already set by caller
        request.ContextLength = request.ContextLength > 0 ? request.ContextLength : ContextLength;
        request.ReplyMode = ReplyMode;
        request.IncludeOwnMessages = IncludeOwnMessages;
        request.ReplyStyle = ReplyStyle;
        request.ReplyLength = ReplyLength;
        if (request.TemporaryInstruction is null && !string.IsNullOrWhiteSpace(TemporaryInstruction))
        {
            request.TemporaryInstruction = TemporaryInstruction;
        }

        if ((request.PinnedMessageIds is null || request.PinnedMessageIds.Count == 0)
            && !string.IsNullOrWhiteSpace(request.ContactId))
        {
            request.PinnedMessageIds = await _aiSettings.GetPinnedIdsAsync(request.ContactId);
        }

        var hadTempInstruction = !string.IsNullOrWhiteSpace(request.TemporaryInstruction);

        try
        {
            IsGenerating = true;
            TypingPreview = string.Empty;

            // Preview / summary before generate
            var preview = _contextBuilder.Build(new AIContextBuildInput
            {
                ContactId = request.ContactId,
                ContactName = request.ContactName,
                IsGroup = request.IsGroup,
                Messages = request.ContextSnapshot,
                ContextCount = Math.Max(1, request.ContextLength),
                IncludeOwnMessages = request.IncludeOwnMessages,
                PinnedMessageIds = request.PinnedMessageIds ?? Array.Empty<string>(),
                TemporaryInstruction = request.TemporaryInstruction,
                ReplyStyle = request.ReplyStyle,
                ReplyLength = request.ReplyLength,
                TokenBudget = request.TokenBudget > 0 ? request.TokenBudget : 3500,
                TemporarilyExcludedMessageIds = request.TemporarilyExcludedMessageIds
            });
            LastBuildResult = preview;
            ContextSummaryText = preview.SummaryText;
            PreviewMessages.Clear();
            foreach (var msg in preview.Messages)
            {
                PreviewMessages.Add(msg);
            }

            var result = await _orchestrator.GenerateAsync(
                request,
                chunk => Dispatcher.UIThread.Post(() => TypingPreview = chunk));

            if (result is null)
            {
                return null;
            }

            if (_orchestrator.LastBuildResult is not null)
            {
                LastBuildResult = _orchestrator.LastBuildResult;
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

            if (hadTempInstruction)
            {
                TemporaryInstruction = string.Empty;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI generate failed");
            await _toast.ShowAsync("生成失败");
            // keep TemporaryInstruction on failure
            return null;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public async Task<string?> GenerateForContactAsync(AIGenerationRequest request)
    {
        var result = await GenerateForContactDetailedAsync(request);
        return result?.Content;
    }

    partial void OnReplyModeChanged(AIReplyMode value) => _ = PersistSettingsAsync();
    partial void OnContextLengthChanged(int value)
    {
        OnPropertyChanged(nameof(ContextCount));
        _ = PersistSettingsAsync();
    }
    partial void OnAutoGenerateOnReceiveChanged(bool value) => _ = PersistSettingsAsync();
    partial void OnIncludeOwnMessagesChanged(bool value) => _ = PersistSettingsAsync();
    partial void OnReplyStyleChanged(ReplyStyle value) => _ = PersistSettingsAsync();
    partial void OnReplyLengthChanged(ReplyLength value) => _ = PersistSettingsAsync();
    partial void OnGroupTriggerModeChanged(GroupTriggerMode value) => _ = PersistSettingsAsync();

    private void ApplyEffective(EffectiveAISettings effective)
    {
        ReplyMode = effective.ReplyMode;
        ContextLength = effective.ContextCount;
        IncludeOwnMessages = effective.IncludeOwnMessages;
        ReplyStyle = effective.ReplyStyle;
        ReplyLength = effective.ReplyLength;
        AutoGenerateOnReceive = effective.AutoGenerateOnReceive;
        GroupTriggerMode = effective.GroupTriggerMode;
        IsUsingContactOverride = effective.IsUsingOverride;
    }

    private async Task PersistSettingsAsync()
    {
        if (_suppressPersist)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_boundContactId))
            {
                var ov = new AIContactOverride
                {
                    ContactId = _boundContactId,
                    UseOverride = true,
                    ReplyMode = ReplyMode,
                    ContextCount = ContextLength,
                    IncludeOwnMessages = IncludeOwnMessages,
                    ReplyStyle = ReplyStyle,
                    ReplyLength = ReplyLength,
                    AutoGenerateOnReceive = AutoGenerateOnReceive,
                    GroupTriggerMode = GroupTriggerMode
                };
                await _aiSettings.SaveOverrideAsync(ov);
                IsUsingContactOverride = true;
            }
            else
            {
                await _aiSettings.SaveGlobalAsync(new AIGlobalSettings
                {
                    ReplyMode = ReplyMode,
                    ContextCount = ContextLength,
                    IncludeOwnMessages = IncludeOwnMessages,
                    ReplyStyle = ReplyStyle,
                    ReplyLength = ReplyLength,
                    AutoGenerateOnReceive = AutoGenerateOnReceive,
                    GroupTriggerMode = GroupTriggerMode
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI settings");
        }
    }

    private static AIGenerationRequest CloneRequest(AIGenerationRequest last)
    {
        return new AIGenerationRequest
        {
            GenerationId = Guid.NewGuid().ToString("N"),
            ContactId = last.ContactId,
            ContactName = last.ContactName,
            ContextSnapshot = last.ContextSnapshot.ToList(),
            ContextLength = last.ContextLength,
            ReplyMode = last.ReplyMode,
            IncludeOwnMessages = last.IncludeOwnMessages,
            TemporaryInstruction = last.TemporaryInstruction,
            ReplyStyle = last.ReplyStyle,
            ReplyLength = last.ReplyLength,
            PinnedMessageIds = last.PinnedMessageIds.ToList(),
            TemporarilyExcludedMessageIds = last.TemporarilyExcludedMessageIds,
            DraftRevisionAtStart = last.DraftRevisionAtStart,
            IsGroup = last.IsGroup,
            TokenBudget = last.TokenBudget
        };
    }
}
