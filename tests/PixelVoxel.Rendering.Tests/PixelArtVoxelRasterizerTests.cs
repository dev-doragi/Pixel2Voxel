using PixelVoxel.Core;

namespace PixelVoxel.Rendering.Tests;

public sealed class PixelArtVoxelRasterizerTests
{
    [Fact]
    public void UsesTheImportedPixelCanvasAsTheMinimumFramebufferSize()
    {
        VoxelMeshData mesh = new VoxelSurfaceMesher()
            .Build(
                VoxelSurfaceMesherTests.CreateSingleVoxelDocument(),
                TestContext.Current.CancellationToken)
            .Mesh!;

        PixelFramebuffer frame = new PixelArtVoxelRasterizer().Render(
            mesh,
            VoxelCameraState.Pixel2To1(),
            sourcePixelWidth: 32,
            sourcePixelHeight: 24);

        Assert.Equal(32, frame.Width);
        Assert.Equal(24, frame.Height);
    }

    [Fact]
    public void WritesOnlyExactSourceFaceColorsOrTheBackground()
    {
        VoxelMeshData mesh = new VoxelSurfaceMesher()
            .Build(
                VoxelSurfaceMesherTests.CreateSingleVoxelDocument(),
                TestContext.Current.CancellationToken)
            .Mesh!;
        Rgba32Color background = new(1, 2, 3, 255);

        PixelFramebuffer frame = new PixelArtVoxelRasterizer().Render(
            mesh,
            VoxelCameraState.Pixel2To1(),
            sourcePixelWidth: 8,
            sourcePixelHeight: 8,
            background: background);

        HashSet<Rgba32Color> allowed = Enum.GetValues<VoxelFace>()
            .Select(VoxelSurfaceMesherTests.FaceColor)
            .Append(background)
            .ToHashSet();

        Assert.All(frame.Pixels.ToArray(), pixel => Assert.Contains(pixel, allowed));
        Assert.Contains(frame.Pixels.ToArray(), pixel => pixel != background);
    }

    [Fact]
    public void FillsAProjectedFaceWithoutPointSamplingGaps()
    {
        Dictionary<VoxelCoordinate, VoxelCell> cells = Enumerable.Range(0, 6)
            .ToDictionary(
                x => new VoxelCoordinate(x, 0, 0),
                _ => CreateCell());
        VoxelMeshData mesh = BuildMesh(new VoxelDimensions(6, 1, 1), cells);
        Rgba32Color background = new(1, 2, 3, 255);
        PixelFramebuffer frame = new PixelArtVoxelRasterizer().Render(
            mesh,
            CreateFrontCamera(),
            sourcePixelWidth: 6,
            sourcePixelHeight: 1,
            background: background);

        Rgba32Color[] pixels = frame.Pixels.ToArray();
        (int X, int Y)[] coloredPixels = pixels
            .Select((pixel, index) => (pixel, index))
            .Where(item => item.pixel != background)
            .Select(item => (item.index % frame.Width, item.index / frame.Width))
            .ToArray();

        Assert.Equal(6, coloredPixels.Length);
        Assert.Single(coloredPixels.Select(pixel => pixel.Y).Distinct());
        Assert.Equal(
            Enumerable.Range(coloredPixels.Min(pixel => pixel.X), 6),
            coloredPixels.Select(pixel => pixel.X).Order());
    }

    [Fact]
    public void KeepsTheSameRotationSafeCanvasForDifferentAngles()
    {
        Dictionary<VoxelCoordinate, VoxelCell> cells = Enumerable.Range(0, 6)
            .ToDictionary(
                x => new VoxelCoordinate(x, 0, 0),
                _ => CreateCell());
        VoxelMeshData mesh = BuildMesh(new VoxelDimensions(6, 1, 1), cells);

        PixelArtVoxelRasterizer rasterizer = new();
        PixelFramebuffer front = rasterizer.Render(
            mesh,
            CreateFrontCamera(),
            sourcePixelWidth: 2,
            sourcePixelHeight: 2);
        PixelFramebuffer isometric = rasterizer.Render(
            mesh,
            VoxelCameraState.Pixel2To1(),
            sourcePixelWidth: 2,
            sourcePixelHeight: 2);

        Assert.Equal(front.Width, isometric.Width);
        Assert.Equal(front.Height, isometric.Height);
        Assert.True(front.Width >= 6);
        Assert.True(front.Height >= 6);
    }

    private static VoxelCameraState CreateFrontCamera() =>
        new(
            VoxelViewMode.PixelPreview,
            VoxelCameraPreset.Front,
            0f,
            0f,
            0f,
            0f,
            1f);

    private static VoxelCell CreateCell() =>
        new(Enum.GetValues<VoxelFace>()
            .ToDictionary(face => face, VoxelSurfaceMesherTests.FaceColor));

    private static VoxelMeshData BuildMesh(
        VoxelDimensions dimensions,
        IReadOnlyDictionary<VoxelCoordinate, VoxelCell> cells) =>
        new VoxelSurfaceMesher()
            .Build(
                new VoxelDocument(new TestVoxelStorage(dimensions, cells)),
                TestContext.Current.CancellationToken)
            .Mesh!;

    private sealed class TestVoxelStorage : IVoxelStorage
    {
        private readonly IReadOnlyDictionary<VoxelCoordinate, VoxelCell> _cells;

        public TestVoxelStorage(
            VoxelDimensions dimensions,
            IReadOnlyDictionary<VoxelCoordinate, VoxelCell> cells)
        {
            Dimensions = dimensions;
            _cells = cells;
        }

        public VoxelDimensions Dimensions { get; }

        public int OccupiedCount => _cells.Count;

        public bool TryGetCell(VoxelCoordinate coordinate, out VoxelCell? cell) =>
            _cells.TryGetValue(coordinate, out cell);

        public IEnumerable<VoxelEntry> GetOccupiedCells() =>
            _cells.Select(pair => new VoxelEntry(pair.Key, pair.Value));
    }
}
