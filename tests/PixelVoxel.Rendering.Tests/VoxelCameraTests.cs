using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering.Tests;

public sealed class VoxelCameraTests
{
    [Fact]
    public void PixelTwoToOnePresetProjectsHorizontalAxisAtOneHalfSlope()
    {
        PixelRenderSettings settings = new();
        VoxelRenderTransform transform = new VoxelRenderTransformResolver().Resolve(
            VoxelCameraState.Pixel2To1(),
            new VoxelDimensions(1, 1, 1),
            settings);

        Vector3 origin = transform.ProjectToScreen(Vector3.Zero);
        Vector3 xAxis = transform.ProjectToScreen(Vector3.UnitX);
        float slope = MathF.Abs((xAxis.Y - origin.Y) / (xAxis.X - origin.X));

        Assert.Equal(0.5f, slope, 4);
    }

    [Fact]
    public void PixelPreviewSnapsTheFramebufferAnchorButFreeViewDoesNot()
    {
        PixelRenderSettings settings = new();
        VoxelDimensions dimensions = new(1, 1, 1);
        VoxelRenderTransformResolver resolver = new();
        VoxelCameraState preview = VoxelCameraState.Pixel2To1() with { PanX = 0.4f, PanY = 0.4f };
        VoxelCameraState free = preview with
        {
            Mode = VoxelViewMode.FreeView,
            Preset = VoxelCameraPreset.Free,
        };

        VoxelRenderTransform snapped = resolver.Resolve(preview, dimensions, settings);
        VoxelRenderTransform unsnapped = resolver.Resolve(free, dimensions, settings);

        Assert.Equal(160f, snapped.ScreenOffsetX);
        Assert.Equal(90f, snapped.ScreenOffsetY);
        Assert.Equal(160.4f, unsnapped.ScreenOffsetX, 3);
        Assert.Equal(90.4f, unsnapped.ScreenOffsetY, 3);
    }

    [Fact]
    public void ModelAndCameraRotationsRemainIndependentUntilFrameComposition()
    {
        PixelRenderSettings settings = new();
        VoxelDimensions dimensions = new(2, 3, 4);
        VoxelRenderTransformResolver resolver = new();
        VoxelCameraState camera = VoxelCameraState.Pixel2To1() with
        {
            Mode = VoxelViewMode.FreeView,
            Preset = VoxelCameraPreset.Free,
            YawDegrees = 15f,
            PitchDegrees = -10f,
        };
        VoxelModelRotationState modelRotation = new(40f, 25f);

        VoxelRenderTransform initial = resolver.Resolve(
            camera,
            modelRotation,
            dimensions,
            settings);
        VoxelRenderTransform movedCamera = resolver.Resolve(
            camera with { YawDegrees = 55f },
            modelRotation,
            dimensions,
            settings);
        VoxelRenderTransform rotatedModel = resolver.Resolve(
            camera,
            modelRotation with { PitchDegrees = 70f },
            dimensions,
            settings);

        Assert.Equal(initial.ModelRotation, movedCamera.ModelRotation);
        Assert.NotEqual(initial.ViewRotation, movedCamera.ViewRotation);
        Assert.Equal(initial.ViewRotation, rotatedModel.ViewRotation);
        Assert.NotEqual(initial.ModelRotation, rotatedModel.ModelRotation);
    }

