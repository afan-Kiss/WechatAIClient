using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace WechatAIClient.Converters;

/// <summary>Converts a local file path string to an Avalonia <see cref="Bitmap"/> for Image.Source.</summary>
public sealed class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
