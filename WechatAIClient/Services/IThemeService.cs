using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IThemeService
{
    AppThemeMode CurrentMode { get; }
    event EventHandler? ThemeChanged;
    void SetTheme(AppThemeMode mode);
}
