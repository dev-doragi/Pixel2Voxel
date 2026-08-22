using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>
/// Fills projected voxel faces on a one-pixel logical grid before integer presentation scaling.
/// </summary>
public sealed class PixelArtVoxelRasterizer
{
    private readonly CpuVoxelRasterizer _triangleRasterizer;
    private readonly PixelRenderLayoutResolver _layoutResolver = new();

    /// <summary>Initializes the pixel-art renderer with the deterministic triangle rasterizer.</summary>
    public PixelArtVoxelRasterizer()
        : this(new CpuVoxelRasterizer())
    {
    }

    internal PixelArtVoxelRasterizer(CpuVoxelRasterizer triangleRasterizer)
    {
        _triangleRasterizer = triangleRasterizer ??
            throw new ArgumentNullException(nameof(triangleRasterizer));
    }

    /// <summary>
    /// Renders complete visible faces at one logical pixel per voxel unit on a stable rotation-safe canvas.
    /// </summary>
    public PixelFramebuffer Render(
        VoxelMeshData mesh,
        VoxelCameraState camera,
        int sourcePixelWidth,
        int sourcePixelHeight,
        Rgba32Color? background = null) =>
        Render(
            mesh,
            camera,
            _layoutResolver.Resolve(mesh.Dimensions, sourcePixelWidth, sourcePixelHeight),
            VoxelRenderStyle.Default with
            {
                Background = background ?? PixelRenderSettings.DefaultBackground,
                Lighting = VoxelLightingSettings.Default with { Enabled = false },
                Outline = VoxelOutlineSettings.Default with { Enabled = false },
            });

    /// <summary>Renders a styled CPU fallback frame using a shared fixed layout.</summary>
    public PixelFramebuffer Render(
        VoxelMeshData mesh,
        VoxelCameraState camera,
        PixelRenderLayout layout,
        VoxelRenderStyle style) =>
        Render(mesh, camera, VoxelModelRotationState.Identity, layout, style);

    /// <summary>Renders a styled frame with independent object and camera rotations.</summary>
    public PixelFramebuffer Render(
        VoxelMeshData mesh,
        VoxelCameraState camera,
        VoxelModelRotationState modelRotation,
        PixelRenderLayout layout,
        VoxelRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(modelRotation);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(style);

        PixelRenderSettings settings = new(
            layout.Width,
            layout.Height,
            pixelsPerVoxel: 1,
            style.Background);
        VoxelRenderTransform transform = new VoxelRenderTransformResolver().Resolve(
            camera,
            modelRotation,
            mesh.Dimensions,
            settings);
        return Render(mesh, transform, layout, style);
    }

    /// <summary>Renders a styled frame using an already resolved shared transform.</summary>
    public PixelFramebuffer Render(
        VoxelMeshData mesh,
        VoxelRenderTransform transform,
        PixelRenderLayout layout,
        VoxelRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(style);

        PixelRasterSurface surface = _triangleRasterizer.RenderPixelSurface(
            mesh,
            transform,
            layout.Width,
            layout.Height,
            style);
        return PixelSurfacePostProcessor.Process(surface, style);
    }
}
