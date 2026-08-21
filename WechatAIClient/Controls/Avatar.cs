using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WechatAIClient.Controls;

public class Avatar : TemplatedControl
{
    public static readonly StyledProperty<string> InitialsProperty =
        AvaloniaProperty.Register<Avatar, string>(nameof(Initials), "?");

    public static readonly StyledProperty<IBrush?> AvatarBackgroundProperty =
        AvaloniaProperty.Register<Avatar, IBrush?>(nameof(AvatarBackground));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Avatar, double>(nameof(Size), 40);

    public string Initials
    {
        get => GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    public IBrush? AvatarBackground
    {
        get => GetValue(AvatarBackgroundProperty);
        set => SetValue(AvatarBackgroundProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
