using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WechatAIClient.Models;
using WechatAIClient.ViewModels;

namespace WechatAIClient.Views;

public partial class MainWindow : Window
{
    private ScrollViewer? _messageScrollViewer;
    private Border? _rootChrome;
    private Grid? _bodyMargin;
    private bool _scrollHooked;
    private const double DefaultCornerRadius = 12;
    private const double DefaultBodyMargin = 12;
    private const double DefaultWidth = 1440;
    private const double DefaultHeight = 900;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
        SizeChanged += (_, _) => UpdateBubbleMaxWidth();
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _rootChrome = this.FindControl<Border>("RootChrome");
        _bodyMargin = this.FindControl<Grid>("BodyMargin");
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            ApplyChromeForWindowState();
        }
    }

    private void ApplyChromeForWindowState()
    {
        _rootChrome ??= this.FindControl<Border>("RootChrome");
        _bodyMargin ??= this.FindControl<Grid>("BodyMargin");

        if (WindowState == WindowState.Maximized)
        {
            if (_rootChrome is not null)
            {
                _rootChrome.CornerRadius = default;
            }

            if (_bodyMargin is not null)
            {
                _bodyMargin.Margin = default;
            }
        }
        else
        {
            if (_rootChrome is not null)
            {
                _rootChrome.CornerRadius = new CornerRadius(DefaultCornerRadius);
            }

            if (_bodyMargin is not null)
            {
                _bodyMargin.Margin = new Thickness(DefaultBodyMargin);
            }
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        FitToWorkAreaIfNeeded();
        ApplyChromeForWindowState();

        _messageScrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
        if (_messageScrollViewer is not null && !_scrollHooked)
        {
            _messageScrollViewer.ScrollChanged += MessageScrollViewer_OnScrollChanged;
            _scrollHooked = true;
            UpdateBubbleMaxWidth();
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Chat.MessagesChanged += ChatOnMessagesChanged;
            vm.Chat.MessagesUpdated += (_, _) =>
                Dispatcher.UIThread.Post(() => ScrollMessagesToEnd(force: true));
        }

        ScrollMessagesToEnd(force: true);
    }

    private void FitToWorkAreaIfNeeded()
    {
        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var work = screen.WorkingArea;
        var scaling = screen.Scaling;
        var workWidthDip = work.Width / scaling;
        var workHeightDip = work.Height / scaling;

        var targetWidth = Math.Min(DefaultWidth, Math.Max(MinWidth, workWidthDip - 48));
        var targetHeight = Math.Min(DefaultHeight, Math.Max(MinHeight, workHeightDip - 48));

        if (Width > targetWidth || Height > targetHeight)
        {
            Width = targetWidth;
            Height = targetHeight;
        }

        if (Position.X + Width > work.X / scaling + workWidthDip ||
            Position.Y + Height > work.Y / scaling + workHeightDip)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            var x = work.X / scaling + Math.Max(0, (workWidthDip - Width) / 2);
            var y = work.Y / scaling + Math.Max(0, (workHeightDip - Height) / 2);
            Position = new PixelPoint((int)(x * scaling), (int)(y * scaling));
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_messageScrollViewer is not null && _scrollHooked)
        {
            _messageScrollViewer.ScrollChanged -= MessageScrollViewer_OnScrollChanged;
            _scrollHooked = false;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Chat.MessagesChanged -= ChatOnMessagesChanged;
            vm.Cleanup();
        }
    }

    private void ChatOnMessagesChanged(object? sender, ChatMessagesChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (e.ForceScroll || vm.Chat.IsNearBottom || vm.Chat.KeepAtBottom)
            {
                ScrollMessagesToEnd(force: e.ForceScroll);
            }
        });
    }

    private void MessageScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_messageScrollViewer is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var extent = _messageScrollViewer.Extent.Height;
        var viewport = _messageScrollViewer.Viewport.Height;
        var offset = _messageScrollViewer.Offset.Y;
        var distance = extent - viewport - offset;
        vm.Chat.IsNearBottom = distance <= 48;
        vm.Chat.KeepAtBottom = vm.Chat.IsNearBottom;
    }

    private void UpdateBubbleMaxWidth()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        _messageScrollViewer ??= this.FindControl<ScrollViewer>("MessageScrollViewer");
        var width = _messageScrollViewer?.Bounds.Width ?? Bounds.Width;
        if (width > 0)
        {
            vm.Chat.BubbleMaxWidth = Math.Max(180, width * 0.72);
        }
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
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (vm.AiPanel.IsContextPreviewOpen)
        {
            vm.AiPanel.ToggleContextPreviewCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (vm.IsSettingsOpen)
        {
            vm.CloseSettingsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ContextPreviewMask_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.AiPanel.IsContextPreviewOpen)
        {
            vm.AiPanel.ToggleContextPreviewCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void SettingsMask_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseSettingsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void AiInstruction_OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Prevent Enter in AI instruction box from bubbling to chat send.
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
        }
    }

    private void MessageInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        // Shift+Enter inserts newline; plain Enter sends.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        // Avalonia 12: InputMethod.IsInputMethodEnabled enables IME on the control;
        // it does NOT report active composition. TextBox normally suppresses Enter
        // while composing; if Enter still fires during composition on some platforms,
        // there is no public "is composing" API — avoid sending only when IME is disabled.
        if (sender is TextBox textBox)
        {
            // Ensure IME remains enabled for CJK input.
            if (!InputMethod.GetIsInputMethodEnabled(textBox))
            {
                InputMethod.SetIsInputMethodEnabled(textBox, true);
            }
        }

        e.Handled = true;
        if (DataContext is MainWindowViewModel vm && vm.Chat.SendCommand.CanExecute(null))
        {
            vm.Chat.SendCommand.Execute(null);
            Dispatcher.UIThread.Post(() => ScrollMessagesToEnd(force: true));
        }
    }

    private void ScrollMessagesToEnd(bool force = false)
    {
        _messageScrollViewer ??= this.FindControl<ScrollViewer>("MessageScrollViewer");
        if (_messageScrollViewer is null)
        {
            return;
        }

        if (!force && DataContext is MainWindowViewModel vm && !vm.Chat.IsNearBottom && !vm.Chat.KeepAtBottom)
        {
            return;
        }

        _messageScrollViewer.ScrollToEnd();
    }
}
