using PixelVoxel.App.Services;
using PixelVoxel.App.ViewModels;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"PixelVoxel.App.Tests-{Guid.NewGuid():N}");

    [Fact]
    public void FirstWheelStepChangesFitToManualUsingTheCurrentFitScale()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        viewModel.ZoomBy(1, currentFitScale: 4);

        Assert.Equal(5, viewModel.ManualZoomScale);
        Assert.Equal("5×", viewModel.ZoomSummary);
        viewModel.FitZoom();
        Assert.Null(viewModel.ManualZoomScale);
    }

    [Fact]
    public void ManualZoomIsLimitedToOneThroughSixteen()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        for (int index = 0; index < 30; index++) viewModel.ZoomBy(1, 4);
        Assert.Equal(16, viewModel.ManualZoomScale);
        for (int index = 0; index < 30; index++) viewModel.ZoomBy(-1, 4);
        Assert.Equal(1, viewModel.ManualZoomScale);
    }

    [Fact]
    public void MouseOrbitAndObjectAnimationKeepIndependentRotationState()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        VoxelCameraState initialCamera = viewModel.CurrentCameraState;
        viewModel.HorizontalAnimationEnabled = true;
        viewModel.VerticalAnimationEnabled = true;

        viewModel.AdvanceAnimations(0.1d);

        Assert.Equal(initialCamera, viewModel.CurrentCameraState);
        Assert.Equal(new VoxelModelRotationState(3f, 3f), viewModel.CurrentModelRotation);
        VoxelModelRotationState animatedRotation = viewModel.CurrentModelRotation;
        viewModel.Rotate(10f, 10f);
        Assert.NotEqual(initialCamera, viewModel.CurrentCameraState);
        Assert.Equal(animatedRotation, viewModel.CurrentModelRotation);
        Assert.True(viewModel.HorizontalAnimationEnabled);
        Assert.True(viewModel.VerticalAnimationEnabled);
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        Assert.Equal(animatedRotation, viewModel.CurrentModelRotation);
        Assert.True(viewModel.HorizontalAnimationEnabled);
        Assert.True(viewModel.VerticalAnimationEnabled);
    }

    [Fact]
    public void DisablingEachAnimationResetsOnlyItsModelRotationAxis()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        VoxelCameraState initialCamera = viewModel.CurrentCameraState;
        viewModel.HorizontalAnimationEnabled = true;
        viewModel.VerticalAnimationEnabled = true;
        viewModel.AdvanceAnimations(0.1d);

        viewModel.HorizontalAnimationEnabled = false;

        Assert.Equal(new VoxelModelRotationState(0f, 3f), viewModel.CurrentModelRotation);
        Assert.Equal(initialCamera, viewModel.CurrentCameraState);
        Assert.True(viewModel.VerticalAnimationEnabled);

        viewModel.VerticalAnimationEnabled = false;

        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);
        Assert.Equal(initialCamera, viewModel.CurrentCameraState);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel() =>
        new(
            new AsepriteSpriteSheetImporter(),
            new VisualHullVoxelReconstructor(),
            new VoxelSurfaceMesher(),
            new PixelArtVoxelRasterizer(),
            new VoxelRenderTransformResolver(),
            new PixelRenderLayoutResolver(),
            new ViewportSettingsStore(Path.Combine(_directory, "settings.json")));
}
