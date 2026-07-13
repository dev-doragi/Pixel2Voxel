namespace PixelVoxel.Core;

/// <summary>
/// Stores framework-independent RGBA pixels for one orthographic source image.
/// </summary>
public sealed class OrthographicImage
{
    private readonly Rgba32Color[] _pixels;

    /// <summary>
    /// Initializes an orthographic image whose pixel origin is the top-left corner.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="pixels">Pixels in row-major order.</param>
    public OrthographicImage(int width, int height, IEnumerable<Rgba32Color> pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        ArgumentNullException.ThrowIfNull(pixels);
        _pixels = pixels.ToArray();

        if (_pixels.Length != checked(width * height))
        {
            throw new ArgumentException("The pixel count must match width times height.", nameof(pixels));
        }

        Width = width;
        Height = height;
    }

    /// <summary>Gets the image width.</summary>
    public int Width { get; }

    /// <summary>Gets the image height.</summary>
    public int Height { get; }

    /// <summary>
    /// Gets a pixel using top-left image coordinates.
    /// </summary>
    public Rgba32Color GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return _pixels[(y * Width) + x];
    }
}

