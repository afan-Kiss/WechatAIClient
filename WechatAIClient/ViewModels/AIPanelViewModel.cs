using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.DeepSeek;

namespace WechatAIClient.ViewModels;

public partial class AIPanelViewModel : ViewModelBase
{
    private readonly IAIService _aiService;
    private readonly AIOrchestrator _orchestrator;
    private readonly IAIContextBuilder _contextBuilder;
    private readonly IAISettingsService _aiSettings;
    private readonly ISecretStore _secrets;
    private readonly IClipboardService _clipboard;
    private readonly IToastService _toast;
    private readonly SqliteStore _sqlite;
    private readonly ILogger<AIPanelViewModel> _logger;
    private string? _boundContactId;
    private string? _boundAccountId;
    private bool _suppressPersist;
    private AIContextBuildResult? _lastBuildResult;
    private IReadOnlyList<ChatMessage> _lastPreviewSource = Array.Empty<ChatMessage>();
    private string _lastPreviewContactId = "";
    private string _lastPreviewAccountId = "";
    private string _lastPreviewContactName = "";
    private bool _lastPreviewIsGroup;
    private EventHandler? _statusHandler;

    public AIPanelViewModel(
        IAIService aiService,
        AIOrchestrator orchestrator,
        IAIContextBuilder contextBuilder,
        IAISettingsService aiSettings,
        ISecretStore secrets,
        IClipboardService clipboard,
        IToastService toast,
        SqliteStore sqlite,
        ILogger<AIPanelViewModel> logger)
    {
        _aiService = aiService;
        _orchestrator = orchestrator;
        _contextBuilder = contextBuilder;
        _aiSettings = aiSettings;
        _secrets = secrets;
        _clipboard = clipboard;
        _toast = toast;
        _sqlite = sqlite;
        _logger = logger;
        ModelName = aiService.ModelName;

        _statusHandler = (_, _) => Dispatcher.UIThread.Post(SyncGenerationStatus);
        _orchestrator.StatusChanged += _statusHandler;
    }

    public ObservableCollection<AIReplyHistoryItem> ReplyHistory { get; } = [];
    public ObservableCollection<AIContextMessage> PreviewMessages { get; } = [];
    public ObservableCollection<string> TemporarilyExcludedMessageIds { get; } = [];

    public AIContextBuildResult? LastBuildResult
    {
        get => _lastBuildResult;
        private set
        {
            if (SetProperty(ref _lastBuildResult, value))
            {
                ContextSummaryText = value?.SummaryText ?? string.Empty;
                TokenEstimateText = value is null
                    ? string.Empty
                    : value.EstimatedTokens >= 1000
                        ? $"约 {value.EstimatedTokens / 1000.0:0.0}K tokens"
                        : $"约 {value.EstimatedTokens} tokens";
            }
        }
    }

    [ObservableProperty]
    private string _modelName = "Mock-AI";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private AIConnectionState _connectionState = AIConnectionState.NotConfigured;

    public bool IsConnected => ConnectionState == AIConnectionState.Connected;

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
    private string _tokenEstimateText = string.Empty;

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
    [NotifyPropertyChangedFor(nameof(IsStreaming))]
    private AIGenerationStatus _generationStatus = AIGenerationStatus.Idle;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool IsStreaming => GenerationStatus == AIGenerationStatus.Streaming;

    [ObservableProperty]
    private string _typingPreview = string.Empty;

    [ObservableProperty]
    private string _streamingPreview = string.Empty;

