using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace WechatAIClient.Controls;

public class Avatar : TemplatedControl
{
    public static readonly StyledProperty<string> InitialsProperty =
        AvaloniaProperty.Register<Avatar, string>(nameof(Initials), "?");

    public static readonly StyledProperty<IBrush?> AvatarBackgroundProperty =
        AvaloniaProperty.Register<Avatar, IBrush?>(nameof(AvatarBackground));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Avatar, double>(nameof(Size), 40);

    public static readonly StyledProperty<string?> ImagePathProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(ImagePath));

    public static readonly StyledProperty<IImage?> ImageSourceProperty =
        AvaloniaProperty.Register<Avatar, IImage?>(nameof(ImageSource));

    private Bitmap? _ownedBitmap;

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

    public string? ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public IImage? ImageSource
    {
        get => GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImagePathProperty)
        {
            TryLoadBitmap(change.GetNewValue<string?>());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DisposeOwnedBitmap();
        base.OnDetachedFromVisualTree(e);
    }

    private void TryLoadBitmap(string? path)
    {
        DisposeOwnedBitmap();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ImageSource = null;
            return;
        }

        try
        {
            _ownedBitmap = new Bitmap(path);
            ImageSource = _ownedBitmap;
        }
        catch
        {
            _ownedBitmap = null;
            ImageSource = null;
        }
    }

    private void DisposeOwnedBitmap()
    {
        if (_ownedBitmap is null)
        {
            return;
        }

        if (ReferenceEquals(ImageSource, _ownedBitmap))
        {
            ImageSource = null;
        }

        _ownedBitmap.Dispose();
        _ownedBitmap = null;
    }
}
