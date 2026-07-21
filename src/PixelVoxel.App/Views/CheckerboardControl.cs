using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PixelVoxel.App.Views;

/// <summary>Draws a neutral checkerboard behind transparent import preview pixels.</summary>
public sealed class CheckerboardControl : Control
{
    private static readonly IBrush Light = new SolidColorBrush(Color.Parse("#3A3E48"));
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#292D35"));

    public override void Render(DrawingContext context)
    {
        const double cell = 8d;
        context.FillRectangle(Dark, Bounds);
        int rows = (int)Math.Ceiling(Bounds.Height / cell);
        int columns = (int)Math.Ceiling(Bounds.Width / cell);
        for (int row = 0; row < rows; row++)
        {
            for (int column = row % 2; column < columns; column += 2)
            {
                context.FillRectangle(
                    Light,
                    new Rect(column * cell, row * cell, cell, cell));
            }
        }
    }
}
