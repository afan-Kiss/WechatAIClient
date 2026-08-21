using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace WechatAIClient.Controls;

/// <summary>Owned local-file image with Bitmap dispose on path change / detach.</summary>
public class MediaImage : Control
{
    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<MediaImage, string?>(nameof(Path));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<MediaImage, Stretch>(nameof(Stretch), Stretch.Uniform);

    private Bitmap? _owned;
    private string? _loadedPath;

    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    static MediaImage()
    {
        AffectsRender<MediaImage>(PathProperty, StretchProperty);
        AffectsMeasure<MediaImage>(PathProperty, StretchProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PathProperty)
        {
            Reload(change.GetNewValue<string?>());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeOwned();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_owned is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var dest = new Rect(Bounds.Size);
        context.DrawImage(_owned, new Rect(_owned.Size), dest);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_owned is null)
        {
            return default;
        }

        var src = _owned.Size;
        if (src.Width <= 0 || src.Height <= 0)
        {
            return default;
        }

        var maxW = double.IsInfinity(availableSize.Width) ? src.Width : availableSize.Width;
        var maxH = double.IsInfinity(availableSize.Height) ? src.Height : availableSize.Height;
        var scale = Math.Min(maxW / src.Width, maxH / src.Height);
        scale = Math.Min(scale, 1);
        return new Size(src.Width * scale, src.Height * scale);
    }

    private void Reload(string? path)
    {
        if (string.Equals(_loadedPath, path, StringComparison.Ordinal))
        {
            return;
        }

        DisposeOwned();
        _loadedPath = path;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            InvalidateVisual();
            return;
        }

        try
        {
            _owned = new Bitmap(path);
        }
        catch
        {
            _owned = null;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void DisposeOwned()
    {
        _owned?.Dispose();
        _owned = null;
        _loadedPath = null;
    }
}
