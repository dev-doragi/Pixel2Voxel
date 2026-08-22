using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class VisualHullVoxelReconstructorTests
{
    [Theory]
    [InlineData(1, 2, 2, 1, 4)]
    [InlineData(2, 2, 2, 2, 8)]
    [InlineData(3, 2, 2, 2, 8)]
    [InlineData(6, 2, 2, 2, 8)]
    public void IntersectsEverySuppliedViewWithoutRequiringSix(
        int viewCount,
        int expectedWidth,
        int expectedHeight,
        int expectedDepth,
        int expectedOccupied)
    {
        VoxelFace[] faces =
        [
            VoxelFace.Front,
            VoxelFace.Right,
            VoxelFace.Top,
            VoxelFace.Back,
            VoxelFace.Left,
            VoxelFace.Bottom,
        ];
        Dictionary<VoxelFace, OrthographicImage> views = faces
            .Take(viewCount)
            .ToDictionary(face => face, face => CreateSolidImage(2, 2, (byte)(10 + (int)face)));

        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(
            new OrthographicViewSet(views));

        Assert.Equal(
            new VoxelDimensions(expectedWidth, expectedHeight, expectedDepth),
            document.Storage.Dimensions);
        Assert.Equal(expectedOccupied, document.Storage.OccupiedCount);
    }

    [Fact]
    public void UsesExplicitLengthForAnAxisMissingFromTheSelectedViews()
    {
        OrthographicViewSet frontOnly = new(
            new Dictionary<VoxelFace, OrthographicImage>
            {
                [VoxelFace.Front] = CreateSolidImage(2, 3, 40),
            });

        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(
            frontOnly,
            new VoxelReconstructionOptions(7, 8, 5));

        Assert.Equal(new VoxelDimensions(2, 3, 5), document.Storage.Dimensions);
        Assert.Equal(30, document.Storage.OccupiedCount);
        Assert.True(document.Storage.TryGetCell(new VoxelCoordinate(0, 0, 0), out VoxelCell? cell));
        Assert.All(Enum.GetValues<VoxelFace>(), face =>
            Assert.Equal(new Rgba32Color(40, 0, 0, 255), cell!.GetColor(face)));
    }

    [Fact]
    public void RejectsInvalidUnobservedAxisLengths()
    {
        OrthographicViewSet frontOnly = new(
            new Dictionary<VoxelFace, OrthographicImage>
            {
                [VoxelFace.Front] = CreateSolidImage(1, 1, 40),
            });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VisualHullVoxelReconstructor().Reconstruct(
                frontOnly,
                new VoxelReconstructionOptions(1, 1, 0)));
    }

    [Fact]
    public void UsesTheUnionOfObservedAxisRanges()
    {
        Rgba32Color transparent = new(0, 0, 0, 0);
        Rgba32Color opaque = new(255, 255, 255, 255);
        Rgba32Color[] front = Enumerable.Repeat(transparent, 25).ToArray();
        Rgba32Color[] back = Enumerable.Repeat(transparent, 25).ToArray();
        front[(1 * 5) + 1] = opaque;
        back[(1 * 5) + 1] = opaque;

        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(
            new OrthographicViewSet(
                new Dictionary<VoxelFace, OrthographicImage>
                {
                    [VoxelFace.Front] = new(5, 5, front),
                    [VoxelFace.Back] = new(5, 5, back),
                }));

        Assert.Equal(new VoxelDimensions(3, 1, 1), document.Storage.Dimensions);
        Assert.Equal(0, document.Storage.OccupiedCount);
    }

    [Fact]
    public void SamplesAllFacesThroughTheCentralTransformTable()
    {
        Dictionary<VoxelFace, OrthographicImage> views = Enum.GetValues<VoxelFace>()
            .ToDictionary(face => face, face => CreatePatternImage((byte)(20 * ((int)face + 1))));
        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(
            new OrthographicViewSet(views));

        Assert.True(document.Storage.TryGetCell(new VoxelCoordinate(0, 0, 0), out VoxelCell? cell));
        Assert.Equal(Pattern(20, 0, 1), cell!.GetColor(VoxelFace.Front));
        Assert.Equal(Pattern(40, 1, 1), cell.GetColor(VoxelFace.Back));
        Assert.Equal(Pattern(60, 0, 1), cell.GetColor(VoxelFace.Left));
        Assert.Equal(Pattern(80, 1, 1), cell.GetColor(VoxelFace.Right));
        Assert.Equal(Pattern(100, 0, 0), cell.GetColor(VoxelFace.Top));
        Assert.Equal(Pattern(120, 0, 1), cell.GetColor(VoxelFace.Bottom));
    }

    [Fact]
    public void PreservesPartialAlphaAndTreatsItAsOccupied()
    {
        OrthographicViewSet views = new(
            new Dictionary<VoxelFace, OrthographicImage>
            {
                [VoxelFace.Front] = new(1, 1, [new Rgba32Color(1, 2, 3, 128)]),
            });

        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(views);

        Assert.Equal(1, document.Storage.OccupiedCount);
        Assert.True(document.Storage.TryGetCell(new VoxelCoordinate(0, 0, 0), out VoxelCell? cell));
        Assert.Equal(new Rgba32Color(1, 2, 3, 128), cell!.GetColor(VoxelFace.Front));
    }

    [Fact]
    public void RejectsEmptySourceView()
    {
        OrthographicViewSet views = new(
            new Dictionary<VoxelFace, OrthographicImage>
            {
                [VoxelFace.Front] = new(1, 1, [new Rgba32Color(0, 0, 0, 0)]),
            });

        Assert.Throws<ArgumentException>(() =>
            new VisualHullVoxelReconstructor().Reconstruct(views));
    }

    [Fact]
    public void RejectsCandidateVolumeAboveTheLimit()
    {
        Rgba32Color[] pixels = Enumerable.Repeat(
            new Rgba32Color(255, 255, 255, 255),
            1025 * 1025).ToArray();
        OrthographicViewSet views = new(
            new Dictionary<VoxelFace, OrthographicImage>
            {
                [VoxelFace.Front] = new(1025, 1025, pixels),
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new VisualHullVoxelReconstructor().Reconstruct(views));

        Assert.Contains("1,048,576", exception.Message);
    }

    [Fact]
    public void RejectsViewsThatDoNotShareACanvas()
    {
        OrthographicViewSet views = new(
            new Dictionary<VoxelFace, OrthographicImage>
            {
                [VoxelFace.Front] = CreateSolidImage(2, 1, 1),
                [VoxelFace.Top] = CreateSolidImage(3, 1, 2),
            });

        Assert.Throws<ArgumentException>(() =>
            new VisualHullVoxelReconstructor().Reconstruct(views));
    }

    private static OrthographicImage CreateSolidImage(int width, int height, byte red) =>
        new(
            width,
            height,
            Enumerable.Repeat(new Rgba32Color(red, 0, 0, 255), width * height));

    private static OrthographicImage CreatePatternImage(byte start)
    {
        Rgba32Color[] pixels = new Rgba32Color[4];
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                pixels[(y * 2) + x] = Pattern(start, x, y);
            }
        }

        return new OrthographicImage(2, 2, pixels);
    }

    private static Rgba32Color Pattern(byte start, int x, int y) =>
        new((byte)(start + x), (byte)y, 0, 255);
}
