using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WechatAIClient.ViewModels;

namespace WechatAIClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Chat.MessagesUpdated += (_, _) =>
                Dispatcher.UIThread.Post(ScrollMessagesToEnd);
        }

        ScrollMessagesToEnd();
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        BeginMoveDrag(e);
    }

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_OnClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void MessageInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;
        if (DataContext is MainWindowViewModel vm && vm.Chat.SendCommand.CanExecute(null))
        {
            vm.Chat.SendCommand.Execute(null);
            Dispatcher.UIThread.Post(ScrollMessagesToEnd);
        }
    }

    private void ScrollMessagesToEnd()
    {
        if (this.FindControl<ScrollViewer>("MessageScrollViewer") is { } viewer)
        {
            viewer.ScrollToEnd();
        }
    }
}
