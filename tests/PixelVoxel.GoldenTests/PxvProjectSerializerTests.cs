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
            true);
        return new PixelVoxelProject(document, views, settings);
    }
}
