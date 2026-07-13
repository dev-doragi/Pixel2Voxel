using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using PixelVoxel.App.ViewModels;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Views;

/// <summary>Hosts Silk.NET rendering inside Avalonia's context-bound GL callbacks.</summary>
public sealed class SilkVoxelViewportControl : OpenGlControlBase
{
    private readonly SilkVoxelRenderer _renderer = new();
    private MainWindowViewModel? _viewModel;
    private VoxelMeshData? _queuedMesh;
    private bool _failed;
    private string? _failureMessage;

    /// <summary>Initializes subscriptions to inherited view-model state.</summary>
    public SilkVoxelViewportControl()
    {
        DataContextChanged += OnDataContextChanged;
    }

    /// <inheritdoc />
    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            _renderer.Initialize(
                gl.GetProcAddress,
                GlVersion.Type == GlProfileType.OpenGLES);
            QueueLatestScene();
        }
        catch (Exception exception)
        {
            FallBackToCpu($"{GlVersion}: {exception.Message}");
        }
    }

    /// <inheritdoc />
    protected override void OnOpenGlRender(GlInterface gl, int framebuffer)
    {
        if (_failed ||
            _viewModel?.CurrentRenderTransform is not VoxelRenderTransform transform ||
            _viewModel.CurrentRenderLayout is not PixelRenderLayout layout)
        {
            return;
        }

        try
        {
            VoxelMeshData? mesh = _viewModel.CurrentMesh;
            if (!ReferenceEquals(mesh, _queuedMesh))
            {
                _renderer.SetMesh(mesh);
                _queuedMesh = mesh;
            }

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            int width = Math.Max(1, (int)Math.Round(Bounds.Width * scaling));
            int height = Math.Max(1, (int)Math.Round(Bounds.Height * scaling));
            _renderer.Render(
                framebuffer,
                width,
                height,
                transform,
                layout,
                _viewModel.CurrentRenderStyle,
                _viewModel.ManualZoomScale);
        }
        catch (Exception exception)
        {
            FallBackToCpu(exception.Message);
        }
    }

    /// <inheritdoc />
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer.Deinitialize();
        _queuedMesh = null;
    }

    /// <inheritdoc />
    protected override void OnOpenGlLost()
    {
        FallBackToCpu("The OpenGL context was lost.");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.RenderStateChanged -= OnRenderStateChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.RenderStateChanged += OnRenderStateChanged;
            if (_failed && _failureMessage is not null)
            {
                _viewModel.ActivateCpuFallback(_failureMessage);
            }
        }

        QueueLatestScene();
    }

    private void OnRenderStateChanged(object? sender, EventArgs e) => QueueLatestScene();

    private void QueueLatestScene()
    {
        if (_failed)
        {
            return;
        }

        Dispatcher.UIThread.Post(RequestNextFrameRendering);
    }

    private void FallBackToCpu(string message)
    {
        _failed = true;
        _failureMessage = message;
        Dispatcher.UIThread.Post(() =>
        {
            IsVisible = false;
            _viewModel?.ActivateCpuFallback(message);
        });
    }
}
