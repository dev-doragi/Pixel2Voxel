using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging;

/// <summary>Reads framework-independent RGBA pixels from PNG streams.</summary>
public sealed class PngPixelReader
{
    /// <summary>Reads a PNG stream into a top-left, row-major image.</summary>
    public async Task<OrthographicImage> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The PNG stream must be readable.", nameof(stream));
        using Image<ImageSharpColor> image = await Image.LoadAsync<ImageSharpColor>(stream, cancellationToken);
        Rgba32Color[] pixels = new Rgba32Color[checked(image.Width * image.Height)];
        for (int y = 0; y < image.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < image.Width; x++)
            {
                ImageSharpColor pixel = image[x, y];
                pixels[(y * image.Width) + x] = new Rgba32Color(pixel.R, pixel.G, pixel.B, pixel.A);
            }
        }

        return new OrthographicImage(image.Width, image.Height, pixels);
    }
}
