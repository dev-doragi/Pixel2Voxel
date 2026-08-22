using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Export;
using PixelVoxel.Imaging;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.GoldenTests;

public sealed class TrimmedAtlasExporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"p2v-atlas-{Guid.NewGuid():N}");

    public TrimmedAtlasExporterTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task TrimsPacksAndWritesSourceOffsets()
    {
        Rgba32Color[] pixels = new Rgba32Color[16];
        pixels[6] = new Rgba32Color(10, 20, 30, 255);
        SpriteFrame frame = new("idle", 0, 0, 4, 4, pixels, 2, 3, 125);
        string path = Path.Combine(_directory, "atlas.png");

        SpriteExportResult result = await new TrimmedAtlasExporter(new PngPixelWriter()).ExportAsync(
            new TrimmedAtlasExportRequest(path, [frame], new TrimmedAtlasOptions()),
            TestContext.Current.CancellationToken);

        Assert.True(File.Exists(result.PngPath));
        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonPath!, TestContext.Current.CancellationToken));
        JsonElement item = json.RootElement.GetProperty("frames")[0];
        Assert.True(item.GetProperty("trimmed").GetBoolean());
        Assert.Equal(2, item.GetProperty("spriteSourceSize").GetProperty("x").GetInt32());
        Assert.Equal(1, item.GetProperty("spriteSourceSize").GetProperty("y").GetInt32());
        Assert.Equal(125, item.GetProperty("duration").GetInt32());
        int contentX = item.GetProperty("frame").GetProperty("x").GetInt32();
        int contentY = item.GetProperty("frame").GetProperty("y").GetInt32();
        using Image<ImageSharpColor> atlas = Image.Load<ImageSharpColor>(path);
        Assert.Equal(new ImageSharpColor(10, 20, 30, 255), atlas[contentX, contentY]);
        Assert.Equal(atlas[contentX, contentY], atlas[contentX - 1, contentY]);
        Assert.Equal(atlas[contentX, contentY], atlas[contentX, contentY - 1]);
        Assert.Equal(new ImageSharpColor(0, 0, 0, 0), atlas[contentX - 2, contentY]);
    }

    [Fact]
    public async Task RejectsDuplicateFrameNamesBeforeWriting()
    {
        SpriteFrame frame = new("same", 0, 0, 1, 1, [new Rgba32Color(1, 2, 3, 255)], 0, 0);
        string path = Path.Combine(_directory, "duplicates.png");

        await Assert.ThrowsAsync<InvalidDataException>(() => new TrimmedAtlasExporter(new PngPixelWriter()).ExportAsync(
            new(path, [frame, frame], new()), TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(Path.ChangeExtension(path, ".json")));
    }

    [Fact]
    public async Task JsonCommitFailureRestoresThePreviousPng()
    {
        string path = Path.Combine(_directory, "rollback.png");
        await new PngPixelWriter().WriteAsync(
            path, 1, 1, new[] { new Rgba32Color(9, 8, 7, 255) },
            TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.ChangeExtension(path, ".json"));
        SpriteFrame replacement = new(
            "replacement", 0, 0, 1, 1, [new Rgba32Color(1, 2, 3, 255)], 0, 0);

        Exception failure = await Assert.ThrowsAnyAsync<Exception>(() => new TrimmedAtlasExporter(new PngPixelWriter()).ExportAsync(
            new(path, [replacement], new()), TestContext.Current.CancellationToken));
        Assert.True(failure is IOException or UnauthorizedAccessException);

        await using FileStream stream = File.OpenRead(path);
        OrthographicImage restored = await new PngPixelReader().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(new Rgba32Color(9, 8, 7, 255), restored.GetPixel(0, 0));
    }

    [Fact]
    public async Task ProducesDeterministicLayoutAndRejectsSinglePageOverflow()
    {
        SpriteFrame[] frames = Enumerable.Range(0, 3).Select(index => new SpriteFrame(
            $"f{index}", index, 0, 2, 2,
            Enumerable.Repeat(new Rgba32Color((byte)index, 0, 0, 255), 4), 1, 1)).ToArray();
        TrimmedAtlasExporter exporter = new(new PngPixelWriter());
        SpriteExportResult first = await exporter.ExportAsync(new(Path.Combine(_directory, "a.png"), frames, new()), TestContext.Current.CancellationToken);
        SpriteExportResult second = await exporter.ExportAsync(new(Path.Combine(_directory, "b.png"), frames, new()), TestContext.Current.CancellationToken);

        Assert.Equal(await File.ReadAllBytesAsync(first.PngPath, TestContext.Current.CancellationToken), await File.ReadAllBytesAsync(second.PngPath, TestContext.Current.CancellationToken));
        Assert.Equal(
            JsonDocument.Parse(await File.ReadAllTextAsync(first.JsonPath!, TestContext.Current.CancellationToken)).RootElement.GetProperty("frames").ToString(),
            JsonDocument.Parse(await File.ReadAllTextAsync(second.JsonPath!, TestContext.Current.CancellationToken)).RootElement.GetProperty("frames").ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => exporter.ExportAsync(
            new(Path.Combine(_directory, "overflow.png"), frames, new TrimmedAtlasOptions(2, 1, 4)), TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
