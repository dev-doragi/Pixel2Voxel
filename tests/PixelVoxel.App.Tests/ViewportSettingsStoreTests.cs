using PixelVoxel.App.Services;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Tests;

public sealed class ViewportSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"PixelVoxel.App.Tests-{Guid.NewGuid():N}");

    [Fact]
    public void SavesAndReloadsVersionOneSettings()
    {
        string path = GetSettingsPath();
        ViewportUserSettings expected = new()
        {
            DefaultYaw = 15f,
            DefaultPitch = -20f,
            ZoomIsFit = false,
            ManualZoomScale = 7,
            AnimationSpeed = 45f,
            LightingEnabled = false,
            OutlineMode = VoxelOutlineMode.SilhouetteAndDepth,
            OutlineColor = "#112233",
            BackgroundColor = "#445566",
        };
        using (ViewportSettingsStore writer = new(path))
        {
            writer.SaveDebounced(expected);
            writer.Flush();
        }

        using ViewportSettingsStore reader = new(path);
        (ViewportUserSettings actual, string? diagnostic) = reader.Load();

        Assert.Null(diagnostic);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CorruptJsonReturnsDefaultsAndANonFatalDiagnostic()
    {
        string path = GetSettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ this is not json }");
        using ViewportSettingsStore store = new(path);

        (ViewportUserSettings settings, string? diagnostic) = store.Load();

        Assert.Equal(new ViewportUserSettings(), settings);
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public void RunningAnimationFlagsAreNotPartOfPersistedSettings()
    {
        string[] propertyNames = typeof(ViewportUserSettings)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("HorizontalAnimationEnabled", propertyNames);
        Assert.DoesNotContain("VerticalAnimationEnabled", propertyNames);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string GetSettingsPath() => Path.Combine(_directory, "settings.json");
}
