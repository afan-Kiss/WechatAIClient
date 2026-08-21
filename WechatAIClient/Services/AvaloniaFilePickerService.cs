using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace WechatAIClient.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickImageAsync(CancellationToken cancellationToken = default)
    {
        var provider = GetStorageProvider();
        if (provider is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择图片",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"]
                }
            ]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileAsync(CancellationToken cancellationToken = default)
    {
        var provider = GetStorageProvider();
        if (provider is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择文件",
            AllowMultiple = false
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            return desktop.MainWindow.StorageProvider;
        }

        return null;
    }
}
