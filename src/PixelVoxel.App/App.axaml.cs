using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PixelVoxel.App.ViewModels;
using PixelVoxel.App.Services;
using PixelVoxel.App.Views;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;
using PixelVoxel.Export;

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
            PixelArtVoxelRasterizer rasterizer = new();
            VoxelRenderTransformResolver transformResolver = new();
            SpriteExportCoordinator exportCoordinator = new(
                rasterizer,
                transformResolver,
                new SpriteSheetExporter(new PngPixelWriter()));
            MainWindowViewModel viewModel = new(
                new AsepriteSpriteSheetImporter(),
                new VisualHullVoxelReconstructor(),
                new VoxelSurfaceMesher(),
                rasterizer,
                transformResolver,
                new PixelRenderLayoutResolver(),
                new ViewportSettingsStore(),
                exportCoordinator,
                new PxvProjectSerializer(new PngPixelWriter(), new PngPixelReader()),
                new VoxelPicker());

            MainWindow window = new()
            {
                DataContext = viewModel,
            };
            desktop.MainWindow = window;

            string? projectPath = desktop.Args?
                .FirstOrDefault(argument =>
                    Path.GetExtension(argument).Equals(".pxv", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                window.Opened += async (_, _) => await viewModel.LoadProjectAsync(projectPath);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
