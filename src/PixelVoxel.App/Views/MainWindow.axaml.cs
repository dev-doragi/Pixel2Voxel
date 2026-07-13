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
    private static readonly FilePickerFileType PngFileType = new("PNG image")
    {
        Patterns = ["*.png"],
    };

    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();
    private Point? _lastPointerPosition;
    private TimeSpan _lastAnimationTime;

    public MainWindow()
    {
        InitializeComponent();
        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000d / 60d), DispatcherPriority.Render, OnAnimationTick);
        _animationTimer.Start();
        Closed += OnClosed;
    }

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
        if (!point.Properties.IsRightButtonPressed || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _lastPointerPosition = point.Position;
        e.Pointer.Capture(ViewportHost);
        e.Handled = true;
    }

    private void Viewport_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPointerPosition is null || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(ViewportHost);
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

    private void Viewport_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_lastPointerPosition is null)
        {
            return;
        }

        ReleasePointer(e.Pointer);
        e.Handled = true;
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
        _lastPointerPosition = null;
        pointer.Capture(null);
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

    private void OnClosed(object? sender, EventArgs e)
    {
        _animationTimer.Stop();
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
