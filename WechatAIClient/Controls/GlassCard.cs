using Avalonia;
using Avalonia.Controls;

namespace WechatAIClient.Controls;

public class GlassCard : ContentControl
{
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<GlassCard, Thickness>(nameof(ContentPadding), new Thickness(16));

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }
}
