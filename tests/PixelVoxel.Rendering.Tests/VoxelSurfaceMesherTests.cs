using PixelVoxel.Core;

namespace PixelVoxel.Rendering.Tests;

public sealed class VoxelSurfaceMesherTests
{
    [Fact]
    public void BuildsFourIndependentColoredVerticesPerExposedFace()
    {
        VoxelDocument document = CreateSingleVoxelDocument();

        VoxelMeshBuildResult result = new VoxelSurfaceMesher().Build(
            document,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.ActualExposedFaceCount);
        Assert.Equal(24, result.Mesh!.Vertices.Length);
        Assert.Equal(36, result.Mesh.Indices.Length);
        Assert.Equal(6, result.Mesh.FaceNormals.Length);
        Assert.Equal(6, result.Mesh.Faces.Length);

        Rgba32Color[] expectedColors =
        [
            FaceColor(VoxelFace.Front),
            FaceColor(VoxelFace.Back),
            FaceColor(VoxelFace.Right),
            FaceColor(VoxelFace.Left),
            FaceColor(VoxelFace.Top),
            FaceColor(VoxelFace.Bottom),
        ];
        System.Numerics.Vector3[] expectedNormals =
        [
            System.Numerics.Vector3.UnitZ,
            -System.Numerics.Vector3.UnitZ,
            System.Numerics.Vector3.UnitX,
            -System.Numerics.Vector3.UnitX,
            System.Numerics.Vector3.UnitY,
            -System.Numerics.Vector3.UnitY,
        ];
        ReadOnlySpan<VoxelMeshVertex> vertices = result.Mesh.Vertices.Span;
        for (int faceIndex = 0; faceIndex < expectedColors.Length; faceIndex++)
        {
            Assert.All(
                vertices.Slice(faceIndex * 4, 4).ToArray(),
                vertex =>
                {
                    Assert.Equal(expectedColors[faceIndex], vertex.Color);
                    Assert.Equal(expectedNormals[faceIndex], vertex.Normal);
                });
        }
    }

    [Fact]
    public void RemovesTheSharedFaceBetweenAdjacentCells()
    {
        VoxelDocument document = CreateDocument(
            new VoxelCoordinate(0, 0, 0),
            new VoxelCoordinate(1, 0, 0));

        VoxelMeshBuildResult result = new VoxelSurfaceMesher().Build(
            document,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.ActualExposedFaceCount);
    }

    [Fact]
    public void PreservesTheDocumentWhenTheExposedFaceLimitIsExceeded()
    {
        const int occupiedCount = 83_334;
        Dictionary<VoxelCoordinate, VoxelCell> cells = new(occupiedCount);
        for (int index = 0; index < occupiedCount; index++)
        {
            cells.Add(new VoxelCoordinate(index * 2, 0, 0), CreateCell());
        }

        VoxelDocument document = new(new TestVoxelStorage(
            new VoxelDimensions((occupiedCount * 2) - 1, 1, 1),
            cells));

        VoxelMeshBuildResult result = new VoxelSurfaceMesher().Build(
            document,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Mesh);
        Assert.Equal(500_004, result.ActualExposedFaceCount);
        Assert.Equal(occupiedCount, document.Storage.OccupiedCount);
    }

    internal static VoxelDocument CreateSingleVoxelDocument() =>
        CreateDocument(new VoxelCoordinate(0, 0, 0));

    internal static Rgba32Color FaceColor(VoxelFace face) =>
        new((byte)(20 + ((int)face * 20)), (byte)(10 + (int)face), 5, 255);

    private static VoxelDocument CreateDocument(params VoxelCoordinate[] coordinates)
    {
        Dictionary<VoxelCoordinate, VoxelCell> cells = coordinates.ToDictionary(
            coordinate => coordinate,
            coordinate => CreateCell());
        int width = coordinates.Max(coordinate => coordinate.X) + 1;
        return new VoxelDocument(new TestVoxelStorage(new VoxelDimensions(width, 1, 1), cells));
    }

    private static VoxelCell CreateCell() =>
        new(Enum.GetValues<VoxelFace>().ToDictionary(face => face, FaceColor));

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
