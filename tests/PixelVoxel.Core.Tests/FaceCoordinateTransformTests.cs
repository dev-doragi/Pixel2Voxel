using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class FaceCoordinateTransformTests
{
    [Fact]
    public void DefinesTheV1ImageToModelTransformTable()
    {
        Assert.Equal(
            new FaceCoordinateTransform(VoxelFace.Front, Axis.X, Axis.Y, false, true),
            FaceCoordinateTransforms.Get(VoxelFace.Front));
        Assert.Equal(
            new FaceCoordinateTransform(VoxelFace.Back, Axis.X, Axis.Y, true, true),
            FaceCoordinateTransforms.Get(VoxelFace.Back));
        Assert.Equal(
            new FaceCoordinateTransform(VoxelFace.Right, Axis.Z, Axis.Y, true, true),
            FaceCoordinateTransforms.Get(VoxelFace.Right));
        Assert.Equal(
            new FaceCoordinateTransform(VoxelFace.Left, Axis.Z, Axis.Y, false, true),
            FaceCoordinateTransforms.Get(VoxelFace.Left));
        Assert.Equal(
            new FaceCoordinateTransform(VoxelFace.Top, Axis.X, Axis.Z, false, false),
            FaceCoordinateTransforms.Get(VoxelFace.Top));
        Assert.Equal(
            new FaceCoordinateTransform(VoxelFace.Bottom, Axis.X, Axis.Z, false, true),
            FaceCoordinateTransforms.Get(VoxelFace.Bottom));
    }
}
