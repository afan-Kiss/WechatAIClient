using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IThemeService
{
    AppThemeMode CurrentMode { get; }
    bool ActualIsLight { get; }
    event EventHandler? ThemeChanged;
    void SetTheme(AppThemeMode mode);
    void NotifySystemThemeChanged(bool isLight);
    Task RestoreAsync(CancellationToken cancellationToken = default);
}
