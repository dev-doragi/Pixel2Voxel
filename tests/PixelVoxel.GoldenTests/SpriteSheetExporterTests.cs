using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Export;
using PixelVoxel.Imaging;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.GoldenTests;

public sealed class SpriteSheetExporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"pixel-voxel-export-{Guid.NewGuid():N}");

    public SpriteSheetExporterTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task WritesHorizontalRgbaSheetAndAsepriteJsonArray()
    {
        string path = Path.Combine(_directory, "directions.png");
        SpriteFrame[] frames =
        [
            Frame("direction_00", 0, 0f, new Rgba32Color(255, 0, 0, 255)),
            Frame("direction_01", 1, -180f, new Rgba32Color(0, 255, 0, 0)),
        ];
        ISpriteExporter exporter = new SpriteSheetExporter(new PngPixelWriter());

        SpriteExportResult result = await exporter.ExportAsync(
            new SpriteSheetExportRequest(path, frames, true, "directions-2"),
            TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Width);
        Assert.Equal(1, result.Height);
        using (Image<ImageSharpColor> image = Image.Load<ImageSharpColor>(path))
        {
            Assert.Equal(new ImageSharpColor(255, 0, 0, 255), image[0, 0]);
            Assert.Equal(new ImageSharpColor(0, 255, 0, 0), image[2, 0]);
        }

        using JsonDocument json = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.JsonPath!, TestContext.Current.CancellationToken));
        JsonElement root = json.RootElement;
        Assert.Equal(JsonValueKind.Array, root.GetProperty("frames").ValueKind);
        Assert.Equal(2, root.GetProperty("frames").GetArrayLength());
        Assert.Equal(2, root.GetProperty("frames")[1].GetProperty("frame").GetProperty("x").GetInt32());
        Assert.Equal(
            "directions-2",
            root.GetProperty("meta").GetProperty("frameTags")[0].GetProperty("name").GetString());
        Assert.Equal(
            1,
            root.GetProperty("meta").GetProperty("slices")[0]
                .GetProperty("keys")[0].GetProperty("pivot").GetProperty("x").GetInt32());
    }

    [Fact]
    public async Task CancellationLeavesNoPartialOutput()
    {
        string path = Path.Combine(_directory, "cancelled.png");
        ISpriteExporter exporter = new SpriteSheetExporter(new PngPixelWriter());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            exporter.ExportAsync(
                new SpriteSheetExportRequest(
                    path,
                    [Frame("direction_00", 0, 0f, new Rgba32Color(1, 2, 3, 255))],
                    true),
                cancellation.Token));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(Path.ChangeExtension(path, ".json")));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static SpriteFrame Frame(
        string name,
        int index,
        float yaw,
        Rgba32Color firstPixel) =>
        new(
            name,
            index,
            yaw,
            2,
            1,
            [firstPixel, new Rgba32Color(9, 8, 7, 255)],
            pivotX: 1,
            pivotY: 1);
}
