using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>
/// Stores one low-resolution, unscaled RGBA pixel frame.
/// </summary>
public sealed class PixelFramebuffer
{
    private readonly Rgba32Color[] _pixels;

    /// <summary>Initializes a complete pixel framebuffer.</summary>
    public PixelFramebuffer(int width, int height, IEnumerable<Rgba32Color> pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        _pixels = pixels?.ToArray() ?? throw new ArgumentNullException(nameof(pixels));
        if (_pixels.Length != checked(width * height))
        {
            throw new ArgumentException("The pixel count must match the framebuffer size.", nameof(pixels));
        }

        Width = width;
        Height = height;
    }

    /// <summary>Gets the framebuffer width.</summary>
    public int Width { get; }

    /// <summary>Gets the framebuffer height.</summary>
    public int Height { get; }

    /// <summary>Gets the pixels in top-left, row-major order.</summary>
    public ReadOnlyMemory<Rgba32Color> Pixels => _pixels;
}