    [ObservableProperty]
    private string? _latestGeneratedReply;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAutoPaused))]
    private DateTime? _autoPausedUntil;

    public bool IsAutoPaused => AutoPausedUntil is { } until && until > DateTime.Now;

    public string? ActiveGenerationContactId => _orchestrator.ActiveContactId;

    public async Task InitializeAsync()
    {
        await BindContactAsync(null);
        await RefreshConnectionStateAsync();
        // Do not block startup with a live connection probe.
        ModelName = _aiService.ModelName;

        var history = await _sqlite.GetHistoryAsync(200);
        ReplyHistory.Clear();
        foreach (var item in history)
        {
            ReplyHistory.Add(item);
        }

        AutoPausedUntil = await _aiSettings.GetAutoPausedUntilAsync();
    }

    public async Task BindContactAsync(string? contactId, string? accountId = null)
    {
        _boundContactId = string.IsNullOrWhiteSpace(contactId) ? null : contactId;
        _boundAccountId = string.IsNullOrWhiteSpace(contactId)
            ? null
            : SqliteStore.NormalizeAccountId(accountId);
        TemporarilyExcludedMessageIds.Clear();
        _suppressPersist = true;
        try
        {
            if (_boundContactId is null)
            {
                var global = await _aiSettings.GetGlobalAsync();
                ApplyEffective(new EffectiveAISettings
                {
                    AccountId = string.Empty,
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
                var effective = await _aiSettings.GetEffectiveAsync(_boundAccountId!, _boundContactId);
                ApplyEffective(effective);
                var pins = await _aiSettings.GetPinnedIdsAsync(_boundAccountId!, _boundContactId);
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
        AccountId = _boundAccountId ?? string.Empty,
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
        bool isGroup,
        string? accountId = null)
    {
        _lastPreviewSource = messages.ToList();
        _lastPreviewContactId = contactId;
        _lastPreviewAccountId = SqliteStore.NormalizeAccountId(accountId);
        _lastPreviewContactName = contactName;
        _lastPreviewIsGroup = isGroup;

        var pins = await _aiSettings.GetPinnedIdsAsync(_lastPreviewAccountId, contactId);
        var excluded = new HashSet<string>(TemporarilyExcludedMessageIds, StringComparer.Ordinal);

        AIContextBuildInput MakeInput(HashSet<string>? tempExclude) => new()
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
            ReplyLength = ReplyLength,
            TemporarilyExcludedMessageIds = tempExclude is { Count: > 0 } ? tempExclude : null
        };

        // Final payload / summary always use exclusions (same builder as AIRequest).
        var finalResult = _contextBuilder.Build(MakeInput(excluded));
        LastBuildResult = finalResult;

        // Preview list: candidates without temp-exclude so unchecked rows stay visible.
        var displayResult = excluded.Count == 0
            ? finalResult
            : _contextBuilder.Build(MakeInput(null));

        PreviewMessages.Clear();
        foreach (var msg in displayResult.Messages)
        {
            if (msg.IsSystemRole)
            {
                continue;
            }

            msg.IsIncludedInRequest = !excluded.Contains(msg.MessageId);
            PreviewMessages.Add(msg);
        }

        OnPropertyChanged(nameof(ContextSummaryText));
        OnPropertyChanged(nameof(TokenEstimateText));
        // LastBuildResult setter already updates summary/token; ensure UI refresh if same reference path
        if (LastBuildResult is not null)
        {
            ContextSummaryText = LastBuildResult.SummaryText;
            TokenEstimateText = LastBuildResult.EstimatedTokens >= 1000
                ? $"约 {LastBuildResult.EstimatedTokens / 1000.0:0.0}K tokens"
                : $"约 {LastBuildResult.EstimatedTokens} tokens";
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            await _aiService.ConnectAsync();
            await RefreshConnectionStateAsync(connectedHint: _aiService.IsConnected);
            ModelName = _aiService.ModelName;
            if (IsConnected)
            {
                await _toast.ShowAsync("AI 已连接");
            }
        }
        catch (AIServiceException ex)
        {
            ConnectionState = AIConnectionState.Failed;
            OnPropertyChanged(nameof(IsConnected));
            await _toast.ShowAsync(ex.UserMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connect failed");
            ConnectionState = AIConnectionState.Failed;
            OnPropertyChanged(nameof(IsConnected));
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
    private void ToggleExclude(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        var existing = TemporarilyExcludedMessageIds.FirstOrDefault(id =>
            string.Equals(id, messageId, StringComparison.Ordinal));
        if (existing is not null)
        {
            TemporarilyExcludedMessageIds.Remove(existing);
        }
        else
        {
            TemporarilyExcludedMessageIds.Add(messageId);
        }

        _ = RefreshPreviewFromCacheAsync();
    }

    [RelayCommand]
    private void ApplyInstructionChip(string? chip)
    {
        if (string.IsNullOrWhiteSpace(chip))
        {
            return;
        }

        TemporaryInstruction = chip.Trim();
    }

    [RelayCommand]
    private void SelectAllPreview()
    {
        // 全选 = 全部纳入本次请求（清空临时排除）
        TemporarilyExcludedMessageIds.Clear();
        _ = RefreshPreviewFromCacheAsync();
    }

    [RelayCommand]
    private void ResetPreviewExclusions()
    {
        TemporarilyExcludedMessageIds.Clear();
        _ = RefreshPreviewFromCacheAsync();
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

    [RelayCommand]
    private async Task RestoreGlobalDefaultsAsync()
    {
        if (string.IsNullOrWhiteSpace(_boundContactId))
        {
            await _toast.ShowAsync("当前为全局设置");
            return;
        }

        await _aiSettings.ClearOverrideAsync(
            _boundAccountId ?? SqliteStore.LegacyAccountId,
            _boundContactId);
        await BindContactAsync(_boundContactId, _boundAccountId);
        await _toast.ShowAsync("已恢复全局默认");
    }

    [RelayCommand]
    private async Task ToggleContextPreviewAsync()
    {
        IsContextPreviewOpen = !IsContextPreviewOpen;
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
        StreamingPreview = string.Empty;
        GenerationStatus = AIGenerationStatus.Cancelled;
        StatusText = "已取消";
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
            AccountId = _boundAccountId ?? SqliteStore.LegacyAccountId,
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
        request.AccountId = SqliteStore.NormalizeAccountId(request.AccountId);

        if (ReplyMode == AIReplyMode.Off)
        {
            await _toast.ShowAsync("AI 已关闭");
            return null;
        }

        var provider = await _aiSettings.GetProviderSettingsAsync();
        if (provider.Provider == AIProviderKind.DeepSeek)
        {
            var key = await _secrets.GetSecretAsync(DeepSeekAIService.ApiKeySecretName);
            if (string.IsNullOrWhiteSpace(key))
            {
                await _toast.ShowAsync("请先配置 API Key");
                ConnectionState = AIConnectionState.NotConfigured;
                OnPropertyChanged(nameof(IsConnected));
                return null;
            }
        }

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
            request.PinnedMessageIds = await _aiSettings.GetPinnedIdsAsync(request.AccountId, request.ContactId);
        }

        if (TemporarilyExcludedMessageIds.Count > 0)
        {
            request.TemporarilyExcludedMessageIds = new HashSet<string>(TemporarilyExcludedMessageIds, StringComparer.Ordinal);
        }

        var hadTempInstruction = !string.IsNullOrWhiteSpace(request.TemporaryInstruction);

        try
        {
            IsGenerating = true;
            TypingPreview = string.Empty;
            StreamingPreview = string.Empty;
            GenerationStatus = AIGenerationStatus.PreparingContext;
            StatusText = "准备上下文…";

            var excluded = request.TemporarilyExcludedMessageIds;
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
                TemporarilyExcludedMessageIds = excluded
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
                chunk => Dispatcher.UIThread.Post(() =>
                {
                    if (_orchestrator.ActiveContactId is not null &&
                        !string.Equals(_orchestrator.ActiveContactId, request.ContactId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    TypingPreview = chunk;
                    StreamingPreview = chunk;
                    GenerationStatus = AIGenerationStatus.Streaming;
                    StatusText = "生成中…";
                }));

            if (result is null || result.Status != AIGenerationStatus.Completed)
            {
                if (GenerationStatus != AIGenerationStatus.Cancelled)
                {
                    GenerationStatus = AIGenerationStatus.Cancelled;
                    StatusText = "已取消";
                }

                return null;
            }

            if (_orchestrator.LastBuildResult is not null)
            {
                LastBuildResult = _orchestrator.LastBuildResult;
            }

            GenerationStatus = AIGenerationStatus.Completed;
            StatusText = "已完成";
            ModelName = _aiService.ModelName;

            var history = new AIReplyHistoryItem
            {
                Timestamp = DateTime.Now,
                Status = ReplyMode == AIReplyMode.Auto ? "自动回复" : "手动确认",
                Content = result.Content,
                ContactName = request.ContactName,
                ContactId = request.ContactId,
                AccountId = request.AccountId,
                AccountName = string.Empty,
                Model = result.Model ?? _aiService.ModelName,
                RequestId = result.RequestId,
                ContextSummary = result.ContextSummary
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
        catch (AIServiceException ex)
        {
            GenerationStatus = AIGenerationStatus.Failed;
            StatusText = ex.UserMessage;
            await _toast.ShowAsync(ex.UserMessage);
            return null;
        }
        catch (Exception ex)
        {
            GenerationStatus = AIGenerationStatus.Failed;
            StatusText = "生成失败";
            _logger.LogError(ex, "AI generate failed");
            await _toast.ShowAsync("生成失败");
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

    public async Task RefreshConnectionStateAsync(bool? connectedHint = null)
    {
        try
        {
            var provider = await _aiSettings.GetProviderSettingsAsync();
            if (provider.Provider == AIProviderKind.Mock)
            {
                ConnectionState = connectedHint == true || _aiService.IsConnected
                    ? AIConnectionState.Connected
                    : AIConnectionState.Configured;
                OnPropertyChanged(nameof(IsConnected));
                return;
            }

            var key = await _secrets.GetSecretAsync(DeepSeekAIService.ApiKeySecretName);
            if (string.IsNullOrWhiteSpace(key))
            {
                ConnectionState = AIConnectionState.NotConfigured;
            }
            else if (connectedHint == true || (_aiService.IsConnected && _aiService.ProviderKind == AIProviderKind.DeepSeek))
            {
                ConnectionState = AIConnectionState.Connected;
            }
            else if (connectedHint == false)
            {
                ConnectionState = AIConnectionState.Failed;
            }
            else
            {
                ConnectionState = AIConnectionState.Configured;
            }

            OnPropertyChanged(nameof(IsConnected));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RefreshConnectionState failed");
        }
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

    private void SyncGenerationStatus()
    {
        GenerationStatus = _orchestrator.Status;
        StatusText = _orchestrator.Status switch
        {
            AIGenerationStatus.PreparingContext => "准备上下文…",
            AIGenerationStatus.Connecting => "连接中…",
            AIGenerationStatus.Streaming => "生成中…",
            AIGenerationStatus.Completed => "已完成",
            AIGenerationStatus.Cancelled => "已取消",
            AIGenerationStatus.Failed => "失败",
            _ => string.Empty
        };
        IsGenerating = _orchestrator.Status is AIGenerationStatus.PreparingContext
            or AIGenerationStatus.Connecting
            or AIGenerationStatus.Streaming;
    }

    private async Task RefreshPreviewFromCacheAsync()
    {
        if (_lastPreviewSource.Count == 0 || string.IsNullOrWhiteSpace(_lastPreviewContactId))
        {
            return;
        }

        await RefreshContextPreviewAsync(
            _lastPreviewSource,
            _lastPreviewContactId,
            _lastPreviewContactName,
            _lastPreviewIsGroup,
            _lastPreviewAccountId);
    }

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
                    AccountId = _boundAccountId ?? SqliteStore.LegacyAccountId,
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
            AccountId = last.AccountId,
            ContactId = last.ContactId,
            ContactName = last.ContactName,
            TriggerAccountId = last.TriggerAccountId,
            TriggerConversationId = last.TriggerConversationId,
            TriggerMessageId = last.TriggerMessageId,
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
