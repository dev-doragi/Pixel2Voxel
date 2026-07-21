using System.Numerics;
using PixelVoxel.Core;
using PixelVoxel.Export;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Services;

/// <summary>Bridges render snapshots to backend-independent sprite export contracts.</summary>
public sealed class SpriteExportCoordinator
{
    private readonly PixelArtVoxelRasterizer _rasterizer;
    private readonly VoxelRenderTransformResolver _transformResolver;
    private readonly ISpriteExporter _exporter;

    /// <summary>Initializes the application-level sprite export workflow.</summary>
    public SpriteExportCoordinator(
        PixelArtVoxelRasterizer rasterizer,
        VoxelRenderTransformResolver transformResolver,
        ISpriteExporter exporter)
    {
        _rasterizer = rasterizer ?? throw new ArgumentNullException(nameof(rasterizer));
        _transformResolver = transformResolver ?? throw new ArgumentNullException(nameof(transformResolver));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    /// <summary>Exports the exact logical current view as one PNG.</summary>
    public async Task<SpriteExportResult> ExportCurrentViewAsync(
        string path,
        VoxelMeshData mesh,
        VoxelCameraState camera,
        VoxelModelRotationState modelRotation,
        PixelRenderLayout layout,
        VoxelRenderStyle style,
        bool transparentBackground,
        CancellationToken cancellationToken = default)
    {
        VoxelRenderStyle outputStyle = ResolveOutputStyle(style, transparentBackground);
        SpriteFrame frame = await Task.Run(() => RenderFrame(
            "current_view",
            0,
            modelRotation.YawDegrees,
            mesh,
            camera,
            modelRotation,
            layout,
            outputStyle,
            cancellationToken), cancellationToken);
        return await _exporter.ExportAsync(
            new SpriteSheetExportRequest(path, [frame], writeAsepriteJson: false),
            cancellationToken);
    }

    /// <summary>Exports a fixed-preset 4, 8, or 16 direction horizontal sprite sheet.</summary>
    public async Task<SpriteExportResult> ExportDirectionSheetAsync(
        string path,
        int directionCount,
        VoxelCameraPreset cameraPreset,
        VoxelMeshData mesh,
        PixelRenderLayout layout,
        VoxelRenderStyle style,
        bool transparentBackground,
        CancellationToken cancellationToken = default)
    {
        if (directionCount is not (4 or 8 or 16))
        {
            throw new ArgumentOutOfRangeException(nameof(directionCount));
        }

        if (cameraPreset is not (VoxelCameraPreset.Pixel2To1 or VoxelCameraPreset.TrueIsometric))
        {
            throw new ArgumentOutOfRangeException(nameof(cameraPreset));
        }

        VoxelCameraState camera = new(
            VoxelViewMode.PixelPreview,
            cameraPreset,
            0f,
            0f,
            0f,
            0f,
            1f);
        VoxelRenderStyle outputStyle = ResolveOutputStyle(style, transparentBackground);
        float[] yaws = GetClockwiseDirectionYaws(directionCount);
        SpriteFrame[] frames = await Task.Run(() =>
        {
            SpriteFrame[] output = new SpriteFrame[yaws.Length];
            (int PivotX, int PivotY)? sharedPivot = null;
            for (int index = 0; index < yaws.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VoxelModelRotationState rotation = new(yaws[index], 0f);
                SpriteFrame rendered = RenderFrame(
                    $"direction_{index:D2}",
                    index,
                    yaws[index],
                    mesh,
                    camera,
                    rotation,
                    layout,
                    outputStyle,
                    cancellationToken);
                sharedPivot ??= (rendered.PivotX, rendered.PivotY);
                output[index] = new SpriteFrame(
                    rendered.Name,
                    rendered.DirectionIndex,
                    rendered.YawDegrees,
                    rendered.Width,
                    rendered.Height,
                    rendered.Pixels.ToArray(),
                    sharedPivot.Value.PivotX,
                    sharedPivot.Value.PivotY);
            }

            return output;
        }, cancellationToken);

        return await _exporter.ExportAsync(
            new SpriteSheetExportRequest(
                path,
                frames,
                writeAsepriteJson: true,
                directionTag: $"directions-{directionCount}"),
            cancellationToken);
    }

    /// <summary>Gets model yaw angles ordered clockwise from the unrotated model.</summary>
    public static float[] GetClockwiseDirectionYaws(int directionCount)
    {
        if (directionCount is not (4 or 8 or 16))
        {
            throw new ArgumentOutOfRangeException(nameof(directionCount));
        }

        float step = 360f / directionCount;
        return Enumerable.Range(0, directionCount)
            .Select(index => VoxelCameraMotion.WrapAngle(-step * index))
            .ToArray();
    }

    private SpriteFrame RenderFrame(
        string name,
        int index,
        float yaw,
        VoxelMeshData mesh,
        VoxelCameraState camera,
        VoxelModelRotationState rotation,
        PixelRenderLayout layout,
        VoxelRenderStyle style,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PixelFramebuffer framebuffer = _rasterizer.Render(mesh, camera, rotation, layout, style);
        PixelRenderSettings settings = new(layout.Width, layout.Height, 1, style.Background);
        VoxelRenderTransform transform = _transformResolver.Resolve(
            camera,
            rotation,
            mesh.Dimensions,
            settings);
        Vector3 groundCenter = new(
            mesh.Dimensions.Width / 2f,
            0f,
            mesh.Dimensions.Depth / 2f);
        Vector3 projectedPivot = transform.ProjectToScreen(groundCenter);
        int pivotX = Math.Clamp(
            (int)MathF.Round(projectedPivot.X, MidpointRounding.AwayFromZero),
            0,
            layout.Width);
        int pivotY = Math.Clamp(
            (int)MathF.Round(projectedPivot.Y, MidpointRounding.AwayFromZero),
            0,
            layout.Height);
        return new SpriteFrame(
            name,
            index,
            yaw,
            framebuffer.Width,
            framebuffer.Height,
            framebuffer.Pixels.ToArray(),
            pivotX,
            pivotY);
    }

    private static VoxelRenderStyle ResolveOutputStyle(
        VoxelRenderStyle style,
        bool transparentBackground) =>
        transparentBackground
            ? style with
            {
                Background = new Rgba32Color(
                    style.Background.Red,
                    style.Background.Green,
                    style.Background.Blue,
                    0),
            }
            : style;
}
