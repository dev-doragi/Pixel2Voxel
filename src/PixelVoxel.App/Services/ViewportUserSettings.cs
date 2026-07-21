using System.Text.Json;
using System.Text.Json.Serialization;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Services;

/// <summary>Stores user-owned viewport preferences independently from project files.</summary>
public sealed record ViewportUserSettings
{
    public int Version { get; init; } = 2;

    public float DefaultYaw { get; init; } = -45f;

    public float DefaultPitch { get; init; } = -30f;

    public bool CameraFaceSnapEnabled { get; init; } = true;

    public bool ZoomIsFit { get; init; } = true;

    public int ManualZoomScale { get; init; } = 6;

    public float AnimationSpeed { get; init; } = 30f;

    public bool LightingEnabled { get; init; } = true;

    public float LightAzimuth { get; init; } = -45f;

    public float LightElevation { get; init; } = 45f;

    public float Ambient { get; init; } = 0.35f;

    public float Intensity { get; init; } = 0.65f;

    public bool OutlineEnabled { get; init; } = true;

    public VoxelOutlineMode OutlineMode { get; init; } = VoxelOutlineMode.Silhouette;

    public string OutlineColor { get; init; } = "#000000";

    public string BackgroundColor { get; init; } = "#141820";

    public int ExportDirectionCount { get; init; } = 8;

    public bool ExportTrueIsometric { get; init; }

    public bool ExportTransparentBackground { get; init; } = true;

    public IReadOnlyList<RecentImportSettings> RecentImports { get; init; } = [];
}

/// <summary>Stores one recent import source and its reusable alignment.</summary>
public sealed record RecentImportSettings
{
    public SixViewImportSourceKind SourceKind { get; init; }

    public IReadOnlyList<string> Paths { get; init; } = [];

    public IReadOnlyList<RecentFaceAlignmentSettings> Alignments { get; init; } = [];

    [JsonIgnore]
    public string DisplayName => Paths.Count == 0
        ? "Missing import"
        : Path.GetFileName(Paths[0]);
}

/// <summary>Stores one recent source-to-face mapping and pixel alignment.</summary>
public sealed record RecentFaceAlignmentSettings
{
    public int SourceSlotIndex { get; init; }

    public VoxelFace TargetFace { get; init; }

    public bool FlipHorizontal { get; init; }

    public bool FlipVertical { get; init; }

    public int OffsetX { get; init; }

    public int OffsetY { get; init; }
}

/// <summary>Loads and atomically saves debounced viewport settings.</summary>
public sealed class ViewportSettingsStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly object _gate = new();
    private Timer? _timer;
    private ViewportUserSettings? _pending;

    public ViewportSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PixelVoxel",
            "settings.json");
    }

    public (ViewportUserSettings Settings, string? Diagnostic) Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return (new ViewportUserSettings(), null);
            }

            ViewportUserSettings? settings = JsonSerializer.Deserialize<ViewportUserSettings>(
                File.ReadAllText(_path),
                JsonOptions);
            if (settings is null || settings.Version is not (1 or 2))
            {
                return (new ViewportUserSettings(), "Viewport settings were reset because their version is unsupported.");
            }

            return settings.Version == 1
                ? (settings with { Version = 2 }, "Viewport settings were upgraded to version 2.")
                : (settings, null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new ViewportUserSettings(), $"Viewport settings were reset: {exception.Message}");
        }
    }

    public void SaveDebounced(ViewportUserSettings settings)
    {
        lock (_gate)
        {
            _pending = settings;
            _timer ??= new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(300, Timeout.Infinite);
        }
    }

    public void Flush()
    {
        ViewportUserSettings? settings;
        lock (_gate)
        {
            settings = _pending;
            _pending = null;
        }

        if (settings is null)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(_path);
        if (directory is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            string temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // User settings are non-critical. Keep the application running if persistence is unavailable.
        }
    }

    public void Dispose()
    {
        Flush();
        _timer?.Dispose();
        _timer = null;
    }
}
