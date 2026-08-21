using System.Globalization;
using Avalonia.Data.Converters;
using WechatAIClient.Models;

namespace WechatAIClient.Converters;

public sealed class MessageTypeEqualsConverter : IValueConverter
{
    public static readonly MessageTypeEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageType type && parameter is string name &&
            Enum.TryParse<MessageType>(name, true, out var expected))
        {
            return type == expected;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
