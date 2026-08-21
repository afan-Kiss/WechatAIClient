using Avalonia;
using Avalonia.Controls;

namespace WechatAIClient.Controls;

public class ChatBubble : ContentControl
{
    public static readonly StyledProperty<bool> IsSelfProperty =
        AvaloniaProperty.Register<ChatBubble, bool>(nameof(IsSelf));

    public bool IsSelf
    {
        get => GetValue(IsSelfProperty);
        set => SetValue(IsSelfProperty, value);
    }
}
