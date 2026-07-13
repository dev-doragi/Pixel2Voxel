using System.Text.Json;
using System.Text.Json.Serialization;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Services;

/// <summary>Stores user-owned viewport preferences independently from project files.</summary>
public sealed record ViewportUserSettings
{
    public int Version { get; init; } = 1;

    public float DefaultYaw { get; init; } = -45f;

    public float DefaultPitch { get; init; } = -30f;

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
            if (settings is null || settings.Version != 1)
            {
                return (new ViewportUserSettings(), "Viewport settings were reset because their version is unsupported.");
            }

            return (settings, null);
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
