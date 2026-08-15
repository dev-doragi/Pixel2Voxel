using PixelVoxel.App.Services;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Tests;

public sealed class ViewportSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"PixelVoxel.App.Tests-{Guid.NewGuid():N}");

    [Fact]
    public void SavesAndReloadsVersionFourSettings()
    {
        string path = GetSettingsPath();
        ViewportUserSettings expected = new()
        {
            DefaultYaw = 15f,
            DefaultPitch = -20f,
            CameraFaceSnapEnabled = false,
            CameraFaceSnapAngle = 18f,
            ZoomIsFit = false,
            ManualZoomScale = 7,
            AnimationSpeed = 45f,
            AnimationFramesPerSecond = 24,
            GifExportResizePercent = 600,
            LeftPanelWidth = 420,
            RightPanelVisible = false,
            LightingEnabled = false,
            OutlineMode = VoxelOutlineMode.SilhouetteAndDepth,
            OutlineColor = "#112233",
            BackgroundColor = "#445566",
            ExportDirectionCount = 16,
            ExportTrueIsometric = true,
            ExportTransparentBackground = false,
            RecentImports =
            [
                new RecentImportSettings
                {
                    SourceKind = SixViewImportSourceKind.HorizontalSheet,
                    Paths = ["C:\\sprites\\six-view.png"],
                    Alignments =
                    [
                        new RecentFaceAlignmentSettings
                        {
                            SourceSlotIndex = 2,
                            TargetFace = VoxelFace.Front,
                            FlipHorizontal = true,
                            OffsetX = 3,
                            OffsetY = -2,
                        },
                    ],
                },
            ],
        };
        using (ViewportSettingsStore writer = new(path))
        {
            writer.SaveDebounced(expected);
            writer.Flush();
        }

        using ViewportSettingsStore reader = new(path);
        (ViewportUserSettings actual, string? diagnostic) = reader.Load();

        Assert.Null(diagnostic);
        Assert.Equivalent(expected, actual, strict: true);
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
    public void VersionOneSettingsUpgradeWithVersionFourDefaults()
    {
        string path = GetSettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"Version\":1,\"DefaultYaw\":25}");
        using ViewportSettingsStore store = new(path);

        (ViewportUserSettings settings, string? diagnostic) = store.Load();

        Assert.Equal(4, settings.Version);
        Assert.Equal(25f, settings.DefaultYaw);
        Assert.Equal(8, settings.ExportDirectionCount);
        Assert.True(settings.ExportTransparentBackground);
        Assert.Empty(settings.RecentImports);
        Assert.Equal(360, settings.LeftPanelWidth);
        Assert.Equal(12, settings.AnimationFramesPerSecond);
        Assert.Equal(400, settings.GifExportResizePercent);
        Assert.Equal(10f, settings.CameraFaceSnapAngle);
        Assert.Contains("upgraded", diagnostic);
    }

    [Fact]
    public void VersionThreeIntegerGifScaleMigratesToResizePercent()
    {
        string path = GetSettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"Version\":3,\"GifExportScale\":8}");
        using ViewportSettingsStore store = new(path);

        (ViewportUserSettings settings, string? diagnostic) = store.Load();

        Assert.Equal(4, settings.Version);
        Assert.Equal(800, settings.GifExportResizePercent);
        Assert.Equal(0, settings.GifExportScale);
        Assert.Contains("upgraded", diagnostic);
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
        Assert.DoesNotContain("YawAnimationEnabled", propertyNames);
        Assert.DoesNotContain("PitchAnimationEnabled", propertyNames);
        Assert.DoesNotContain("RollAnimationEnabled", propertyNames);
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
