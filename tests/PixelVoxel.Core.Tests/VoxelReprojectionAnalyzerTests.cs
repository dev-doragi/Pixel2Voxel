using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class VoxelReprojectionAnalyzerTests
{
    [Fact]
    public void ReportsSourcePixelsRemovedByConflictingSilhouettes()
    {
        Rgba32Color clear = default;
        Rgba32Color solid = new(255, 255, 255, 255);
        OrthographicImage front = new(3, 1, [solid, clear, clear]);
        OrthographicImage back = new(3, 1, [solid, clear, clear]);
        OrthographicViewSet views = new(new Dictionary<VoxelFace, OrthographicImage>
        {
            [VoxelFace.Front] = front,
            [VoxelFace.Back] = back,
        });
        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(views);

        VoxelReprojectionAnalysis result = new VoxelReprojectionAnalyzer().Analyze(views, document);

        Assert.Equal(2, result.ConflictCount);
        Assert.Equal(1, result.GetConflictCount(VoxelFace.Front));
        Assert.All(result.Conflicts, conflict =>
        {
            Assert.True(conflict.SourceOccupied);
            Assert.False(conflict.ReprojectedOccupied);
        });
    }

    [Fact]
    public void ReportsNoConflictWhenAReconstructionMatchesItsSource()
    {
        OrthographicViewSet views = new(new Dictionary<VoxelFace, OrthographicImage>
        {
            [VoxelFace.Front] = new(2, 1,
            [
                new Rgba32Color(1, 2, 3, 1),
                new Rgba32Color(4, 5, 6, 255),
            ]),
        });
        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(views);

        Assert.Empty(new VoxelReprojectionAnalyzer().Analyze(views, document).Conflicts);
    }

    [Fact]
    public void SourceMaskPixelCanBeEditedWithoutMutatingTheOriginal()
    {
        OrthographicImage original = new(1, 1, [new Rgba32Color(1, 2, 3, 255)]);

        OrthographicImage edited = original.WithPixel(0, 0, default);

        Assert.Equal((byte)255, original.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)0, edited.GetPixel(0, 0).Alpha);
    }
}
