using System.IO.Compression;
using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Export;
using PixelVoxel.Imaging;

namespace PixelVoxel.GoldenTests;

public sealed class PxvProjectSerializerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "PixelVoxel.PxvTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ProjectRoundTripPreservesDocumentViewsAndSettings()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "model.pxv");
        PxvProjectSerializer serializer = CreateSerializer();
        PixelVoxelProject source = CreateProject();

        await serializer.SaveAsync(path, source, TestContext.Current.CancellationToken);
        PixelVoxelProject loaded = await serializer.LoadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(source.Document.Storage.Dimensions, loaded.Document.Storage.Dimensions);
        Assert.Equal(source.Document.Storage.OccupiedCount, loaded.Document.Storage.OccupiedCount);
        Assert.True(loaded.Document.Storage.TryGetCell(new VoxelCoordinate(1, 0, 0), out VoxelCell? cell));
        Assert.Equal(new Rgba32Color(10, 20, 30, 255), cell!.GetColor(VoxelFace.Front));
        Assert.NotNull(loaded.SourceViews);
        Assert.Equal(new Rgba32Color(1, 2, 3, 255), loaded.SourceViews![VoxelFace.Front].GetPixel(0, 0));
        Assert.Equal(source.Settings, loaded.Settings);
        Assert.Equal(source.Palette, loaded.Palette);
    }

    [Fact]
    public async Task SavingTheSameSnapshotProducesDeterministicBytes()
    {
        Directory.CreateDirectory(_directory);
        string first = Path.Combine(_directory, "first.pxv");
        string second = Path.Combine(_directory, "second.pxv");
        PxvProjectSerializer serializer = CreateSerializer();
        PixelVoxelProject project = CreateProject();

        await serializer.SaveAsync(first, project, TestContext.Current.CancellationToken);
        await serializer.SaveAsync(second, project, TestContext.Current.CancellationToken);

        Assert.Equal(
            await File.ReadAllBytesAsync(first, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(second, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VersionThreeRoundTripPreservesEditableAnimationFrames()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "animated.pxv");
        PixelVoxelProject basis = CreateProject();
        PixelVoxelProjectFrame[] frames =
        [
            new("idle_0000", 80, basis.Document, basis.SourceViews),
            new(
                "idle_0001",
                120,
                new VoxelDocument(
                    new VoxelDimensions(2, 1, 1),
                    [new VoxelEntry(new VoxelCoordinate(0, 0, 0),
                        VoxelCell.CreateUniform(new Rgba32Color(90, 80, 70, 128)))]),
                basis.SourceViews),
        ];
        PixelVoxelProject source = new(frames, 1, basis.Settings, basis.Palette);

        await CreateSerializer().SaveAsync(path, source, TestContext.Current.CancellationToken);
        PixelVoxelProject loaded = await CreateSerializer().LoadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(2, loaded.Frames.Count);
        Assert.Equal(1, loaded.CurrentFrameIndex);
        Assert.Equal([80, 120], loaded.Frames.Select(frame => frame.DurationMilliseconds));
        Assert.Equal(["idle_0000", "idle_0001"], loaded.Frames.Select(frame => frame.Name));
        Assert.Equal(1, loaded.Frames[1].Document.Storage.OccupiedCount);
        Assert.True(loaded.Frames[1].Document.Storage.TryGetCell(
            new VoxelCoordinate(0, 0, 0), out VoxelCell? cell));
        Assert.Equal(new Rgba32Color(90, 80, 70, 128), cell!.GetColor(VoxelFace.Front));
        Assert.NotNull(loaded.Frames[0].SourceViews);
        Assert.Same(loaded.Frames[1].Document, loaded.Document);
    }

    [Fact]
    public async Task UnsupportedVersionIsRejectedBeforeApplicationStateCanChange()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "future.pxv");
        await using (FileStream stream = File.Create(path))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json");
            await using Stream entryStream = entry.Open();
            await JsonSerializer.SerializeAsync(
                entryStream,
                new
                {
                    format = "PixelVoxel",
                    version = 99,
                    coordinateSystem = "XYZ-RightUpFront-v1",
                    width = 1,
                    height = 1,
                    depth = 1,
                    views = Array.Empty<string>(),
                    settings = CreateProject().Settings,
                },
                cancellationToken: TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateSerializer().LoadAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VersionOneProjectLoadsWithEmptyPalette()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "legacy.pxv");
        await CreateSerializer().SaveAsync(path, CreateProject(), TestContext.Current.CancellationToken);
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            ZipArchiveEntry documentEntry = archive.GetEntry("document.bin")!;
            byte[] document;
            using (Stream input = documentEntry.Open())
            using (MemoryStream buffer = new()) { input.CopyTo(buffer); document = buffer.ToArray(); }
            BitConverter.GetBytes(1).CopyTo(document, 4);
            documentEntry.Delete();
            using (Stream output = archive.CreateEntry("document.bin").Open()) output.Write(document);

            ZipArchiveEntry manifestEntry = archive.GetEntry("manifest.json")!;
            manifestEntry.Delete();
            await using Stream manifest = archive.CreateEntry("manifest.json").Open();
            await JsonSerializer.SerializeAsync(manifest, new
            {
                format = "PixelVoxel", version = 1, coordinateSystem = "XYZ-RightUpFront-v1",
                width = 2, height = 1, depth = 1, views = new[] { "front" }, settings = CreateProject().Settings,
            }, cancellationToken: TestContext.Current.CancellationToken);
        }

        PixelVoxelProject loaded = await CreateSerializer().LoadAsync(path, TestContext.Current.CancellationToken);
        Assert.Empty(loaded.Palette);
        Assert.Equal(1, loaded.Document.Storage.OccupiedCount);
    }

    [Fact]
    public async Task CancelledSaveRemovesTemporaryArchive()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "cancelled.pxv");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSerializer().SaveAsync(path, CreateProject(), cancellation.Token));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static PxvProjectSerializer CreateSerializer() =>
        new(new PngPixelWriter(), new PngPixelReader());

    private static PixelVoxelProject CreateProject()
    {
        Rgba32Color color = new(10, 20, 30, 255);
        VoxelDocument document = new(
            new VoxelDimensions(2, 1, 1),
            [new VoxelEntry(new VoxelCoordinate(1, 0, 0), VoxelCell.CreateUniform(color))]);
        OrthographicViewSet views = new(new Dictionary<VoxelFace, OrthographicImage>
        {
            [VoxelFace.Front] = new OrthographicImage(1, 1, [new Rgba32Color(1, 2, 3, 255)]),
        });
        PixelVoxelProjectSettings settings = new(
            "Pixel2To1",
            -45f,
            -30f,
            0f,
            0f,
            new Rgba32Color(20, 24, 32, 255),
            true,
            -45f,
            45f,
            0.35f,
            0.65f,
            true,
            false,
            new Rgba32Color(0, 0, 0, 255),
            8,
            false,
            true,
            17f);
        return new PixelVoxelProject(document, views, settings,
            [new Rgba32Color(12, 34, 56, 255), new Rgba32Color(78, 90, 12, 255)]);
    }
}