    [Fact]
    public void ModelRollUsesTheObjectsLocalForwardAxis()
    {
        PixelRenderSettings settings = new();
        VoxelDimensions dimensions = new(4, 4, 4);
        VoxelCameraState camera = new(
            VoxelViewMode.FreeView,
            VoxelCameraPreset.Free,
            37f,
            -24f,
            0f,
            0f,
            1f);
        VoxelRenderTransformResolver resolver = new();
        VoxelRenderTransform unrolled = resolver.Resolve(
            camera,
            VoxelModelRotationState.Identity,
            dimensions,
            settings);
        VoxelRenderTransform rolled = resolver.Resolve(
            camera,
            new VoxelModelRotationState(0f, 0f, 90f),
            dimensions,
            settings);
        Vector3 center = new(2f, 2f, 2f);
        Vector3 point = center + Vector3.UnitX;
        Vector2 before = ToScreenDelta(unrolled, center, point);
        Vector2 after = ToScreenDelta(rolled, center, point);

        Assert.True(before.Length() > 0f);
        Assert.True(after.Length() > 0f);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void OrientationsExposeNormalizedOrthonormalBasis()
    {
        Quaternion orientation = VoxelOrientation.FromYawPitchRoll(37f, -24f, 83f);
        VoxelOrientationBasis basis = VoxelOrientation.GetBasis(orientation);

        Assert.Equal(1f, orientation.Length(), 5);
        Assert.Equal(1f, basis.Right.Length(), 5);
        Assert.Equal(1f, basis.Up.Length(), 5);
        Assert.Equal(1f, basis.Forward.Length(), 5);
        Assert.Equal(0f, Vector3.Dot(basis.Right, basis.Up), 5);
        Assert.Equal(0f, Vector3.Dot(basis.Up, basis.Forward), 5);
        Assert.Equal(0f, Vector3.Dot(basis.Forward, basis.Right), 5);
    }

    [Fact]
    public void ModelViewInverseRoundTripsArbitraryDirection()
    {
        VoxelRenderTransform transform = new VoxelRenderTransformResolver().Resolve(
            new VoxelCameraState(VoxelViewMode.FreeView, VoxelCameraPreset.Free, 31f, -22f, 0f, 0f, 1f),
            new VoxelModelRotationState(17f, 29f, 41f),
            new VoxelDimensions(3, 4, 5),
            new PixelRenderSettings());
        Vector3 direction = Vector3.Normalize(new Vector3(2f, 3f, 4f));

        Vector3 view = Vector3.TransformNormal(direction, transform.ModelView);
        Vector3 restored = Vector3.TransformNormal(view, transform.InverseModelView);

        Assert.Equal(direction.X, restored.X, 5);
        Assert.Equal(direction.Y, restored.Y, 5);
        Assert.Equal(direction.Z, restored.Z, 5);
    }

    [Fact]
    public void WorldLightingNormalUsesModelRotationButNotCameraRotation()
    {
        PixelRenderSettings settings = new();
        VoxelDimensions dimensions = new(1, 1, 1);
        VoxelRenderTransformResolver resolver = new();
        VoxelModelRotationState modelRotation = new(90f, 0f);
        VoxelRenderTransform frontCamera = resolver.Resolve(
            VoxelCameraState.Pixel2To1(),
            modelRotation,
            dimensions,
            settings);
        VoxelRenderTransform rightCamera = resolver.Resolve(
            VoxelCameraState.Pixel2To1() with { Preset = VoxelCameraPreset.Right },
            modelRotation,
            dimensions,
            settings);

        Vector3 frontNormal = frontCamera.TransformNormalToWorld(Vector3.UnitZ);
        Vector3 rightNormal = rightCamera.TransformNormalToWorld(Vector3.UnitZ);

        Assert.Equal(frontNormal.X, rightNormal.X, 5);
        Assert.Equal(frontNormal.Y, rightNormal.Y, 5);
        Assert.Equal(frontNormal.Z, rightNormal.Z, 5);
        Assert.Equal(1f, MathF.Abs(frontNormal.X), 5);
    }

    private static Vector2 ToScreenDelta(
        VoxelRenderTransform transform,
        Vector3 center,
        Vector3 point)
    {
        Vector3 projectedCenter = transform.ProjectToScreen(center);
        Vector3 projectedPoint = transform.ProjectToScreen(point);
        return new Vector2(
            projectedPoint.X - projectedCenter.X,
            projectedPoint.Y - projectedCenter.Y);
    }
}
