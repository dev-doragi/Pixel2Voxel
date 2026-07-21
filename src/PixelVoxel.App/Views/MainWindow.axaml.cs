using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PixelVoxel.App.ViewModels;
using PixelVoxel.Core;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Views;

public sealed partial class MainWindow : Window
{
    private static readonly DataFormat<string> ImportFaceDragFormat =
        DataFormat.CreateInProcessFormat<string>("PixelVoxel.ImportFace");
    private static readonly FilePickerFileType PngFileType = new("PNG image")
    {
        Patterns = ["*.png"],
    };
    private static readonly FilePickerFileType ProjectFileType = new("Pixel Voxel project")
    {
        Patterns = ["*.pxv"],
    };

    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();
    private Point? _lastPointerPosition;
    private bool _isEditingStroke;
    private bool _allowClose;
    private bool _closingPromptOpen;
    private TimeSpan _lastAnimationTime;

    public MainWindow()
    {
        InitializeComponent();
        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000d / 60d), DispatcherPriority.Render, OnAnimationTick);
        _animationTimer.Start();
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void NewProject_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && await EnsureCanReplaceProjectAsync())
        {
            await viewModel.NewProjectAsync();
        }
    }

    private async void OpenProject_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !await EnsureCanReplaceProjectAsync()) return;
        string? path = await PickProjectOpenPathAsync();
        if (path is not null) await viewModel.LoadProjectAsync(path);
    }

    private async void SaveProject_Click(object? sender, RoutedEventArgs e) =>
        await SaveProjectAsync(forceChoosePath: false);

    private async void SaveProjectAs_Click(object? sender, RoutedEventArgs e) =>
        await SaveProjectAsync(forceChoosePath: true);

    private void Undo_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.Undo();

    private void Redo_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.Redo();

    private void DeleteSelection_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.DeleteSelection();

    private void ClearSelection_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.ClearSelection();

    private async void OpenHorizontalSheet_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickPngAsync("Open Front/Right/Back/Left/Top/Bottom 6x1 sheet");
        if (path is not null && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadHorizontalSheetAsync(path);
        }
    }

    private async void SelectFaceFile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string faceName } ||
            !Enum.TryParse(faceName, out VoxelFace face) ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = await PickPngAsync($"Select {face} PNG");
        if (path is not null)
        {
            viewModel.SetSeparatePath(face, path);
        }
    }

    private async void ReconstructSeparate_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ReconstructSeparateAsync();
        }
    }

    private async void ChooseSeparateFiles_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> paths = await PickPngsAsync("Choose up to six separate face PNG files");
        if (paths.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadSeparateFilesAsync(paths);
        }
    }

    private async void ApplyImport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && await EnsureCanReplaceProjectAsync())
        {
            await viewModel.ApplyImportDraftAsync();
        }
    }

    private async void Reimport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ReimportAsync();
        }
    }

    private async void OpenRecentImport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.OpenSelectedRecentImportAsync();
        }
    }

    private void ResetImportFace_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: VoxelFace face } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ResetImportFace(face);
        }
    }

    private void ResetAllImport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ResetAllImportAdjustments();
        }
    }

    private async void ExportCurrentView_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickPngSavePathAsync("Export current logical pixel view", "pixel-voxel-view.png");
        if (path is not null && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ExportCurrentViewAsync(path);
        }
    }

    private async void ExportDirectionSheet_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        string? path = await PickPngSavePathAsync(
            "Export horizontal direction sheet and Aseprite JSON",
            $"pixel-voxel-directions-{viewModel.ExportDirectionCount}.png");
        if (path is not null)
        {
            await viewModel.ExportDirectionSheetAsync(path);
        }
    }

    private void SelectExportDirectionCount_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string text } &&
            int.TryParse(text, out int count) &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExportDirectionCount = count;
        }
    }

    private void PngDrop_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetDroppedPngPaths(e).Count > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void SheetDrop_Drop(object? sender, DragEventArgs e)
    {
        IReadOnlyList<string> paths = GetDroppedPngPaths(e);
        if (DataContext is not MainWindowViewModel viewModel) return;
        if (paths.Count != 1)
        {
            viewModel.ReportError("Drop exactly one PNG into the 6×1 sheet area.");
            return;
        }

        await viewModel.LoadHorizontalSheetAsync(paths[0]);
    }

    private async void SeparateDrop_Drop(object? sender, DragEventArgs e)
    {
        IReadOnlyList<string> paths = GetDroppedPngPaths(e);
        if (paths.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadSeparateFilesAsync(paths);
        }
    }

    private async void ImportFaceHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: ImportFaceSlotViewModel slot } ||
            !slot.HasSource ||
            !e.GetCurrentPoint((Control)sender).Properties.IsLeftButtonPressed)
        {
            return;
        }

        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(ImportFaceDragFormat, slot.TargetFace.ToString()));
        await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
    }

    private void ImportFaceCard_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(ImportFaceDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ImportFaceCard_Drop(object? sender, DragEventArgs e)
    {
        string? sourceFaceName = e.DataTransfer.TryGetValue(ImportFaceDragFormat);
        if (Enum.TryParse(sourceFaceName, out VoxelFace source) &&
            sender is Control { DataContext: ImportFaceSlotViewModel targetSlot } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SwapImportSources(source, targetSlot.TargetFace);
            e.DragEffects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    private void SelectEditTool_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string toolName } &&
            Enum.TryParse(toolName, out VoxelEditTool tool) &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedEditTool = tool;
        }
    }

    private void MoveSelection_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string direction } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        (int x, int y, int z) = direction switch
        {
            "+X" => (1, 0, 0),
            "-X" => (-1, 0, 0),
            "+Y" => (0, 1, 0),
            "-Y" => (0, -1, 0),
            "+Z" => (0, 0, 1),
            "-Z" => (0, 0, -1),
            _ => (0, 0, 0),
        };
        viewModel.MoveSelection(x, y, z);
    }

    private async void ResizeVolume_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        int clipped = viewModel.CountResizeClippedVoxels();
        bool allowClipping = clipped == 0 || await ConfirmAsync(
            "Resize Volume",
            $"This resize removes {clipped:N0} occupied voxels. Continue?");
        if (allowClipping) viewModel.ResizeVolume(allowClipping: clipped > 0);
    }

    private void SelectPreset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string presetName } &&
            Enum.TryParse(presetName, out VoxelCameraPreset preset) &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectPreset(preset);
        }
    }

    private void LoadSavedDefault_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.LoadSavedDefault();
        }
    }

    private void SaveCurrentAsDefault_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveCurrentAsDefault();
        }
    }

    private void FitZoom_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.FitZoom();
        }
    }

    private void Viewport_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(ViewportHost);
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            _lastPointerPosition = point.Position;
            e.Pointer.Capture(ViewportHost);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            (float x, float y, int width, int height) = GetEditorPointer(point.Position, viewModel);
            _isEditingStroke = viewModel.BeginEditStroke(
                x,
                y,
                width,
                height,
                e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            if (_isEditingStroke) e.Pointer.Capture(ViewportHost);
            e.Handled = viewModel.HasDocument;
        }
    }

    private void Viewport_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(ViewportHost);
        if (_isEditingStroke)
        {
            if (!point.Properties.IsLeftButtonPressed)
            {
                viewModel.EndEditStroke();
                _isEditingStroke = false;
                e.Pointer.Capture(null);
                return;
            }

            (float x, float y, int width, int height) = GetEditorPointer(point.Position, viewModel);
            viewModel.ContinueEditStroke(x, y, width, height);
            e.Handled = true;
            return;
        }

        if (_lastPointerPosition is null)
        {
            if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsRightButtonPressed)
            {
                (float x, float y, int width, int height) = GetEditorPointer(point.Position, viewModel);
                viewModel.UpdateEditorHover(x, y, width, height);
            }

            return;
        }
        if (!point.Properties.IsRightButtonPressed)
        {
            ReleasePointer(e.Pointer);
            return;
        }

        Vector delta = point.Position - _lastPointerPosition.Value;
        _lastPointerPosition = point.Position;
        viewModel.Rotate((float)delta.X, (float)delta.Y);
        e.Handled = true;
    }

    private void Viewport_PointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isEditingStroke && _lastPointerPosition is null)
        {
            (DataContext as MainWindowViewModel)?.ClearEditorHover();
        }
    }

    private void Viewport_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isEditingStroke && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.EndEditStroke();
            _isEditingStroke = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_lastPointerPosition is null)
        {
            return;
        }

        ReleasePointer(e.Pointer);
        e.Handled = true;
    }

    private (float X, float Y, int Width, int Height) GetEditorPointer(
        Point position,
        MainWindowViewModel viewModel)
    {
        double scaling = viewModel.IsCpuFallbackActive
            ? 1d
            : TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
        return (
            (float)(position.X * scaling),
            (float)(position.Y * scaling),
            Math.Max(1, (int)Math.Round(ViewportHost.Bounds.Width * scaling)),
            Math.Max(1, (int)Math.Round(ViewportHost.Bounds.Height * scaling)));
    }

    private void Viewport_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || e.Delta.Y == 0d)
        {
            return;
        }

        viewModel.ZoomBy(Math.Sign(e.Delta.Y), CalculateFitScale(viewModel.CurrentRenderLayout));
        e.Handled = true;
    }

    private int CalculateFitScale(PixelRenderLayout? layout)
    {
        if (layout is null)
        {
            return 1;
        }

        double renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
        int width = Math.Max(1, (int)Math.Round(ViewportHost.Bounds.Width * renderScaling));
        int height = Math.Max(1, (int)Math.Round(ViewportHost.Bounds.Height * renderScaling));
        return Math.Max(1, Math.Min(width / layout.Width, height / layout.Height));
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        TimeSpan current = _animationClock.Elapsed;
        double elapsedSeconds = (current - _lastAnimationTime).TotalSeconds;
        _lastAnimationTime = current;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AdvanceAnimations(elapsedSeconds);
        }
    }

    private void ReleasePointer(IPointer pointer)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CommitCameraSnap();
        }

        _lastPointerPosition = null;
        pointer.Capture(null);
    }

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || e.Source is TextBox) return;
        bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (control && e.Key == Key.Z)
        {
            viewModel.Undo();
            e.Handled = true;
        }
        else if (control && e.Key == Key.Y)
        {
            viewModel.Redo();
            e.Handled = true;
        }
        else if (control && e.Key == Key.S)
        {
            await SaveProjectAsync(forceChoosePath: false);
            e.Handled = true;
        }
        else if (control && e.Key == Key.O)
        {
            OpenProject_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (control && e.Key == Key.N)
        {
            NewProject_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            viewModel.DeleteSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.ClearSelection();
            e.Handled = true;
        }
    }

    private async Task<bool> SaveProjectAsync(bool forceChoosePath)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.HasDocument) return false;
        string? path = forceChoosePath || viewModel.ProjectPath is null
            ? await PickProjectSavePathAsync()
            : viewModel.ProjectPath;
        return path is not null && await viewModel.SaveProjectAsync(path);
    }

    private async Task<bool> EnsureCanReplaceProjectAsync()
    {
        if (DataContext is not MainWindowViewModel { IsProjectDirty: true }) return true;
        UnsavedChoice choice = await ShowUnsavedChangesDialogAsync();
        return choice switch
        {
            UnsavedChoice.Save => await SaveProjectAsync(forceChoosePath: false),
            UnsavedChoice.Discard => true,
            _ => false,
        };
    }

    private async Task<UnsavedChoice> ShowUnsavedChangesDialogAsync()
    {
        Window dialog = new()
        {
            Title = "Unsaved Pixel Voxel project",
            Width = 430,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        Button save = new() { Content = "Save", MinWidth = 90 };
        Button discard = new() { Content = "Discard", MinWidth = 90 };
        Button cancel = new() { Content = "Cancel", MinWidth = 90 };
        save.Click += (_, _) => dialog.Close(UnsavedChoice.Save);
        discard.Click += (_, _) => dialog.Close(UnsavedChoice.Discard);
        cancel.Click += (_, _) => dialog.Close(UnsavedChoice.Cancel);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = "The current project has unsaved changes.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new StackPanel
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Children = { save, discard, cancel },
                },
            },
        };
        return await dialog.ShowDialog<UnsavedChoice>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        Window dialog = new()
        {
            Title = title,
            Width = 430,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        Button confirm = new() { Content = "Continue", MinWidth = 90 };
        Button cancel = new() { Content = "Cancel", MinWidth = 90 };
        confirm.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Children = { confirm, cancel },
                },
            },
        };
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task<string?> PickProjectOpenPathAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Pixel Voxel project",
                AllowMultiple = false,
                FileTypeFilter = [ProjectFileType],
            });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private async Task<string?> PickProjectSavePathAsync()
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save Pixel Voxel project",
                SuggestedFileName = "pixel-voxel-project.pxv",
                DefaultExtension = "pxv",
                FileTypeChoices = [ProjectFileType],
                ShowOverwritePrompt = true,
            });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickPngAsync(string title)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [PngFileType],
            });

        if (files.Count == 0)
        {
            return null;
        }

        string? path = files[0].TryGetLocalPath();
        if (path is null && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ReportError("Only local PNG files are supported.");
        }

        return path;
    }

    private async Task<IReadOnlyList<string>> PickPngsAsync(string title)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter = [PngFileType],
            });
        string[] paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => path is not null)
            .Cast<string>()
            .Take(6)
            .ToArray();
        if (paths.Length != files.Count && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ReportError("Only six local PNG files can be inspected at once.");
        }

        return paths;
    }

    private async Task<string?> PickPngSavePathAsync(string title, string suggestedName)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                DefaultExtension = "png",
                FileTypeChoices = [PngFileType],
                ShowOverwritePrompt = true,
            });
        string? path = file?.TryGetLocalPath();
        if (file is not null && path is null && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ReportError("Only local PNG output paths are supported.");
        }

        return path;
    }

    private static IReadOnlyList<string> GetDroppedPngPaths(DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? [])
            .OfType<IStorageFile>()
            .Select(file => file.TryGetLocalPath())
            .Where(path => path is not null &&
                Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToArray();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || DataContext is not MainWindowViewModel { IsProjectDirty: true }) return;
        e.Cancel = true;
        if (_closingPromptOpen) return;
        _closingPromptOpen = true;
        try
        {
            if (await EnsureCanReplaceProjectAsync())
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closingPromptOpen = false;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _animationTimer.Stop();
        Closing -= OnClosing;
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private enum UnsavedChoice
    {
        Cancel,
        Save,
        Discard,
    }
}
