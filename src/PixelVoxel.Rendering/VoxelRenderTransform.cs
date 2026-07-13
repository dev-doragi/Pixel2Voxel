using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>
/// Contains the resolved per-frame transform shared by CPU and OpenGL rendering.
/// </summary>
public sealed class VoxelRenderTransform
{
    internal VoxelRenderTransform(
        Matrix4x4 modelRotation,
        Matrix4x4 viewRotation,
        Matrix4x4 clipFromModel,
        Vector3 modelCenter,
        float pixelsPerVoxel,
        float screenOffsetX,
        float screenOffsetY,
        int width,
        int height)
    {
        ModelRotation = modelRotation;
        ViewRotation = viewRotation;
        CombinedRotation = modelRotation * viewRotation;
        ClipFromModel = clipFromModel;
        ModelCenter = modelCenter;
        PixelsPerVoxel = pixelsPerVoxel;
        ScreenOffsetX = screenOffsetX;
        ScreenOffsetY = screenOffsetY;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the model-to-clip matrix used by the OpenGL shader.</summary>
    public Matrix4x4 ClipFromModel { get; }

    /// <summary>Gets the object-local to world rotation.</summary>
    public Matrix4x4 ModelRotation { get; }

    /// <summary>Gets the world-to-camera orientation used by the viewport.</summary>
    public Matrix4x4 ViewRotation { get; }

    /// <summary>Gets the geometric center of the voxel bounds.</summary>
    public Vector3 ModelCenter { get; }

    /// <summary>Gets the resolved internal pixel scale.</summary>
    public float PixelsPerVoxel { get; }

    /// <summary>Gets the snapped or free horizontal framebuffer anchor.</summary>
    public float ScreenOffsetX { get; }

    /// <summary>Gets the snapped or free vertical framebuffer anchor.</summary>
    public float ScreenOffsetY { get; }

    /// <summary>Gets the target framebuffer width.</summary>
    public int Width { get; }

    /// <summary>Gets the target framebuffer height.</summary>
    public int Height { get; }

    internal Matrix4x4 CombinedRotation { get; }

    /// <summary>Projects a model vertex into top-left framebuffer coordinates.</summary>
    public Vector3 ProjectToScreen(Vector3 position)
    {
        Vector3 view = Vector3.Transform(position - ModelCenter, CombinedRotation);
        return new Vector3(
            ScreenOffsetX + (view.X * PixelsPerVoxel),
            ScreenOffsetY - (view.Y * PixelsPerVoxel),
            view.Z);
    }

    /// <summary>Determines whether an outward normal faces the current camera.</summary>
    public bool IsFrontFacing(Vector3 normal) =>
        Vector3.TransformNormal(normal, CombinedRotation).Z > 0f;

    /// <summary>Transforms an object-local normal into world space for lighting.</summary>
    public Vector3 TransformNormalToWorld(Vector3 normal) =>
        Vector3.Normalize(Vector3.TransformNormal(normal, ModelRotation));
}

/// <summary>Resolves camera presets, model centering, and framebuffer pixel snapping.</summary>
public sealed class VoxelRenderTransformResolver
{
    private const float TrueIsometricPitch = -35.2643897f;

    /// <summary>Resolves an immutable transform for one frame.</summary>
    public VoxelRenderTransform Resolve(
        VoxelCameraState camera,
        VoxelDimensions dimensions,
        PixelRenderSettings settings) =>
        Resolve(camera, VoxelModelRotationState.Identity, dimensions, settings);

    /// <summary>Resolves independent model and camera rotations into one immutable frame transform.</summary>
    public VoxelRenderTransform Resolve(
        VoxelCameraState camera,
        VoxelModelRotationState modelRotation,
        VoxelDimensions dimensions,
        PixelRenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(modelRotation);
        ArgumentNullException.ThrowIfNull(settings);

        if (!float.IsFinite(camera.Zoom) || camera.Zoom <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(camera));
        }

        if (!float.IsFinite(modelRotation.YawDegrees) ||
            !float.IsFinite(modelRotation.PitchDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(modelRotation));
        }

        Matrix4x4 viewRotation = ResolveRotation(camera);
        Matrix4x4 objectRotation = ResolveModelRotation(modelRotation);
        Matrix4x4 combinedRotation = objectRotation * viewRotation;

        Vector3 center = new(
            dimensions.Width / 2f,
            dimensions.Height / 2f,
            dimensions.Depth / 2f);
        float scale = settings.PixelsPerVoxel * camera.Zoom;
        float offsetX = (settings.Width / 2f) + camera.PanX;
        float offsetY = (settings.Height / 2f) + camera.PanY;

        if (camera.Mode == VoxelViewMode.PixelPreview)
        {
            offsetX = MathF.Round(offsetX);
            offsetY = MathF.Round(offsetY);
        }

        float diagonal = MathF.Max(
            1f,
            MathF.Sqrt(
                (dimensions.Width * dimensions.Width) +
                (dimensions.Height * dimensions.Height) +
                (dimensions.Depth * dimensions.Depth)));
        Matrix4x4 clipFromModel =
            Matrix4x4.CreateTranslation(-center) *
            combinedRotation *
            Matrix4x4.CreateScale(
                (2f * scale) / settings.Width,
                (2f * scale) / settings.Height,
                -1f / diagonal) *
            Matrix4x4.CreateTranslation(
                ((2f * offsetX) / settings.Width) - 1f,
                1f - ((2f * offsetY) / settings.Height),
                0f);

        return new VoxelRenderTransform(
            objectRotation,
            viewRotation,
            clipFromModel,
            center,
            scale,
            offsetX,
            offsetY,
            settings.Width,
            settings.Height);
    }

    internal static Matrix4x4 ResolveRotation(VoxelCameraState camera)
    {
        (float yaw, float pitch) = ResolveAngles(camera);
        return
            Matrix4x4.CreateRotationY(DegreesToRadians(yaw)) *
            Matrix4x4.CreateRotationX(DegreesToRadians(-pitch));
    }

    internal static Matrix4x4 ResolveModelRotation(VoxelModelRotationState modelRotation) =>
        Matrix4x4.CreateRotationY(DegreesToRadians(modelRotation.YawDegrees)) *
        Matrix4x4.CreateRotationX(DegreesToRadians(-modelRotation.PitchDegrees));

    private static (float Yaw, float Pitch) ResolveAngles(VoxelCameraState camera) =>
        camera.Preset switch
        {
            VoxelCameraPreset.Pixel2To1 => (-45f, -30f),
            VoxelCameraPreset.TrueIsometric => (-45f, TrueIsometricPitch),
            VoxelCameraPreset.Front => (0f, 0f),
            VoxelCameraPreset.Right => (-90f, 0f),
            VoxelCameraPreset.Top => (0f, -90f),
            VoxelCameraPreset.Free => (camera.YawDegrees, camera.PitchDegrees),
            _ => throw new ArgumentOutOfRangeException(nameof(camera)),
        };

    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);
}
