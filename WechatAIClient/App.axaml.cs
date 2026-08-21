using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;
using WechatAIClient.Services.Logging;
using WechatAIClient.Services.DeepSeek;
using WechatAIClient.Services.Mock;
using WechatAIClient.Services.Wechat;
using WechatAIClient.Services.Weixin;
using WechatAIClient.ViewModels;
using WechatAIClient.Views;

namespace WechatAIClient;

public partial class App : Application
{
    private static readonly Uri DarkThemeUri = new("avares://WechatAIClient/Themes/Theme.Dark.axaml");
    private static readonly Uri LightThemeUri = new("avares://WechatAIClient/Themes/Theme.Light.axaml");

    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();
        var logger = Services.GetRequiredService<ILogger<App>>();
        Services.GetRequiredService<SqliteStore>().Initialize();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            logger.LogCritical(ex, "Unhandled exception");
            WriteCrashLog(ex);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            if (IsRecoverable(e.Exception))
            {
                logger.LogWarning(e.Exception, "Recoverable UI exception");
                e.Handled = true;
                return;
            }

            logger.LogCritical(e.Exception, "Fatal UI thread exception");
            WriteCrashLog(e.Exception);
            e.Handled = false;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(1);
            }
        };

        var themeService = Services.GetRequiredService<IThemeService>();
        ApplyTheme(themeService.CurrentMode, themeService.ActualIsLight);
        themeService.ThemeChanged += (_, _) =>
            Dispatcher.UIThread.Post(() => ApplyTheme(themeService.CurrentMode, themeService.ActualIsLight));

        ActualThemeVariantChanged += (_, _) =>
        {
            var isLight = ActualThemeVariant == ThemeVariant.Light;
            themeService.NotifySystemThemeChanged(isLight);
            if (themeService.CurrentMode == AppThemeMode.System)
            {
                Dispatcher.UIThread.Post(() => ApplyTheme(AppThemeMode.System, isLight));
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktopLifetime.MainWindow = new MainWindow { DataContext = mainVm };
            desktopLifetime.Exit += (_, _) =>
            {
                mainVm.Cleanup();
                if (Services.GetService<IWechatService>() is IAsyncDisposable wechat)
                {
                    wechat.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            };
            _ = mainVm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider());
        });

        services.AddSingleton<SqliteStore>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddHttpClient(LocalWeixinApiClient.HttpClientName);
        services.AddSingleton<ILocalWeixinApiClient, LocalWeixinApiClient>();
        services.AddSingleton<IWechatCallbackParser, WechatCallbackParser>();
        services.AddSingleton<FakeWechatBridgeClient>();
        services.AddSingleton<BridgeSupervisor>();
        services.AddSingleton<IWechatBridgeClient, LocalApiWechatBridgeClient>();
        services.AddSingleton<MockWechatService>();
        services.AddSingleton<RealWechatService>();
        services.AddSingleton<IWechatService, RoutingWechatService>();
        services.AddHttpClient(DeepSeekAIService.HttpClientName);
        services.AddSingleton<MockAIService>();
        services.AddSingleton<DeepSeekAIService>();
        services.AddSingleton<IAIService, RoutingAIService>();
        services.AddSingleton<IAIContextBuilder, AIContextBuilder>();
        services.AddSingleton<IAISettingsService, AISettingsService>();
        services.AddSingleton<AIOrchestrator>();
        services.AddSingleton<ContactListViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<AIPanelViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }

    private void ApplyTheme(AppThemeMode mode, bool actualIsLight)
    {
        RequestedThemeVariant = mode switch
        {
            AppThemeMode.Light => ThemeVariant.Light,
            AppThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        var useLight = mode switch
        {
            AppThemeMode.Light => true,
            AppThemeMode.Dark => false,
            _ => actualIsLight
        };

        var themeUri = useLight ? LightThemeUri : DarkThemeUri;
        var dictionaries = Resources.MergedDictionaries;
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            if (dictionaries[i] is ResourceInclude)
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Insert(0, new ResourceInclude(themeUri) { Source = themeUri });
    }

    private static bool IsRecoverable(Exception ex)
    {
        if (ex is OperationCanceledException or TaskCanceledException or ArgumentException)
        {
            return true;
        }

        return ex is InvalidOperationException ioe
               && ioe.Message.Contains("SQLite unavailable", StringComparison.Ordinal);
    }

    private static void WriteCrashLog(Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WechatAIClient",
                "Logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, ex?.ToString() ?? "Unknown fatal error");
        }
        catch
        {
            // last resort
        }
    }
}
