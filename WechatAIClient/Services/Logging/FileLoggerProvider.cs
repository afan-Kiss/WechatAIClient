using Microsoft.Extensions.Logging;

namespace WechatAIClient.Services.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _gate = new();
    private readonly long _maxFileBytes;
    private readonly int _retainDays;

    public FileLoggerProvider(long maxFileBytes = 5 * 1024 * 1024, int retainDays = 14)
    {
        _maxFileBytes = maxFileBytes;
        _retainDays = retainDays;
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WechatAIClient",
            "Logs");
        Directory.CreateDirectory(_directory);
        CleanupOldLogs();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose()
    {
        // no unmanaged resources
    }

    internal void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        var line =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{level}\t{category}\t{eventId.Id}\t{message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        line += Environment.NewLine;

        lock (_gate)
        {
            var path = GetTodayPath();
            RotateIfNeeded(path);
            File.AppendAllText(path, line);
        }
    }

    private string GetTodayPath()
        => Path.Combine(_directory, $"wechat-ai-{DateTime.Now:yyyy-MM-dd}.log");

    private void RotateIfNeeded(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var info = new FileInfo(path);
            if (info.Length < _maxFileBytes)
            {
                return;
            }

            var archive = Path.Combine(
                _directory,
                $"wechat-ai-{DateTime.Now:yyyy-MM-dd}-{DateTime.Now:HHmmss}.log");
            File.Move(path, archive, overwrite: true);
        }
        catch
        {
            // best-effort rotation
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-_retainDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "wechat-ai-*.log"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < cutoff)
                {
                    info.Delete();
                }
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(_category, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}
