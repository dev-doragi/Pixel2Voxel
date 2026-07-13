using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>Identifies the screen-space pixel outline algorithm.</summary>
public enum VoxelOutlineMode
{
    /// <summary>Draws only the outside model silhouette.</summary>
    Silhouette,

    /// <summary>Adds normal and depth discontinuities inside the silhouette.</summary>
    SilhouetteAndDepth,
}

/// <summary>Defines one world-fixed, quantized directional light.</summary>
public sealed record VoxelLightingSettings(
    bool Enabled,
    float AzimuthDegrees,
    float ElevationDegrees,
    float Ambient,
    float Intensity)
{
    /// <summary>Gets the default four-level pixel light.</summary>
    public static VoxelLightingSettings Default { get; } =
        new(true, -45f, 45f, 0.35f, 0.65f);

    /// <summary>Gets the normalized model-space vector from a surface toward the light.</summary>
    public Vector3 Direction
    {
        get
        {
            float azimuth = DegreesToRadians(AzimuthDegrees);
            float elevation = DegreesToRadians(ElevationDegrees);
            float horizontal = MathF.Cos(elevation);
            return Vector3.Normalize(new Vector3(
                horizontal * MathF.Sin(azimuth),
                MathF.Sin(elevation),
                horizontal * MathF.Cos(azimuth)));
        }
    }

    private static float DegreesToRadians(float value) => value * (MathF.PI / 180f);
}

/// <summary>Defines a one-logical-pixel screen-space outline.</summary>
public sealed record VoxelOutlineSettings(
    bool Enabled,
    VoxelOutlineMode Mode,
    Rgba32Color Color,
    float DepthThreshold)
{
    /// <summary>Gets the default opaque black silhouette.</summary>
    public static VoxelOutlineSettings Default { get; } =
        new(true, VoxelOutlineMode.Silhouette, new Rgba32Color(0, 0, 0, 255), 0.5f);
}

/// <summary>Groups background, lighting, and outline settings for both render backends.</summary>
public sealed record VoxelRenderStyle(
    Rgba32Color Background,
    VoxelLightingSettings Lighting,
    VoxelOutlineSettings Outline)
{
    /// <summary>Gets the default pixel viewport style.</summary>
    public static VoxelRenderStyle Default { get; } =
        new(
            PixelRenderSettings.DefaultBackground,
            VoxelLightingSettings.Default,
            VoxelOutlineSettings.Default);
}
