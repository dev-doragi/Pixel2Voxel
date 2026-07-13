using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class VoxelFaceTests
{
    [Fact]
    public void DefinesTheSixNamedOrthographicFaces()
    {
        VoxelFace[] expected =
        [
            VoxelFace.Front,
            VoxelFace.Back,
            VoxelFace.Left,
            VoxelFace.Right,
            VoxelFace.Top,
            VoxelFace.Bottom,
        ];

        Assert.Equal(expected, Enum.GetValues<VoxelFace>());
    }
}

