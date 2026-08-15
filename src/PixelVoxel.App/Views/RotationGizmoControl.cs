using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PixelVoxel.App.ViewModels;
using PixelVoxel.Rendering;

namespace PixelVoxel.App.Views;

public sealed class RotationGizmoControl : Control
{
    private const double OuterRadius = 48d;
    private const double AxisRadius = 34d;
    private static readonly IBrush XBrush = Brushes.Red;
    private static readonly IBrush YBrush = Brushes.LimeGreen;
    private static readonly IBrush ZBrush = Brushes.DodgerBlue;

    public RotationGizmoAxis ActiveAxis { get; private set; }
    public RotationGizmoAxis HoverAxis { get; private set; }
    public float DragDegrees { get; private set; }
    private IReadOnlyList<Point>? _dragRingPoints;

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
        List<(RotationGizmoAxis Axis, double Distance)> candidates =
            [(RotationGizmoAxis.View, Math.Abs(outerDistance - OuterRadius))];
        foreach (RotationGizmoAxis axis in new[] { RotationGizmoAxis.LocalX, RotationGizmoAxis.LocalY, RotationGizmoAxis.LocalZ })
        {
            IReadOnlyList<Point> samples = GetRingPoints(center, viewModel.CurrentRenderTransform, axis);
            candidates.Add((axis, DistanceToPolyline(point, samples)));
        }
        (RotationGizmoAxis selectedAxis, double distance) = candidates.MinBy(candidate => candidate.Distance);
        return distance <= 7d ? selectedAxis : RotationGizmoAxis.None;
    }

    public void SetDrag(RotationGizmoAxis axis, float degrees)
    {
        if (ActiveAxis == RotationGizmoAxis.None && axis != RotationGizmoAxis.None &&
            DataContext is MainWindowViewModel viewModel)
        {
            Point center = new(Bounds.Width / 2d, Bounds.Height / 2d);
            _dragRingPoints = GetRingPoints(center, viewModel.CurrentRenderTransform, axis);
        }
        ActiveAxis = axis;
        DragDegrees = degrees;
        InvalidateVisual();
    }

    public void ClearDrag()
    {
        ActiveAxis = RotationGizmoAxis.None;
        DragDegrees = 0f;
        _dragRingPoints = null;
        InvalidateVisual();
    }

    public void SetHover(RotationGizmoAxis axis)
    {
        if (HoverAxis == axis) return;
        HoverAxis = axis;
        InvalidateVisual();
    }

    public float GetDragDegrees(RotationGizmoAxis axis, Point previous, Point current)
    {
        Point center = new(Bounds.Width / 2d, Bounds.Height / 2d);
        if (axis == RotationGizmoAxis.View)
        {
            double first = Math.Atan2(previous.Y - center.Y, previous.X - center.X);
            double second = Math.Atan2(current.Y - center.Y, current.X - center.X);
            double delta = (second - first) * 180d / Math.PI;
            if (delta > 180d) delta -= 360d;
            if (delta < -180d) delta += 360d;
            // Screen coordinates grow downward, so invert the geometric pointer angle
            // to match the visible positive rotation direction of the gizmo ring.
            return (float)-delta;
        }

        if (DataContext is not MainWindowViewModel viewModel) return 0f;
        IReadOnlyList<Point> samples = _dragRingPoints ??
            GetRingPoints(center, viewModel.CurrentRenderTransform, axis);
        int segmentIndex = ClosestSegment(previous, samples);
        Avalonia.Vector tangent = samples[segmentIndex + 1] - samples[segmentIndex];
        double length = Math.Sqrt((tangent.X * tangent.X) + (tangent.Y * tangent.Y));
        if (length < 0.001d) return 0f;
        Avalonia.Vector movement = current - previous;
        double alongRing = ((movement.X * tangent.X) + (movement.Y * tangent.Y)) / length;
        return (float)(-alongRing * 180d / (Math.PI * AxisRadius));
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
        new(brush, ActiveAxis == axis ? 4d : HoverAxis == axis ? 3.25d : 2d);

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

    private static int ClosestSegment(Point point, IReadOnlyList<Point> samples)
    {
        int closestIndex = 0;
        double closestDistance = double.PositiveInfinity;
        for (int index = 0; index < samples.Count - 1; index++)
        {
            Point midpoint = new((samples[index].X + samples[index + 1].X) / 2d, (samples[index].Y + samples[index + 1].Y) / 2d);
            double distance = Distance(point, midpoint);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestIndex = index;
        }
        return closestIndex;
    }

    private static double Distance(Point first, Point second)
    {
        Avalonia.Vector delta = first - second;
        return Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
    }

    private static string AxisName(RotationGizmoAxis axis) => axis switch
    {
        RotationGizmoAxis.LocalX => "Pitch (X)",
        RotationGizmoAxis.LocalY => "Yaw (Y)",
        RotationGizmoAxis.LocalZ => "Roll (Z)",
        RotationGizmoAxis.View => "Screen Roll",
        _ => string.Empty,
    };
}
