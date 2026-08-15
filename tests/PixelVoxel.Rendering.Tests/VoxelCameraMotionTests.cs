using System.Numerics;

namespace PixelVoxel.Rendering.Tests;

public sealed class VoxelCameraMotionTests
{
    [Fact]
    public void FaceSnapUsesTwoDegreeDefaultThreshold()
    {
        Assert.Equal(2f, VoxelCameraMotion.DefaultFaceSnapThresholdDegrees);
    }

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
    public void AnimationAngleWraps(float input, float expected)
    {
        Assert.Equal(expected, VoxelCameraMotion.WrapAngle(input));
    }

    [Theory]
    [InlineData(179f, 3f, -178f)]
    [InlineData(-179f, -3f, 178f)]
    public void AnimationWrapsThroughAFullRotation(
        float pitch,
        float delta,
        float expected)
    {
        Assert.Equal(expected, VoxelCameraMotion.WrapAngle(pitch + delta));
    }

    [Theory]
    [InlineData(2f, -2f, 0f, 0f)]
    [InlineData(-88f, 2f, -90f, 0f)]
    [InlineData(92f, -1f, 90f, 0f)]
    [InlineData(179f, 2f, -180f, 0f)]
    public void CameraSnapsToSideFacesWithinDefaultThreshold(
        float yaw,
        float pitch,
        float expectedYaw,
        float expectedPitch)
    {
        VoxelCameraFaceSnap result = VoxelCameraMotion.SnapToFace(yaw, pitch);

        Assert.True(result.IsSnapped);
        Assert.Equal(expectedYaw, result.YawDegrees);
        Assert.Equal(expectedPitch, result.PitchDegrees);
    }

    [Theory]
    [InlineData(37f, -88f, 0f, -90f)]
    [InlineData(52f, -88f, 90f, -90f)]
    [InlineData(-123f, 89f, -90f, 90f)]
    [InlineData(136f, 89f, -180f, 90f)]
    public void CameraSnapsToTopAndBottomWithNearestQuarterTurnRotation(
        float yaw,
        float pitch,
        float expectedYaw,
        float expectedPitch)
    {
        VoxelCameraFaceSnap result = VoxelCameraMotion.SnapToFace(yaw, pitch);

        Assert.True(result.IsSnapped);
        Assert.Equal(expectedYaw, result.YawDegrees);
        Assert.Equal(expectedPitch, result.PitchDegrees);
    }

    [Fact]
    public void CameraReturnsRawRotationAfterLeavingTopFaceThreshold()
    {
        const float rawYaw = 37f;
        float rawPitch = -90f + VoxelCameraMotion.DefaultFaceSnapThresholdDegrees + 1f;

        VoxelCameraFaceSnap result = VoxelCameraMotion.SnapToFace(rawYaw, rawPitch);

        Assert.False(result.IsSnapped);
        Assert.Equal(rawYaw, result.YawDegrees);
        Assert.Equal(rawPitch, result.PitchDegrees);
    }

    [Fact]
    public void CameraDoesNotSnapOutsideTheFaceThreshold()
    {
        float offset = VoxelCameraMotion.DefaultFaceSnapThresholdDegrees + 1f;
        float yaw = 90f - offset;

        VoxelCameraFaceSnap result = VoxelCameraMotion.SnapToFace(yaw, offset);

        Assert.False(result.IsSnapped);
        Assert.Equal(yaw, result.YawDegrees);
        Assert.Equal(offset, result.PitchDegrees);
    }

    [Fact]
    public void SheetFaceSnapFollowsTheRotatedOriginalFaceNormal()
    {
        Quaternion modelRotation = VoxelOrientation.FromYawPitchRoll(30f, 0f, 0f);

        VoxelCameraFaceSnap result = VoxelCameraMotion.SnapToSheetFace(
            -28f, 1f, modelRotation, 5f);

        Assert.True(result.IsSnapped);
        Assert.Equal(-30f, result.YawDegrees, 3);
        Assert.Equal(0f, result.PitchDegrees, 3);
    }

    [Fact]
    public void SheetFaceSnapDoesNotFallBackToAnUnrotatedWorldFace()
    {
        Quaternion modelRotation = VoxelOrientation.FromYawPitchRoll(30f, 0f, 0f);

        VoxelCameraFaceSnap result = VoxelCameraMotion.SnapToSheetFace(
            1f, 0f, modelRotation, 5f);

        Assert.False(result.IsSnapped);
    }

}
