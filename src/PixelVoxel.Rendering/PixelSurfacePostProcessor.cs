using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

internal static class PixelSurfacePostProcessor
{
    public static PixelFramebuffer Process(PixelRasterSurface surface, VoxelRenderStyle style)
    {
        Rgba32Color[] output = new Rgba32Color[checked(surface.Width * surface.Height)];

        for (int index = 0; index < output.Length; index++)
        {
            output[index] = surface.Coverage[index]
                ? ApplyLighting(surface.Colors[index], surface.Normals[index], style.Lighting)
                : style.Background;
        }

        if (!style.Outline.Enabled)
        {
            return new PixelFramebuffer(surface.Width, surface.Height, output);
        }

        Rgba32Color[] outlined = (Rgba32Color[])output.Clone();
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                int index = (y * surface.Width) + x;
                bool drawOutline = surface.Coverage[index]
                    ? style.Outline.Mode == VoxelOutlineMode.SilhouetteAndDepth &&
                      HasInternalBoundary(surface, x, y, index, style.Outline.DepthThreshold)
                    : HasCoveredNeighbor(surface, x, y);

                if (drawOutline)
                {
                    outlined[index] = style.Outline.Color;
                }
            }
        }

        return new PixelFramebuffer(surface.Width, surface.Height, outlined);
    }

    internal static Rgba32Color ApplyLighting(
        Rgba32Color color,
        Vector3 normal,
        VoxelLightingSettings settings)
    {
        if (!settings.Enabled)
        {
            return color;
        }

        float diffuse = MathF.Max(0f, Vector3.Dot(normal, settings.Direction));
        float level = MathF.Round(diffuse * 3f, MidpointRounding.AwayFromZero) / 3f;
        float brightness = Math.Clamp(
            Math.Clamp(settings.Ambient, 0f, 1f) +
            (Math.Clamp(settings.Intensity, 0f, 1f) * level),
            0f,
            1f);
        return new Rgba32Color(
            Scale(color.Red, brightness),
            Scale(color.Green, brightness),
            Scale(color.Blue, brightness),
            color.Alpha);
    }

    private static byte Scale(byte value, float brightness) =>
        (byte)Math.Clamp(
            (int)MathF.Round(value * brightness, MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);

    private static bool HasCoveredNeighbor(PixelRasterSurface surface, int x, int y)
    {
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                int neighborX = x + offsetX;
                int neighborY = y + offsetY;
                if (IsInside(surface, neighborX, neighborY) &&
                    surface.Coverage[(neighborY * surface.Width) + neighborX])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasInternalBoundary(
        PixelRasterSurface surface,
        int x,
        int y,
        int index,
        float threshold)
    {
        ReadOnlySpan<(int X, int Y)> offsets =
        [(-1, 0), (1, 0), (0, -1), (0, 1)];

        foreach ((int offsetX, int offsetY) in offsets)
        {
            int neighborX = x + offsetX;
            int neighborY = y + offsetY;
            if (!IsInside(surface, neighborX, neighborY))
            {
                continue;
            }

            int neighborIndex = (neighborY * surface.Width) + neighborX;
            if (!surface.Coverage[neighborIndex])
            {
                continue;
            }

            if (Vector3.Dot(surface.Normals[index], surface.Normals[neighborIndex]) < 0.999f ||
                MathF.Abs(surface.Depth[index] - surface.Depth[neighborIndex]) > threshold)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(PixelRasterSurface surface, int x, int y) =>
        x >= 0 && y >= 0 && x < surface.Width && y < surface.Height;
}
