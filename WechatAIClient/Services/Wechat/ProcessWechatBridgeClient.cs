using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.Wechat;

/// <summary>
/// In-process WeChat process probe + version gate. No hook/adapter for 4.x in Round 4.
/// </summary>
public sealed class ProcessWechatBridgeClient : IWechatBridgeClient
{
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly ILogger<ProcessWechatBridgeClient> _logger;
    private readonly BridgeSupervisor _supervisor;
    private readonly object _gate = new();
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

    private WechatConnectionState _state = WechatConnectionState.Disconnected;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _backoffIndex;
    private bool _disposed;
    private WechatVersionInfo _lastVersion = new(string.Empty, string.Empty, false, null);
    private WechatAccountInfo? _account;

    public ProcessWechatBridgeClient(
        ILogger<ProcessWechatBridgeClient> logger,
        BridgeSupervisor? supervisor = null)
    {
        _logger = logger;
        _supervisor = supervisor ?? new BridgeSupervisor();
    }

    public BridgeSupervisor Supervisor => _supervisor;

    public WechatConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<WechatConnectionState>? StateChanged;
#pragma warning disable CS0067 // Event required by interface; messaging adapter not wired for Process probe yet.
    public event EventHandler<BridgeMessageEvent>? MessageReceived;
    public event EventHandler<OutgoingAcknowledgedEvent>? OutgoingAcknowledged;
#pragma warning restore CS0067
    public event EventHandler? BridgeCrashed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_loopCts is not null)
            {
                return Task.CompletedTask;
            }

            _loopCts = new CancellationTokenSource();
            var token = _loopCts.Token;
            _loopTask = Task.Run(() => PollLoopAsync(token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _loopCts;
            loop = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }

        if (cts is not null)
        {
            try
            {
                await cts.CancelAsync();
            }
            catch
            {
                // ignore
            }
        }

        if (loop is not null)
        {
            try
            {
                await loop.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch
            {
                // ignore
            }
        }

        cts?.Dispose();
        SetState(WechatConnectionState.Disconnected);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _supervisor.Reset();
        lock (_gate)
        {
            _backoffIndex = 0;
        }

        SetState(WechatConnectionState.Connecting);
        await EvaluateOnceAsync(cancellationToken);
        if (_loopCts is null)
        {
            await StartAsync(cancellationToken);
        }
    }

    public Task<WechatAccountInfo?> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_state == WechatConnectionState.Connected ? _account : null);
        }
    }

    public Task<IReadOnlyList<BridgeContact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Unsupported / not connected → empty, never throw
        return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
    }

    public Task<IReadOnlyList<BridgeContact>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
    }

    public Task<IReadOnlyList<BridgeContact>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<BridgeContact>>(Array.Empty<BridgeContact>());
    }

    public Task<IReadOnlyList<BridgeMessage>> GetMessagesAsync(
        string conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<BridgeMessage>>(Array.Empty<BridgeMessage>());
    }

    public Task<SendMessageResult> SendTextAsync(
        string conversationId,
        string text,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isFromAi;
        if (State != WechatConnectionState.Connected)
        {
            return Task.FromResult(new SendMessageResult(
                false, null, clientRequestId, DateTime.Now, "NotConnected", "微信未连接"));
        }

        // Round 4: no messaging adapter yet even when Connected (classic stub).
        return Task.FromResult(new SendMessageResult(
            false, null, clientRequestId, DateTime.Now, "NotSupported",
            "当前版本尚未接入真实消息发送适配器"));
    }

    public Task<SendMessageResult> SendImageAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isFromAi;
        return Task.FromResult(new SendMessageResult(
            false, null, clientRequestId, DateTime.Now, "NotSupported", "图片发送暂未实现"));
    }

    public Task<SendMessageResult> SendFileAsync(
        string conversationId,
        string localPath,
        string clientRequestId,
        bool isFromAi = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isFromAi;
        return Task.FromResult(new SendMessageResult(
            false, null, clientRequestId, DateTime.Now, "NotSupported", "文件发送暂未实现"));
    }

    public Task<WechatVersionInfo> DetectVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!WechatProcessProbe.TryFindRunning(out var info))
        {
            return Task.FromResult(new WechatVersionInfo(
                string.Empty, string.Empty, false, "微信未运行"));
        }

        var supported = WechatProcessProbe.IsSupportedVersion(info.ProductVersion, out var hint);
        var version = new WechatVersionInfo(info.ProductVersion, info.FilePath, supported, hint);
        lock (_gate)
        {
            _lastVersion = version;
        }

        return Task.FromResult(version);
    }

    /// <summary>Test/internal: raise crash and apply supervisor policy.</summary>
    public void NotifyInternalFault()
    {
        _supervisor.RecordCrash();
        BridgeCrashed?.Invoke(this, EventArgs.Empty);
        SetState(WechatConnectionState.BridgeError);
        if (!_supervisor.AutoRestartEnabled)
        {
            _logger.LogWarning("Bridge crash limit reached; auto-restart disabled");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WeChat process poll failed");
                NotifyInternalFault();
                if (!_supervisor.AutoRestartEnabled)
                {
                    break;
                }
            }

            var delay = _pollInterval;
            lock (_gate)
            {
                if (_state is WechatConnectionState.WechatNotRunning or WechatConnectionState.BridgeError)
                {
                    var idx = Math.Min(_backoffIndex, BackoffSchedule.Length - 1);
                    delay = BackoffSchedule[idx];
                    if (_backoffIndex < BackoffSchedule.Length - 1)
                    {
                        _backoffIndex++;
                    }
                }
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task EvaluateOnceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var version = await DetectVersionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(version.FilePath))
        {
            SetState(WechatConnectionState.WechatNotRunning);
            lock (_gate)
            {
                _account = null;
            }

            return;
        }

        if (!version.IsSupported)
        {
            SetState(WechatConnectionState.VersionUnsupported);
            lock (_gate)
            {
                _account = null;
            }

            _logger.LogInformation("WeChat version unsupported: {Version}", version.ProductVersion);
            return;
        }

        // Classic 3.9.12.* — stub Connected until a real adapter is wired.
        lock (_gate)
        {
            _account ??= new WechatAccountInfo("local", "本机微信", null);
            _backoffIndex = 0;
        }

        SetState(WechatConnectionState.Connected);
    }

    private void SetState(WechatConnectionState state)
    {
        WechatConnectionState previous;
        lock (_gate)
        {
            previous = _state;
            if (previous == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }
}
