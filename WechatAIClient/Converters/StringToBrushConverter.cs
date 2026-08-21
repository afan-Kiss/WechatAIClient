using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WechatAIClient.Converters;

public sealed class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return Brush.Parse(hex);
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
