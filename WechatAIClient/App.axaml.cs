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
using WechatAIClient.Services.Mock;
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
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            logger.LogError(e.Exception, "UI thread exception");
            e.Handled = true;
        };

        var themeService = Services.GetRequiredService<IThemeService>();
        ApplyTheme(themeService.CurrentMode);
        themeService.ThemeChanged += (_, _) =>
            Dispatcher.UIThread.Post(() => ApplyTheme(themeService.CurrentMode));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
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
        });

        services.AddSingleton<SqliteStore>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWechatService, MockWechatService>();
        services.AddSingleton<IAIService, MockAIService>();
        services.AddSingleton<ContactListViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<AIPanelViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }

    private void ApplyTheme(AppThemeMode mode)
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
            _ => ActualThemeVariant == ThemeVariant.Light
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
}
