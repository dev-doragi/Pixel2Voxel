using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>
/// Defines the stable internal resolution and pixel scale used by both render backends.
/// </summary>
public sealed record PixelRenderSettings
{
    /// <summary>The default opaque viewport background.</summary>
    public static readonly Rgba32Color DefaultBackground = new(20, 24, 32, 255);

    /// <summary>Initializes pixel rendering settings.</summary>
    public PixelRenderSettings(
        int width = 320,
        int height = 180,
        int pixelsPerVoxel = 4,
        Rgba32Color? background = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (pixelsPerVoxel is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerVoxel));
        }

        Width = width;
        Height = height;
        PixelsPerVoxel = pixelsPerVoxel;
        Background = background ?? DefaultBackground;
    }

    /// <summary>Gets the internal framebuffer width.</summary>
    public int Width { get; }

    /// <summary>Gets the internal framebuffer height.</summary>
    public int Height { get; }

    /// <summary>Gets the integer number of internal pixels per voxel unit.</summary>
    public int PixelsPerVoxel { get; }

    /// <summary>Gets the opaque clear color.</summary>
    public Rgba32Color Background { get; }
}
