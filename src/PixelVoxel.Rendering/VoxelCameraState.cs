namespace PixelVoxel.Rendering;

/// <summary>Identifies whether the camera is output-stable or freely inspectable.</summary>
public enum VoxelViewMode
{
    /// <summary>Uses output presets and integer pixel snapping.</summary>
    PixelPreview,

    /// <summary>Allows continuously changing camera angles.</summary>
    FreeView,
}

/// <summary>Identifies a predefined orthographic camera orientation.</summary>
public enum VoxelCameraPreset
{
    /// <summary>Uses a 2:1 pixel-art axis slope.</summary>
    Pixel2To1,

    /// <summary>Uses mathematically equal isometric foreshortening.</summary>
    TrueIsometric,

    /// <summary>Looks at the Front face.</summary>
    Front,

    /// <summary>Looks at the Right face.</summary>
    Right,

    /// <summary>Looks down from the Top face.</summary>
    Top,

    /// <summary>Uses caller-provided yaw and pitch.</summary>
    Free,
}

/// <summary>
/// Stores user-facing orthographic camera controls before pixel snapping is resolved.
/// </summary>
public sealed record VoxelCameraState(
    VoxelViewMode Mode,
    VoxelCameraPreset Preset,
    float YawDegrees,
    float PitchDegrees,
    float PanX,
    float PanY,
    float Zoom)
{
    /// <summary>Creates the default 2:1 pixel preview camera.</summary>
    public static VoxelCameraState Pixel2To1() =>
        new(VoxelViewMode.PixelPreview, VoxelCameraPreset.Pixel2To1, -45f, -30f, 0f, 0f, 1f);
}
