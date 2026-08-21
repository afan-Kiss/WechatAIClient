using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace WechatAIClient.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await clipboard.TryGetTextAsync();
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            return desktop.MainWindow.Clipboard;
        }

        return null;
    }
}
