using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class ThemeService : IThemeService
{
    private const string ThemeKey = "theme.mode";
    private readonly ISettingsStore _settings;
    private readonly ILogger<ThemeService> _logger;
    private AppThemeMode _currentMode = AppThemeMode.Dark;
    private bool _systemIsLight;

    public ThemeService(ISettingsStore settings, ILogger<ThemeService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public AppThemeMode CurrentMode => _currentMode;

    public bool ActualIsLight => _currentMode switch
    {
        AppThemeMode.Light => true,
        AppThemeMode.Dark => false,
        _ => _systemIsLight
    };

    public event EventHandler? ThemeChanged;

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await _settings.GetAsync(ThemeKey, cancellationToken);
            if (Enum.TryParse<AppThemeMode>(raw, ignoreCase: true, out var mode))
            {
                _currentMode = mode;
                _logger.LogInformation("Restored theme {Mode}", mode);
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore theme");
        }
    }

    public void SetTheme(AppThemeMode mode)
    {
        if (_currentMode == mode)
        {
            return;
        }

        _currentMode = mode;
        _logger.LogInformation("Theme switched to {Mode}", mode);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        _ = PersistAsync(mode);
    }

    public void NotifySystemThemeChanged(bool isLight)
    {
        if (_systemIsLight == isLight)
        {
            return;
        }

        _systemIsLight = isLight;
        if (_currentMode == AppThemeMode.System)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task PersistAsync(AppThemeMode mode)
    {
        try
        {
            await _settings.SetAsync(ThemeKey, mode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist theme");
        }
    }
}
