using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private AppThemeMode _currentMode = AppThemeMode.Dark;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    public AppThemeMode CurrentMode => _currentMode;

    public event EventHandler? ThemeChanged;

    public void SetTheme(AppThemeMode mode)
    {
        if (_currentMode == mode)
        {
            return;
        }

        _currentMode = mode;
        _logger.LogInformation("Theme switched to {Mode}", mode);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
