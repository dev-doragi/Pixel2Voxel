using PixelVoxel.Core;

namespace PixelVoxel.Export;

/// <summary>Stores project-specific camera, render, and export values without a UI dependency.</summary>
public sealed record PixelVoxelProjectSettings(
    string CameraPreset,
    float CameraYaw,
    float CameraPitch,
    float ModelYaw,
    float ModelPitch,
    Rgba32Color BackgroundColor,
    bool LightingEnabled,
    float LightAzimuth,
    float LightElevation,
    float LightAmbient,
    float LightIntensity,
    bool OutlineEnabled,
    bool OutlineIncludeDepth,
    Rgba32Color OutlineColor,
    int ExportDirectionCount,
    bool ExportTrueIsometric,
    bool ExportTransparentBackground,
    float ModelRoll = 0f,
    float? ModelOrientationX = null,
    float? ModelOrientationY = null,
    float? ModelOrientationZ = null,
    float? ModelOrientationW = null);

/// <summary>Contains the persistent source views, editable model, and project settings.</summary>
public sealed class PixelVoxelProject
{
    /// <summary>Initializes a portable project snapshot.</summary>
    public PixelVoxelProject(
        VoxelDocument document,
        OrthographicViewSet? sourceViews,
        PixelVoxelProjectSettings settings,
        IEnumerable<Rgba32Color>? palette = null)
        : this(
            [new PixelVoxelProjectFrame("frame_0000", 100, document, sourceViews)],
            0,
            settings,
            palette)
    {
    }

    /// <summary>Initializes a project containing one or more independently editable frames.</summary>
    public PixelVoxelProject(
        IEnumerable<PixelVoxelProjectFrame> frames,
        int currentFrameIndex,
        PixelVoxelProjectSettings settings,
        IEnumerable<Rgba32Color>? palette = null)
    {
        Frames = frames?.ToArray() ?? throw new ArgumentNullException(nameof(frames));
        if (Frames.Count == 0) throw new ArgumentException("At least one project frame is required.", nameof(frames));
        if ((uint)currentFrameIndex >= (uint)Frames.Count) throw new ArgumentOutOfRangeException(nameof(currentFrameIndex));
        CurrentFrameIndex = currentFrameIndex;
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Palette = palette?.ToArray() ?? [];
        if (Palette.Count > 32) throw new ArgumentOutOfRangeException(nameof(palette));
    }

    /// <summary>Gets the editable document restored from the project.</summary>
    public VoxelDocument Document => Frames[CurrentFrameIndex].Document;

    /// <summary>Gets normalized source views when the project was reconstructed from images.</summary>
    public OrthographicViewSet? SourceViews => Frames[CurrentFrameIndex].SourceViews;

    /// <summary>Gets all independently editable timeline frames.</summary>
    public IReadOnlyList<PixelVoxelProjectFrame> Frames { get; }

    /// <summary>Gets the frame selected when the project was saved.</summary>
    public int CurrentFrameIndex { get; }

    /// <summary>Gets project-specific presentation and export values.</summary>
    public PixelVoxelProjectSettings Settings { get; }

    /// <summary>Gets up to 32 project-owned RGBA editing colors.</summary>
    public IReadOnlyList<Rgba32Color> Palette { get; }
}

/// <summary>Contains one editable voxel frame and its synchronized source views.</summary>
public sealed class PixelVoxelProjectFrame
{
    public PixelVoxelProjectFrame(
        string name,
        int durationMilliseconds,
        VoxelDocument document,
        OrthographicViewSet? sourceViews)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (durationMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
        Name = name;
        DurationMilliseconds = durationMilliseconds;
        Document = document ?? throw new ArgumentNullException(nameof(document));
        SourceViews = sourceViews;
    }

    public string Name { get; }
    public int DurationMilliseconds { get; }
    public VoxelDocument Document { get; }
    public OrthographicViewSet? SourceViews { get; }
}

/// <summary>Defines the boundary for portable Pixel Voxel project persistence.</summary>
public interface IProjectSerializer
{
    /// <summary>Saves a complete project snapshot to one .pxv file.</summary>
    Task SaveAsync(
        string path,
        PixelVoxelProject project,
        CancellationToken cancellationToken = default);

    /// <summary>Loads and validates a complete .pxv project without mutating application state.</summary>
    Task<PixelVoxelProject> LoadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
