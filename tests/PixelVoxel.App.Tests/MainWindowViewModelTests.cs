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

    [Theory]
    [InlineData(40d, 4d, 1)]
    [InlineData(-40d, 4d, -1)]
    [InlineData(4d, 40d, 1)]
    [InlineData(4d, -40d, -1)]
    public void GizmoDirectionalDragMapsOnlyPointerMovementInAllFourDirections(
        double deltaX,
        double deltaY,
        int expectedSign)
    {
        float degrees = GizmoDragMotion.GetDegrees(deltaX, deltaY);

        Assert.Equal(expectedSign, Math.Sign(degrees));
        Assert.Equal(28f, Math.Abs(degrees), 3);
    }

    [Fact]
    public void StationaryGizmoPointerProducesNoRotation()
    {
        Assert.Equal(0f, GizmoDragMotion.GetDegrees(0d, 0d));
    }

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
    public void WindowTitleUsesThePixel2VoxelBrand()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        Assert.StartsWith("Pixel2Voxel", viewModel.WindowTitle);
    }

    [Fact]
    public void ViewModeIsDefaultAndFreeViewCanBeResetToStandardRotation()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        Assert.Equal(VoxelEditTool.View, viewModel.SelectedEditTool);
        Assert.True(viewModel.IsViewMode);
        Assert.True(viewModel.IsViewTool);
        Assert.False(viewModel.IsPaintTool);

        viewModel.EnterFreeView();
        Assert.True(viewModel.CameraFaceSnapEnabled);
        viewModel.Rotate(20f, 10f);
        Assert.Equal(VoxelViewMode.FreeView, viewModel.CurrentCameraState.Mode);

        viewModel.YawAnimationEnabled = true;
        viewModel.ToggleAnimationPreview();
        viewModel.AdvanceAnimations(0.1d);
        viewModel.ResetView();

        Assert.Equal(VoxelCameraState.Pixel2To1(), viewModel.CurrentCameraState);
        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);
        Assert.False(viewModel.IsAnimationActive);
    }

    [Fact]
    public void ImportDrawerAlwaysExposesAnInverseCollapsedState()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        Assert.True(viewModel.LeftPanelVisible);
        Assert.False(viewModel.IsLeftPanelCollapsed);
        Assert.Equal(viewModel.LeftPanelWidth, viewModel.LeftPanelGridWidth.Value);

        viewModel.LeftPanelVisible = false;

        Assert.True(viewModel.IsLeftPanelCollapsed);
        Assert.Equal(0, viewModel.LeftPanelGridWidth.Value);

        viewModel.LeftPanelVisible = true;

        Assert.Equal(viewModel.LeftPanelWidth, viewModel.LeftPanelGridWidth.Value);
    }

    [Fact]
    public void PanelGridWidthsUpdateThePersistedPanelWidths()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        viewModel.LeftPanelGridWidth = new(440, Avalonia.Controls.GridUnitType.Pixel);
        viewModel.RightPanelGridWidth = new(410, Avalonia.Controls.GridUnitType.Pixel);

        Assert.Equal(440, viewModel.LeftPanelWidth);
        Assert.Equal(410, viewModel.RightPanelWidth);
    }

    [Fact]
    public void WorkspaceStartsInImportAndUpdatesContextFlags()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        Assert.Equal(WorkspaceMode.Import, viewModel.ActiveWorkspace);
        Assert.True(viewModel.IsImportWorkspace);
        Assert.False(viewModel.IsViewportWorkspace);

        viewModel.ActiveWorkspace = WorkspaceMode.Animate;

        Assert.True(viewModel.IsAnimateWorkspace);
        Assert.True(viewModel.IsViewportWorkspace);
        Assert.Equal(2, viewModel.InspectorTabIndex);
    }

    [Fact]
    public void EditToolSelectionUpdatesTheInstructionShownOverTheViewport()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        viewModel.SelectedEditTool = VoxelEditTool.Paint;

        Assert.Equal("Paint faces", viewModel.ActiveToolTitle);
        Assert.Contains("Left drag", viewModel.ActiveToolHint);
        Assert.Contains("Shift", viewModel.ActiveToolHint);
    }

    [Fact]
    public void AnimationPreviewCannotStartUntilAnAxisIsSelected()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        viewModel.ToggleAnimationPreview();

        Assert.False(viewModel.IsAnimationActive);
        Assert.Equal("Select at least one rotation axis", viewModel.StatusText);
    }

    [Fact]
    public void ResponsivePanelCollapseDoesNotOverwriteUserVisibility()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.LeftPanelWidth = 400;
        viewModel.RightPanelWidth = 360;

        viewModel.ResponsiveWindowWidth = 760;

        Assert.True(viewModel.LeftPanelVisible);
        Assert.True(viewModel.RightPanelVisible);
        Assert.Equal(0, viewModel.LeftPanelGridWidth.Value);
        Assert.Equal(0, viewModel.RightPanelGridWidth.Value);

        viewModel.ResponsiveWindowWidth = 1360;

        Assert.Equal(400, viewModel.LeftPanelGridWidth.Value);
        Assert.Equal(360, viewModel.RightPanelGridWidth.Value);
    }

    [Fact]
    public void ExportModeSelectionProvidesOnePrimaryActionLabel()
    {
        using MainWindowViewModel viewModel = CreateViewModel();

        viewModel.SelectedExportMode = ExportWorkflowMode.AnimatedGif;

        Assert.True(viewModel.IsAnimatedGifExport);
        Assert.Equal("Export animated GIF...", viewModel.PrimaryExportLabel);
        Assert.False(viewModel.IsDirectionSheetExport);
    }

    [Fact]
    public void ViewRingUsesCircularPointerMotion()
    {
        PixelVoxel.App.Views.RotationGizmoControl gizmo = new();
        gizmo.Measure(new Avalonia.Size(116, 132));
        gizmo.Arrange(new Avalonia.Rect(0, 0, 116, 132));

        float degrees = gizmo.GetDragDegrees(
            RotationGizmoAxis.View,
            new Avalonia.Point(106, 66),
            new Avalonia.Point(58, 114));

        Assert.Equal(-90f, degrees, 3);
    }

    [Fact]
    public void ObjectGizmoCanRotateAndResetIndependently()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        VoxelCameraState camera = viewModel.CurrentCameraState;

        viewModel.RotateObject(RotationGizmoAxis.LocalZ, 14f);

        Assert.Equal(14f, viewModel.CurrentModelRotation.RollDegrees);
        Assert.Equal(camera, viewModel.CurrentCameraState);
        viewModel.ResetObject();
        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);
    }

    [Fact]
    public void CameraResetRestoresGizmoRotationAndStopsRotationAnimation()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.Rotate(20f, 10f);
        viewModel.Pan(12f, -7f);
        viewModel.ZoomBy(1, 4);
        viewModel.RotateObject(RotationGizmoAxis.LocalX, 15f);
        viewModel.YawAnimationEnabled = true;
        viewModel.ToggleAnimationPreview();
        viewModel.AdvanceAnimations(0.1d);

        viewModel.ResetCamera();

        Assert.Equal(VoxelCameraState.Pixel2To1(), viewModel.CurrentCameraState);
        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);
        Assert.Null(viewModel.ManualZoomScale);
        Assert.False(viewModel.IsAnimationActive);
    }

    [Fact]
    public void ObjectRotationResetStopsAnimationBeforeTheNextFrame()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.YawAnimationEnabled = true;
        viewModel.PitchAnimationEnabled = true;
        viewModel.ToggleAnimationPreview();
        viewModel.AdvanceAnimations(0.1d);
        Assert.NotEqual(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);

        viewModel.ResetObject();
        viewModel.AdvanceAnimations(0.1d);

        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);
        Assert.False(viewModel.IsAnimationActive);
        Assert.False(viewModel.YawAnimationEnabled);
        Assert.False(viewModel.PitchAnimationEnabled);
    }

    [Fact]
    public void FaceSnapAfterFullResetUsesTheCanonicalCameraAndUnrotatedModel()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.YawAnimationEnabled = true;
        viewModel.ToggleAnimationPreview();
        viewModel.AdvanceAnimations(0.1d);
        viewModel.Rotate(80f, -40f);

        viewModel.ResetView();
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        viewModel.EnterFreeView();
        viewModel.Rotate(2f, -2f);
        viewModel.CommitCameraSnap();

        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);
        Assert.False(viewModel.IsAnimationActive);
        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);
    }

    [Fact]
    public void GizmoDragUsesCapturedYawPitchRollInsteadOfAccumulatingLocalAxes()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        VoxelModelRotationState start = new(20f, 30f, 40f);

        viewModel.SetObjectRotationFromDrag(start, RotationGizmoAxis.LocalY, 15f);
        Assert.Equal(new VoxelModelRotationState(35f, 30f, 40f), viewModel.CurrentModelRotation);

        viewModel.SetObjectRotationFromDrag(start, RotationGizmoAxis.LocalX, -10f);
        Assert.Equal(new VoxelModelRotationState(20f, 20f, 40f), viewModel.CurrentModelRotation);

        viewModel.SetObjectRotationFromDrag(start, RotationGizmoAxis.LocalZ, 25f);
        Assert.Equal(new VoxelModelRotationState(20f, 30f, 65f), viewModel.CurrentModelRotation);
    }

    [Fact]
    public void PanMovesOnlyTheCameraAnchor()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        VoxelModelRotationState rotation = viewModel.CurrentModelRotation;

        viewModel.Pan(12f, -7f);

        Assert.Equal(12f, viewModel.CurrentCameraState.PanX);
        Assert.Equal(-7f, viewModel.CurrentCameraState.PanY);
        Assert.Equal(rotation, viewModel.CurrentModelRotation);
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
        viewModel.YawAnimationEnabled = true;
        viewModel.PitchAnimationEnabled = true;
        viewModel.ToggleAnimationPreview();

        viewModel.AdvanceAnimations(0.1d);

        Assert.Equal(initialCamera, viewModel.CurrentCameraState);
        Assert.Equal(3f, viewModel.CurrentModelRotation.YawDegrees);
        Assert.Equal(3f, viewModel.CurrentModelRotation.PitchDegrees);
        Assert.Equal(1f, viewModel.CurrentModelRotation.Orientation.Length(), 5);
        VoxelModelRotationState animatedRotation = viewModel.CurrentModelRotation;
        viewModel.Rotate(10f, 10f);
        Assert.NotEqual(initialCamera, viewModel.CurrentCameraState);
        Assert.Equal(animatedRotation, viewModel.CurrentModelRotation);
        Assert.True(viewModel.YawAnimationEnabled);
        Assert.True(viewModel.PitchAnimationEnabled);
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        Assert.Equal(animatedRotation, viewModel.CurrentModelRotation);
        Assert.True(viewModel.YawAnimationEnabled);
        Assert.True(viewModel.PitchAnimationEnabled);
    }

    [Fact]
    public void PreviewPlayPauseKeepsAxisSelectionAndCurrentRotation()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        VoxelCameraState initialCamera = viewModel.CurrentCameraState;
        viewModel.YawAnimationEnabled = true;
        viewModel.PitchAnimationEnabled = true;
        viewModel.RollAnimationEnabled = true;
        Assert.False(viewModel.IsAnimationActive);
        viewModel.AdvanceAnimations(0.1d);
        Assert.Equal(VoxelModelRotationState.Identity, viewModel.CurrentModelRotation);

        viewModel.ToggleAnimationPreview();
        viewModel.AdvanceAnimations(0.1d);
        VoxelModelRotationState paused = viewModel.CurrentModelRotation;

        viewModel.ToggleAnimationPreview();
        viewModel.AdvanceAnimations(0.1d);

        Assert.Equal(3f, paused.YawDegrees);
        Assert.Equal(3f, paused.PitchDegrees);
        Assert.Equal(3f, paused.RollDegrees);
        Assert.Equal(1f, paused.Orientation.Length(), 5);
        Assert.Equal(paused, viewModel.CurrentModelRotation);
        Assert.Equal(initialCamera, viewModel.CurrentCameraState);
        Assert.False(viewModel.IsAnimationActive);
        Assert.True(viewModel.HasAnimationAxis);
        Assert.True(viewModel.YawAnimationEnabled);
    }

    [Fact]
    public void ContinuingTheSameDragLeavesAVisualSnapUsingTheRawOrbitPath()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        viewModel.Rotate(2.5f, -2.5f);
        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);

        viewModel.Rotate(10f, 0f);

        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);
    }

    [Fact]
    public void ReleasingOnAVisualSnapCommitsItAsTheNextDragOrigin()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        viewModel.Rotate(2.5f, -2.5f);
        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);

        viewModel.CommitCameraSnap();
        viewModel.Rotate(10f, 0f);

        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        Assert.Equal(0f, viewModel.CurrentCameraState.PitchDegrees);
    }

    [Fact]
    public void FaceSnapRangeIsCustomizableAndClamped()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        viewModel.CameraFaceSnapAngle = 5f;

        viewModel.Rotate(10f, 0f);
        Assert.Equal(7f, viewModel.CurrentCameraState.YawDegrees);

        viewModel.CameraFaceSnapAngle = 12f;
        Assert.Equal(0f, viewModel.CurrentCameraState.YawDegrees);
        viewModel.CameraFaceSnapAngle = 100f;
        Assert.Equal(30f, viewModel.CameraFaceSnapAngle);
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
    public async Task RotationCaptureUsesRequestedFpsWithoutDuplicateTerminalFrame()
    {
        VoxelDocument document = new(new VoxelDimensions(1, 1, 1),
            [new VoxelEntry(new VoxelCoordinate(0, 0, 0), VoxelCell.CreateUniform(new Rgba32Color(255, 0, 0, 255)))]);
        VoxelMeshData mesh = Assert.IsType<VoxelMeshData>(new VoxelSurfaceMesher().Build(document, TestContext.Current.CancellationToken).Mesh);
        PixelArtVoxelRasterizer rasterizer = new();
        SpriteExportCoordinator coordinator = new(rasterizer, new VoxelRenderTransformResolver(), new SpriteSheetExporter(new PngPixelWriter()));

        VoxelModelRotationState baseRotation = new(20f, 30f, 40f);
        SpriteFrame[] frames = await coordinator.RenderRotationFramesAsync(12, 60, true, false, false,
            VoxelCameraState.Pixel2To1(), baseRotation, mesh, new PixelRenderLayoutResolver().Resolve(new VoxelDimensions(1, 1, 1), 32, 32),
            VoxelRenderStyle.Default, true, TestContext.Current.CancellationToken);

        Assert.Equal(72, frames.Length);
        Assert.Equal(20, frames[0].YawDegrees);
        Assert.Equal(15, frames[^1].YawDegrees);
        Assert.All(frames, frame => Assert.Equal(83, frame.DurationMilliseconds));
    }

    [Theory]
    [InlineData(VoxelViewMode.FreeView, VoxelCameraPreset.Free, 31f, -22f)]
    [InlineData(VoxelViewMode.PixelPreview, VoxelCameraPreset.Front, 0f, 0f)]
    [InlineData(VoxelViewMode.PixelPreview, VoxelCameraPreset.Right, -90f, 0f)]
    [InlineData(VoxelViewMode.PixelPreview, VoxelCameraPreset.Top, 0f, -90f)]
    [InlineData(VoxelViewMode.PixelPreview, VoxelCameraPreset.Pixel2To1, -45f, -30f)]
    [InlineData(VoxelViewMode.PixelPreview, VoxelCameraPreset.TrueIsometric, -45f, -35.2643897f)]
    public async Task RotationCaptureFirstFrameUsesCenteredEditorCameraAndBaseRotation(
        VoxelViewMode mode,
        VoxelCameraPreset preset,
        float yaw,
        float pitch)
    {
        (VoxelMeshData mesh, PixelRenderLayout layout) = CreateAsymmetricExportFixture();
        PixelArtVoxelRasterizer rasterizer = new();
        SpriteExportCoordinator coordinator = new(rasterizer, new VoxelRenderTransformResolver(), new SpriteSheetExporter(new PngPixelWriter()));
        VoxelCameraState editorCamera = new(mode, preset, yaw, pitch, 13f, -9f, 5f);
        VoxelCameraState expectedCamera = editorCamera with { PanX = 0f, PanY = 0f, Zoom = 1f };
        VoxelModelRotationState baseRotation = new(17f, 29f, 41f);

        SpriteFrame[] frames = await coordinator.RenderRotationFramesAsync(
            12, 60, true, false, false, editorCamera, baseRotation, mesh, layout,
            VoxelRenderStyle.Default, true, TestContext.Current.CancellationToken);
        Rgba32Color background = VoxelRenderStyle.Default.Background;
        PixelFramebuffer expected = rasterizer.Render(
            mesh, expectedCamera, baseRotation, layout,
            VoxelRenderStyle.Default with
            {
                Background = new Rgba32Color(background.Red, background.Green, background.Blue, 0),
            });

        Assert.Equal(expected.Pixels, frames[0].Pixels);
    }

    [Fact]
    public async Task RotationCaptureIgnoresEditorPanAndZoomForEveryFrame()
    {
        (VoxelMeshData mesh, PixelRenderLayout layout) = CreateAsymmetricExportFixture();
        SpriteExportCoordinator coordinator = new(new PixelArtVoxelRasterizer(), new VoxelRenderTransformResolver(), new SpriteSheetExporter(new PngPixelWriter()));
        VoxelCameraState centered = new(VoxelViewMode.FreeView, VoxelCameraPreset.Free, 23f, -37f, 0f, 0f, 1f);
        VoxelCameraState navigated = centered with { PanX = 18f, PanY = -11f, Zoom = 7f };
        VoxelModelRotationState baseRotation = new(12f, 34f, 56f);

        SpriteFrame[] centeredFrames = await coordinator.RenderRotationFramesAsync(
            6, 180, true, true, true, centered, baseRotation, mesh, layout,
            VoxelRenderStyle.Default, true, TestContext.Current.CancellationToken);
        SpriteFrame[] navigatedFrames = await coordinator.RenderRotationFramesAsync(
            6, 180, true, true, true, navigated, baseRotation, mesh, layout,
            VoxelRenderStyle.Default, true, TestContext.Current.CancellationToken);

        Assert.Equal(centeredFrames.Length, navigatedFrames.Length);
        for (int index = 0; index < centeredFrames.Length; index++)
        {
            Assert.Equal(centeredFrames[index].Pixels, navigatedFrames[index].Pixels);
            Assert.Equal(centeredFrames[index].DurationMilliseconds, navigatedFrames[index].DurationMilliseconds);
            Assert.Equal((centeredFrames[index].PivotX, centeredFrames[index].PivotY),
                (navigatedFrames[index].PivotX, navigatedFrames[index].PivotY));
        }
    }

    [Fact]
    public async Task RotationCaptureAddsOneSharedAngleAndPreservesUnselectedAxes()
    {
        (VoxelMeshData mesh, PixelRenderLayout layout) = CreateAsymmetricExportFixture();
        PixelArtVoxelRasterizer rasterizer = new();
        SpriteExportCoordinator coordinator = new(rasterizer, new VoxelRenderTransformResolver(), new SpriteSheetExporter(new PngPixelWriter()));
        VoxelCameraState camera = new(VoxelViewMode.FreeView, VoxelCameraPreset.Free, 11f, -19f, 4f, 8f, 3f);
        VoxelModelRotationState baseRotation = new(20f, 30f, 40f);

        SpriteFrame[] frames = await coordinator.RenderRotationFramesAsync(
            4, 180, true, false, true, camera, baseRotation, mesh, layout,
            VoxelRenderStyle.Default, true, TestContext.Current.CancellationToken);
        VoxelModelRotationState expectedRotation = new(110f, 30f, 130f);
        Rgba32Color background = VoxelRenderStyle.Default.Background;
        PixelFramebuffer expected = rasterizer.Render(
            mesh, camera with { PanX = 0f, PanY = 0f, Zoom = 1f }, expectedRotation, layout,
            VoxelRenderStyle.Default with
            {
                Background = new Rgba32Color(background.Red, background.Green, background.Blue, 0),
            });

        Assert.Equal(8, frames.Length);
        Assert.Equal(expected.Pixels, frames[2].Pixels);
        Assert.Equal(-25f, frames[^1].YawDegrees);
    }

    private static (VoxelMeshData Mesh, PixelRenderLayout Layout) CreateAsymmetricExportFixture()
    {
        VoxelDocument document = new(
            new VoxelDimensions(3, 2, 2),
            [
                new VoxelEntry(new VoxelCoordinate(0, 0, 0), VoxelCell.CreateUniform(new Rgba32Color(255, 0, 0, 255))),
                new VoxelEntry(new VoxelCoordinate(2, 1, 1), VoxelCell.CreateUniform(new Rgba32Color(0, 255, 0, 255))),
            ]);
        VoxelMeshData mesh = Assert.IsType<VoxelMeshData>(
            new VoxelSurfaceMesher().Build(document, TestContext.Current.CancellationToken).Mesh);
        PixelRenderLayout layout = new PixelRenderLayoutResolver().Resolve(document.Storage.Dimensions, 64, 64);
        return (mesh, layout);
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
        bool exportWasEnabledWhenNotified = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.CanExport))
            {
                exportWasEnabledWhenNotified |= viewModel.CanExport;
            }
        };

        await viewModel.LoadHorizontalSheetAsync(validPath);

        Assert.True(viewModel.CanApplyImport);
        Assert.Equal(WorkspaceMode.Import, viewModel.ActiveWorkspace);
        Assert.Equal(ImportWorkflowStep.MapAndAlign, viewModel.ActiveImportStep);
        Assert.NotNull(viewModel.SelectedImportFaceSlot);
        Assert.True(viewModel.CanGoToNextImportStep);
        Assert.Null(viewModel.CurrentMesh);
        viewModel.MoveToNextImportStep();
        Assert.Equal(ImportWorkflowStep.Validate, viewModel.ActiveImportStep);
        viewModel.MoveToNextImportStep();
        Assert.Equal(ImportWorkflowStep.Reconstruct, viewModel.ActiveImportStep);
        await viewModel.ApplyImportDraftAsync();
        VoxelMeshData existingMesh = Assert.IsType<VoxelMeshData>(viewModel.CurrentMesh);
        Assert.Equal(WorkspaceMode.Edit, viewModel.ActiveWorkspace);
        Assert.True(viewModel.CanExport);
        Assert.True(exportWasEnabledWhenNotified);

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
    public async Task ProjectPaletteIsBoundedDirtyAndRoundTrips()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "palette.pxv");
        using MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.NewProjectAsync(TestContext.Current.CancellationToken);
        for (int index = 0; index < 35; index++)
        {
            viewModel.EditColor = Avalonia.Media.Color.FromRgb((byte)index, (byte)(index + 1), (byte)(index + 2));
            viewModel.AddCurrentColorToPalette();
        }
        Assert.Equal(32, viewModel.ProjectPalette.Count);
        Assert.True(viewModel.IsProjectDirty);
        Assert.True(await viewModel.SaveProjectAsync(path, TestContext.Current.CancellationToken));
        Assert.False(viewModel.IsProjectDirty);

        viewModel.RemovePaletteColor(viewModel.ProjectPalette[0]);
        Assert.True(viewModel.IsProjectDirty);
        Assert.True(await viewModel.LoadProjectAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(32, viewModel.ProjectPalette.Count);
        Assert.False(viewModel.IsProjectDirty);
    }

    [Fact]
    public async Task EyedropperSamplesOriginalFaceColorAndEmptySpaceDoesNothing()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        viewModel.EditColor = Avalonia.Media.Color.FromRgb(12, 34, 56);
        await viewModel.NewProjectAsync(TestContext.Current.CancellationToken);
        viewModel.EditColor = Avalonia.Media.Color.FromRgb(1, 2, 3);
        viewModel.SelectPreset(VoxelCameraPreset.Front);
        PixelRenderLayout layout = Assert.IsType<PixelRenderLayout>(viewModel.CurrentRenderLayout);
        VoxelRenderTransform transform = Assert.IsType<VoxelRenderTransform>(viewModel.CurrentRenderTransform);
        System.Numerics.Vector3 screen = transform.ProjectToScreen(new System.Numerics.Vector3(15.5f, 15.5f, 16f));
        viewModel.SelectedEditTool = VoxelEditTool.Eyedropper;

        Assert.False(viewModel.BeginEditStroke(screen.X, screen.Y, layout.Width, layout.Height, false));
        Assert.Equal(Avalonia.Media.Color.FromRgb(12, 34, 56), viewModel.EditColor);
        Assert.False(viewModel.BeginEditStroke(-100, -100, layout.Width, layout.Height, false));
        Assert.Equal(Avalonia.Media.Color.FromRgb(12, 34, 56), viewModel.EditColor);
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
