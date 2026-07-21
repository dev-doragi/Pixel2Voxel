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
    bool ExportTransparentBackground);

/// <summary>Contains the persistent source views, editable model, and project settings.</summary>
public sealed class PixelVoxelProject
{
    /// <summary>Initializes a portable project snapshot.</summary>
    public PixelVoxelProject(
        VoxelDocument document,
        OrthographicViewSet? sourceViews,
        PixelVoxelProjectSettings settings)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        SourceViews = sourceViews;
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Gets the editable document restored from the project.</summary>
    public VoxelDocument Document { get; }

    /// <summary>Gets normalized source views when the project was reconstructed from images.</summary>
    public OrthographicViewSet? SourceViews { get; }

    /// <summary>Gets project-specific presentation and export values.</summary>
    public PixelVoxelProjectSettings Settings { get; }
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
