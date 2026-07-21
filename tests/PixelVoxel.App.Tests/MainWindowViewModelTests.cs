using PixelVoxel.App.Services;
using PixelVoxel.App.ViewModels;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;
using PixelVoxel.Export;

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

    [Fact]
    public void ContinuingTheSameDragLeavesAVisualSnapUsingTheRawOrbitPath()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        viewModel.Rotate(4f, -4f);
        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);

        viewModel.Rotate(10f, 0f);

        Assert.Equal(10f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(3f, viewModel.CurrentCameraState.PitchDegrees);
    }

    [Fact]
    public void ReleasingOnAVisualSnapCommitsItAsTheNextDragOrigin()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        viewModel.Rotate(4f, -4f);
        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);

        viewModel.CommitCameraSnap();
        viewModel.Rotate(10f, 0f);

        Assert.Equal(7f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);
    }

    [Theory]
    [InlineData(4, 0f, -90f, -180f, 90f)]
    [InlineData(8, 0f, -45f, -90f, -135f)]
    [InlineData(16, 0f, -22.5f, -45f, -67.5f)]
    public void DirectionSheetYawOrderStartsAtZeroAndAdvancesClockwise(
        int directionCount,
        float first,
        float second,
        float third,
        float fourth)
    {
        float[] yaws = SpriteExportCoordinator.GetClockwiseDirectionYaws(directionCount);

        Assert.Equal(directionCount, yaws.Length);
        Assert.Equal([first, second, third, fourth], yaws.Take(4));
    }

    [Fact]
    public async Task InspectDoesNotReplaceModelAndBlockingDraftPreservesExistingModel()
    {
        string validPath = Path.Combine(_directory, "valid-sheet.png");
        string invalidPath = Path.Combine(_directory, "invalid-sheet.png");
        Directory.CreateDirectory(_directory);
        await WriteSheetAsync(validPath, nonBinaryAlpha: false);
        await WriteSheetAsync(invalidPath, nonBinaryAlpha: true);
        using MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.LoadHorizontalSheetAsync(validPath);

        Assert.True(viewModel.CanApplyImport);
        Assert.Null(viewModel.CurrentMesh);
        await viewModel.ApplyImportDraftAsync();
        VoxelMeshData existingMesh = Assert.IsType<VoxelMeshData>(viewModel.CurrentMesh);

        await viewModel.LoadHorizontalSheetAsync(invalidPath);

        Assert.False(viewModel.CanApplyImport);
        Assert.Same(existingMesh, viewModel.CurrentMesh);
        await viewModel.ApplyImportDraftAsync();
        Assert.Same(existingMesh, viewModel.CurrentMesh);
    }

    [Fact]
    public async Task ViewportEraseStrokeIsOneUndoableEdit()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.NewProjectAsync(TestContext.Current.CancellationToken);
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        PixelRenderLayout layout = Assert.IsType<PixelRenderLayout>(viewModel.CurrentRenderLayout);
        VoxelRenderTransform transform = Assert.IsType<VoxelRenderTransform>(viewModel.CurrentRenderTransform);
        System.Numerics.Vector3 screen = transform.ProjectToScreen(new System.Numerics.Vector3(15.5f, 15.5f, 16f));
        viewModel.SelectedEditTool = VoxelEditTool.Erase;

        Assert.True(viewModel.BeginEditStroke(screen.X, screen.Y, layout.Width, layout.Height, false));
        viewModel.EndEditStroke();

        Assert.Equal(0, viewModel.CurrentDocument!.Storage.OccupiedCount);
        Assert.True(viewModel.CanUndo);
        viewModel.Undo();
        Assert.Equal(1, viewModel.CurrentDocument.Storage.OccupiedCount);
    }

    [Fact]
    public async Task SaveAndReloadClearSessionHistoryAndRestoreCleanDocument()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "editable.pxv");
        using MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.NewProjectAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsProjectDirty);
        Assert.True(await viewModel.SaveProjectAsync(path, TestContext.Current.CancellationToken));
        Assert.False(viewModel.IsProjectDirty);

        viewModel.ResizeWidth = 31;
        viewModel.ResizeVolume(allowClipping: false);
        Assert.True(viewModel.CanUndo);
        Assert.True(viewModel.IsProjectDirty);

        Assert.True(await viewModel.LoadProjectAsync(path, TestContext.Current.CancellationToken));

        Assert.Equal(new VoxelDimensions(32, 32, 32), viewModel.CurrentDocument!.Storage.Dimensions);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.False(viewModel.IsProjectDirty);
    }

    [Fact]
    public async Task InvalidProjectLoadPreservesTheCurrentDocument()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "invalid.pxv");
        await File.WriteAllTextAsync(path, "not a zip", TestContext.Current.CancellationToken);
        using MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.NewProjectAsync(TestContext.Current.CancellationToken);
        VoxelDocument existing = Assert.IsType<VoxelDocument>(viewModel.CurrentDocument);

        Assert.False(await viewModel.LoadProjectAsync(path, TestContext.Current.CancellationToken));

        Assert.Same(existing, viewModel.CurrentDocument);
        Assert.Equal(1, viewModel.CurrentDocument!.Storage.OccupiedCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel() =>
        CreateViewModelCore();

    private MainWindowViewModel CreateViewModelCore()
    {
        PixelArtVoxelRasterizer rasterizer = new();
        VoxelRenderTransformResolver transformResolver = new();
        return new MainWindowViewModel(
            new AsepriteSpriteSheetImporter(),
            new VisualHullVoxelReconstructor(),
            new VoxelSurfaceMesher(),
            rasterizer,
            transformResolver,
            new PixelRenderLayoutResolver(),
            new ViewportSettingsStore(Path.Combine(_directory, "settings.json")),
            new SpriteExportCoordinator(
                rasterizer,
                transformResolver,
                new SpriteSheetExporter(new PngPixelWriter())));
    }

    private static Task WriteSheetAsync(string path, bool nonBinaryAlpha)
    {
        const int width = 12;
        const int height = 2;
        Rgba32Color[] pixels = Enumerable.Repeat(
            new Rgba32Color(240, 240, 240, 255),
            width * height).ToArray();
        if (nonBinaryAlpha)
        {
            pixels[2] = new Rgba32Color(240, 240, 240, 128);
        }

        return new PngPixelWriter().WriteAsync(path, width, height, pixels);
    }
}
