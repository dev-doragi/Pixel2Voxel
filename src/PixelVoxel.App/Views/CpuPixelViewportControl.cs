using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PixelVoxel.App.Views;

/// <summary>Draws a CPU framebuffer at the largest centered integer scale.</summary>
public sealed class CpuPixelViewportControl : Control
{
    /// <summary>Defines the source bitmap property.</summary>
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<CpuPixelViewportControl, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<int?> ManualScaleProperty =
        AvaloniaProperty.Register<CpuPixelViewportControl, int?>(nameof(ManualScale));

    static CpuPixelViewportControl()
    {
        AffectsRender<CpuPixelViewportControl>(SourceProperty, ManualScaleProperty);
    }

    /// <summary>Initializes nearest-neighbor bitmap presentation.</summary>
    public CpuPixelViewportControl()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    /// <summary>Gets or sets the low-resolution source bitmap.</summary>
    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Gets or sets the explicit integer scale, or null to fit.</summary>
    public int? ManualScale
    {
        get => GetValue(ManualScaleProperty);
        set => SetValue(ManualScaleProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Source is not Bitmap source)
        {
            return;
        }

        int width = source.PixelSize.Width;
        int height = source.PixelSize.Height;
        int fitScale = Math.Max(
            1,
            (int)Math.Floor(Math.Min(Bounds.Width / width, Bounds.Height / height)));
        int scale = ManualScale.HasValue
            ? Math.Clamp(ManualScale.Value, 1, 16)
            : fitScale;
        double destinationWidth = width * scale;
        double destinationHeight = height * scale;
        Rect destination = new(
            (Bounds.Width - destinationWidth) / 2d,
            (Bounds.Height - destinationHeight) / 2d,
            destinationWidth,
            destinationHeight);

        context.DrawImage(source, new Rect(0, 0, width, height), destination);
    }
}
