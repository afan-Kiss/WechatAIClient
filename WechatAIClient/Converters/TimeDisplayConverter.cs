using System.Globalization;
using Avalonia.Data.Converters;

namespace WechatAIClient.Converters;

public sealed class TimeDisplayConverter : IValueConverter
{
    public static readonly TimeDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt)
        {
            return string.Empty;
        }

        if (dt.Date == DateTime.Today)
        {
            return dt.ToString("HH:mm");
        }

        if (dt.Date == DateTime.Today.AddDays(-1))
        {
            return "昨天";
        }

        return dt.ToString("MM/dd");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
