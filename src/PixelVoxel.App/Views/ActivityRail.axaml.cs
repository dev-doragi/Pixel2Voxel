using Avalonia.Controls;
using Avalonia.Interactivity;
using PixelVoxel.App.ViewModels;

namespace PixelVoxel.App.Views;

public partial class ActivityRail : UserControl
{
    public ActivityRail() => InitializeComponent();

    private void Workspace_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string modeName } &&
            Enum.TryParse(modeName, out WorkspaceMode mode) &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ActiveWorkspace = mode;
        }
    }

    private void ToggleContextPanel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.LeftPanelVisible = !viewModel.LeftPanelVisible;
    }
}
