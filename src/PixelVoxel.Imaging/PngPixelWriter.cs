using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging;

/// <summary>Writes framework-independent RGBA pixels as a PNG using ImageSharp.</summary>
public sealed class PngPixelWriter
{
    /// <summary>Writes one top-left, row-major RGBA image.</summary>
    public async Task WriteAsync(
        string path,
        int width,
        int height,
        ReadOnlyMemory<Rgba32Color> pixels,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels.Length != checked(width * height))
        {
            throw new ArgumentException("The pixel count must match width times height.", nameof(pixels));
        }

        await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await WriteAsync(stream, width, height, pixels, cancellationToken);
    }

    /// <summary>Writes one top-left, row-major RGBA image to an open stream.</summary>
    public async Task WriteAsync(
        Stream stream,
        int width,
        int height,
        ReadOnlyMemory<Rgba32Color> pixels,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new ArgumentException("The PNG stream must be writable.", nameof(stream));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels.Length != checked(width * height))
        {
            throw new ArgumentException("The pixel count must match width times height.", nameof(pixels));
        }

        using Image<ImageSharpColor> image = new(width, height);
        ReadOnlyMemory<Rgba32Color> source = pixels;
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                Rgba32Color pixel = source.Span[(y * width) + x];
                image[x, y] = new ImageSharpColor(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
            }
        }

        await image.SaveAsPngAsync(stream, cancellationToken);
    }
}
