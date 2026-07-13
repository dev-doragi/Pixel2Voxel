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
}
