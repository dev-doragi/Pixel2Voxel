namespace PixelVoxel.Rendering.Tests;

public sealed class VoxelCameraMotionTests
{
    [Fact]
    public void OrbitDragUsesReversedHorizontalAndExistingVerticalDirections()
    {
        Assert.Equal(-38f, VoxelCameraMotion.RotateYaw(-45f, 10f, 0.7f));
        Assert.Equal(-37f, VoxelCameraMotion.RotatePitch(-30f, 10f, 0.7f));
    }

    [Theory]
    [InlineData(1000f, -89f)]
    [InlineData(-1000f, 89f)]
    public void OrbitPitchIsClamped(float deltaY, float expected)
    {
        float actual = VoxelCameraMotion.RotatePitch(0f, deltaY, 0.7f);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RenderAnglesSnapToWholeDegrees()
    {
        Assert.Equal(13f, VoxelCameraMotion.Snap(12.6f));
        Assert.Equal(-13f, VoxelCameraMotion.Snap(-12.6f));
    }

    [Theory]
    [InlineData(181f, -179f)]
    [InlineData(-181f, 179f)]
    [InlineData(540f, -180f)]
    public void HorizontalAnimationWraps(float input, float expected)
    {
        Assert.Equal(expected, VoxelCameraMotion.WrapAngle(input));
    }

    [Theory]
    [InlineData(179f, 3f, -178f)]
    [InlineData(-179f, -3f, 178f)]
    public void VerticalAnimationWrapsThroughAFullRotation(
        float pitch,
        float delta,
        float expected)
    {
        Assert.Equal(expected, VoxelCameraMotion.WrapAngle(pitch + delta));
    }
}
