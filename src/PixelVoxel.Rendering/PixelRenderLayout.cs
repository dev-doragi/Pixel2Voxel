using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>Defines a camera-independent logical framebuffer that is safe for every rotation.</summary>
public sealed record PixelRenderLayout(
    int Width,
    int Height,
    Vector3 ModelCenter,
    float ModelDiagonal)
{
    /// <summary>Gets the fixed horizontal framebuffer anchor.</summary>
    public float ScreenOffsetX => Width / 2f;

    /// <summary>Gets the fixed vertical framebuffer anchor.</summary>
    public float ScreenOffsetY => Height / 2f;
}

/// <summary>Creates stable pixel render layouts from model and source-image dimensions.</summary>
public sealed class PixelRenderLayoutResolver
{
    /// <summary>Resolves a fixed layout with one logical pixel of safety on every side.</summary>
    public PixelRenderLayout Resolve(
        VoxelDimensions dimensions,
        int sourcePixelWidth,
        int sourcePixelHeight)
    {
        if (sourcePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePixelWidth));
        }

        if (sourcePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePixelHeight));
        }

        double diagonal = Math.Sqrt(
            ((double)dimensions.Width * dimensions.Width) +
            ((double)dimensions.Height * dimensions.Height) +
            ((double)dimensions.Depth * dimensions.Depth));
        int safeExtent = checked((int)Math.Ceiling(diagonal) + 2);

        return new PixelRenderLayout(
            Math.Max(sourcePixelWidth, safeExtent),
            Math.Max(sourcePixelHeight, safeExtent),
            new Vector3(
                dimensions.Width / 2f,
                dimensions.Height / 2f,
                dimensions.Depth / 2f),
            Math.Max(1f, (float)diagonal));
    }
}
