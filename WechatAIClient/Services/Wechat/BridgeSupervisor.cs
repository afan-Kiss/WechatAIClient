namespace WechatAIClient.Services.Wechat;

/// <summary>
/// Tracks bridge crash bursts; stops auto-restart after N crashes in a window
/// (ready for a future child-process bridge).
/// </summary>
public sealed class BridgeSupervisor
{
    private readonly int _maxCrashes;
    private readonly TimeSpan _window;
    private readonly Queue<DateTime> _crashes = new();
    private readonly object _gate = new();

    public BridgeSupervisor(int maxCrashes = 3, TimeSpan? window = null)
    {
        _maxCrashes = Math.Max(1, maxCrashes);
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public bool AutoRestartEnabled { get; private set; } = true;

    public int CrashCountInWindow
    {
        get
        {
            lock (_gate)
            {
                PruneLocked(DateTime.UtcNow);
                return _crashes.Count;
            }
        }
    }

    public void RecordCrash()
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            PruneLocked(now);
            _crashes.Enqueue(now);
            if (_crashes.Count >= _maxCrashes)
            {
                AutoRestartEnabled = false;
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _crashes.Clear();
            AutoRestartEnabled = true;
        }
    }

    private void PruneLocked(DateTime utcNow)
    {
        while (_crashes.Count > 0 && utcNow - _crashes.Peek() > _window)
        {
            _crashes.Dequeue();
        }
    }
}
