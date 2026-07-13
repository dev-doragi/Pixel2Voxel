using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PixelVoxel.App.ViewModels;
using PixelVoxel.App.Services;
using PixelVoxel.App.Views;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;

namespace PixelVoxel.App;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindowViewModel viewModel = new(
                new AsepriteSpriteSheetImporter(),
                new VisualHullVoxelReconstructor(),
                new VoxelSurfaceMesher(),
                new PixelArtVoxelRasterizer(),
                new VoxelRenderTransformResolver(),
                new PixelRenderLayoutResolver(),
                new ViewportSettingsStore());

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
