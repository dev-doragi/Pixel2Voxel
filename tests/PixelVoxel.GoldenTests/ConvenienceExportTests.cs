using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Export;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.GoldenTests;

public sealed class ConvenienceExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PixelVoxel.ConvenienceExports", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AnimationSheetWritesPerFrameDurationsAndRotationTag()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "rotation.png");
        SpriteFrame[] frames = [Frame("rotation_0000", 0, 83), Frame("rotation_0001", 1, 84)];

        SpriteExportResult result = await new SpriteSheetExporter(new PngPixelWriter()).ExportAsync(
            new SpriteSheetExportRequest(path, frames, true, "rotation"), TestContext.Current.CancellationToken);

        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonPath!, TestContext.Current.CancellationToken));
        Assert.Equal(83, json.RootElement.GetProperty("frames")[0].GetProperty("duration").GetInt32());
        Assert.Equal("rotation", json.RootElement.GetProperty("meta").GetProperty("frameTags")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GifLoopsAndPreservesFrameDelay()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "rotation.gif");
        GifExportResult result = await new GifAnimationExporter().ExportAsync(path,
            [Frame("a", 0, 100), Frame("b", 1, 120)], TestContext.Current.CancellationToken, resizePercent: 400);

        using Image<ImageSharpColor> image = await Image.LoadAsync<ImageSharpColor>(path, TestContext.Current.CancellationToken);
        Assert.Equal(2, image.Frames.Count);
        Assert.Equal(0, image.Metadata.GetGifMetadata().RepeatCount);
        Assert.Equal(10, image.Frames[0].Metadata.GetGifMetadata().FrameDelay);
        Assert.Equal(8, image.Width);
        Assert.Equal(4, image.Height);
        Assert.False(result.WasQuantized);
    }

    [Theory]
    [InlineData(100, 32, 32)]
    [InlineData(1000, 320, 320)]
    public async Task GifResizePercentMatchesAsepriteCanvasSizing(
        int resizePercent,
        int expectedWidth,
        int expectedHeight)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, $"rotation-{resizePercent}.gif");
        SpriteFrame frame = new(
            "a", 0, 0f, 32, 32,
            Enumerable.Repeat(new Rgba32Color(255, 0, 0, 255), 32 * 32).ToArray(),
            16, 16, 100);

        await new GifAnimationExporter().ExportAsync(
            path, [frame], TestContext.Current.CancellationToken, resizePercent);

        using Image<ImageSharpColor> image = await Image.LoadAsync<ImageSharpColor>(
            path, TestContext.Current.CancellationToken);
        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(expectedHeight, image.Height);
    }

    [Fact]
    public async Task UnityPackageContainsSurfaceOnlyPaletteAndScopedImporter()
    {
        VoxelDocument document = new(new VoxelDimensions(2, 1, 1),
        [
            new VoxelEntry(new VoxelCoordinate(0, 0, 0), VoxelCell.CreateUniform(new Rgba32Color(255, 0, 0, 255))),
            new VoxelEntry(new VoxelCoordinate(1, 0, 0), VoxelCell.CreateUniform(new Rgba32Color(0, 0, 255, 255))),
        ]);
        VoxelMeshData mesh = Assert.IsType<VoxelMeshData>(new VoxelSurfaceMesher().Build(document, TestContext.Current.CancellationToken).Mesh);

        ObjExportResult result = await new UnityObjExporter(new PngPixelWriter()).ExportAsync(
            new ObjExportRequest(_directory, "two voxels", mesh), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.FaceCount);
        Assert.Equal(2, result.PaletteColorCount);
        string obj = await File.ReadAllTextAsync(result.ObjPath, TestContext.Current.CancellationToken);
        Assert.Equal(10, obj.Split('\n').Count(line => line.StartsWith("f ", StringComparison.Ordinal)));
        Assert.Contains("mtllib two voxels.mtl", obj);
        string importer = await File.ReadAllTextAsync(result.UnityImporterPath, TestContext.Current.CancellationToken);
        Assert.Contains(".pixelvoxel.json", importer);
        Assert.Contains("FilterMode.Point", importer);
        Assert.Contains("mipmapEnabled = false", importer);
    }

    private static SpriteFrame Frame(string name, int index, int duration) => new(
        name, index, index * 10f, 2, 1,
        [new Rgba32Color((byte)(10 + index), 20, 30, 255), new Rgba32Color(0, 0, 0, 0)],
        1, 1, duration);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
