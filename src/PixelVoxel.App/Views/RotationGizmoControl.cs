using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PixelVoxel.App.ViewModels;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Views;

public sealed class RotationGizmoControl : Control
{
    private const double OuterRadius = 72d;
    private const double AxisRadius = 54d;
    private static readonly IBrush XBrush = Brushes.Red;
    private static readonly IBrush YBrush = Brushes.LimeGreen;
    private static readonly IBrush ZBrush = Brushes.DodgerBlue;

    public RotationGizmoAxis ActiveAxis { get; private set; }
    public float DragDegrees { get; private set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not MainWindowViewModel { HasDocument: true } viewModel) return;

        Point center = new(Bounds.Width / 2d, Bounds.Height / 2d);
        DrawAxisRing(context, center, viewModel.CurrentRenderTransform, RotationGizmoAxis.LocalX, XBrush);
        DrawAxisRing(context, center, viewModel.CurrentRenderTransform, RotationGizmoAxis.LocalY, YBrush);
        DrawAxisRing(context, center, viewModel.CurrentRenderTransform, RotationGizmoAxis.LocalZ, ZBrush);
        context.DrawEllipse(null, PenFor(RotationGizmoAxis.View, Brushes.White), center, OuterRadius, OuterRadius);

        if (ActiveAxis != RotationGizmoAxis.None)
        {
            FormattedText label = new(
                $"{AxisName(ActiveAxis)}  {DragDegrees:+0.0;-0.0;0.0}°",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                13d,
                Brushes.White);
            context.DrawText(label, new Point(center.X - (label.Width / 2d), center.Y + OuterRadius + 4d));
        }
    }

    public RotationGizmoAxis HitTestRing(Point point)
    {
        if (DataContext is not MainWindowViewModel { HasDocument: true } viewModel) return RotationGizmoAxis.None;
        Point center = new(Bounds.Width / 2d, Bounds.Height / 2d);
        double outerDistance = Distance(point, center);
        if (Math.Abs(outerDistance - OuterRadius) <= 7d) return RotationGizmoAxis.View;

        foreach (RotationGizmoAxis axis in new[] { RotationGizmoAxis.LocalX, RotationGizmoAxis.LocalY, RotationGizmoAxis.LocalZ })
        {
            IReadOnlyList<Point> samples = GetRingPoints(center, viewModel.CurrentRenderTransform, axis);
            if (DistanceToPolyline(point, samples) <= 7d) return axis;
        }

        return RotationGizmoAxis.None;
    }

    public void SetDrag(RotationGizmoAxis axis, float degrees)
    {
        ActiveAxis = axis;
        DragDegrees = degrees;
        InvalidateVisual();
    }

    public void ClearDrag()
    {
        ActiveAxis = RotationGizmoAxis.None;
        DragDegrees = 0f;
        InvalidateVisual();
    }

    public void Refresh() => InvalidateVisual();

    private void DrawAxisRing(
        DrawingContext context,
        Point center,
        VoxelRenderTransform? transform,
        RotationGizmoAxis axis,
        IBrush brush)
    {
        IReadOnlyList<Point> points = GetRingPoints(center, transform, axis);
        Pen pen = PenFor(axis, brush);
        for (int index = 1; index < points.Count; index++) context.DrawLine(pen, points[index - 1], points[index]);
    }

    private Pen PenFor(RotationGizmoAxis axis, IBrush brush) =>
        new(brush, ActiveAxis == axis ? 4d : 2d);

    private static IReadOnlyList<Point> GetRingPoints(
        Point center,
        VoxelRenderTransform? transform,
        RotationGizmoAxis axis)
    {
        List<Vector2> projected = [];
        for (int index = 0; index <= 64; index++)
        {
            float angle = index * (MathF.Tau / 64f);
            Vector3 direction = axis switch
            {
                RotationGizmoAxis.LocalX => new Vector3(0f, MathF.Cos(angle), MathF.Sin(angle)),
                RotationGizmoAxis.LocalY => new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)),
                _ => new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f),
            };
            projected.Add(transform?.ProjectDirectionToScreen(direction) ?? new Vector2(direction.X, -direction.Y));
        }

        float extent = MathF.Max(0.001f, projected.Max(value => value.Length()));
        return projected.Select(value => new Point(
            center.X + (value.X * AxisRadius / extent),
            center.Y + (value.Y * AxisRadius / extent))).ToArray();
    }

    private static double DistanceToPolyline(Point point, IReadOnlyList<Point> samples)
    {
        double closest = double.PositiveInfinity;
        for (int index = 1; index < samples.Count; index++)
        {
            Avalonia.Vector segment = samples[index] - samples[index - 1];
            double lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
            double t = lengthSquared == 0d ? 0d : Math.Clamp(
                (((point.X - samples[index - 1].X) * segment.X) + ((point.Y - samples[index - 1].Y) * segment.Y)) / lengthSquared,
                0d,
                1d);
            Point nearest = samples[index - 1] + (segment * t);
            closest = Math.Min(closest, Distance(point, nearest));
        }
        return closest;
    }

    private static double Distance(Point first, Point second)
    {
        Avalonia.Vector delta = first - second;
        return Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
    }

    private static string AxisName(RotationGizmoAxis axis) => axis switch
    {
        RotationGizmoAxis.LocalX => "Local X",
        RotationGizmoAxis.LocalY => "Local Y",
        RotationGizmoAxis.LocalZ => "Local Z",
        RotationGizmoAxis.View => "View Roll",
        _ => string.Empty,
    };
}
