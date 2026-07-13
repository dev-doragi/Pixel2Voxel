using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void DimensionCoordinateAndColorUseValueEquality()
    {
        Assert.Equal(new VoxelDimensions(16, 24, 32), new VoxelDimensions(16, 24, 32));
        Assert.Equal(new VoxelCoordinate(1, 2, 3), new VoxelCoordinate(1, 2, 3));
        Assert.Equal(new Rgba32Color(10, 20, 30, 255), new Rgba32Color(10, 20, 30, 255));
    }
}

