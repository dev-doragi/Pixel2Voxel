using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>Reports the first occupied voxel and exposed face reached by a viewport ray.</summary>
public sealed record VoxelPickResult(
    VoxelCoordinate Coordinate,
    VoxelFace Face,
    VoxelCoordinate AdjacentCoordinate,
    float Distance);

/// <summary>Traverses the voxel grid without enumerating every occupied cell.</summary>
public sealed class VoxelPicker
{
    private const float DirectionEpsilon = 0.000001f;
    private const float EntryEpsilon = 0.0001f;

    /// <summary>Picks the first occupied voxel under one logical framebuffer position.</summary>
    public VoxelPickResult? Pick(
        VoxelDocument document,
        VoxelRenderTransform transform,
        Vector2 logicalPosition)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(transform);
        if (!float.IsFinite(logicalPosition.X) || !float.IsFinite(logicalPosition.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(logicalPosition));
        }

        if (!Matrix4x4.Invert(transform.CombinedRotation, out Matrix4x4 inverseRotation))
        {
            throw new InvalidOperationException("The resolved camera transform cannot be inverted.");
        }

        VoxelDimensions dimensions = document.Storage.Dimensions;
        float diagonal = MathF.Max(
            1f,
            MathF.Sqrt(
                ((float)dimensions.Width * dimensions.Width) +
                ((float)dimensions.Height * dimensions.Height) +
                ((float)dimensions.Depth * dimensions.Depth)));
        Vector3 viewOrigin = new(
            (logicalPosition.X - transform.ScreenOffsetX) / transform.PixelsPerVoxel,
            (transform.ScreenOffsetY - logicalPosition.Y) / transform.PixelsPerVoxel,
            diagonal * 2f);
        Vector3 origin = transform.ModelCenter + Vector3.Transform(viewOrigin, inverseRotation);
        Vector3 direction = Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, inverseRotation));

        if (!TryIntersectBounds(origin, direction, dimensions, out float entry, out float exit, out VoxelFace entryFace))
        {
            return null;
        }

        float distance = MathF.Max(0f, entry) + EntryEpsilon;
        Vector3 point = origin + (direction * distance);
        VoxelCoordinate coordinate = new(
            ClampCell(point.X, dimensions.Width),
            ClampCell(point.Y, dimensions.Height),
            ClampCell(point.Z, dimensions.Depth));
        ConfigureAxis(point.X, coordinate.X, direction.X, out int stepX, out float maxX, out float deltaX);
        ConfigureAxis(point.Y, coordinate.Y, direction.Y, out int stepY, out float maxY, out float deltaY);
        ConfigureAxis(point.Z, coordinate.Z, direction.Z, out int stepZ, out float maxZ, out float deltaZ);
        maxX += distance;
        maxY += distance;
        maxZ += distance;
        VoxelFace face = entryFace;

        while (distance <= exit + EntryEpsilon && EditableContains(dimensions, coordinate))
        {
            if (document.Storage.TryGetCell(coordinate, out _))
            {
                return new VoxelPickResult(
                    coordinate,
                    face,
                    OffsetForFace(coordinate, face),
                    distance);
            }

            if (maxX <= maxY && maxX <= maxZ)
            {
                coordinate = coordinate with { X = coordinate.X + stepX };
                distance = maxX;
                maxX += deltaX;
                face = stepX > 0 ? VoxelFace.Left : VoxelFace.Right;
            }
            else if (maxY <= maxZ)
            {
                coordinate = coordinate with { Y = coordinate.Y + stepY };
                distance = maxY;
                maxY += deltaY;
                face = stepY > 0 ? VoxelFace.Bottom : VoxelFace.Top;
            }
            else
            {
                coordinate = coordinate with { Z = coordinate.Z + stepZ };
                distance = maxZ;
                maxZ += deltaZ;
                face = stepZ > 0 ? VoxelFace.Back : VoxelFace.Front;
            }
        }

        return null;
    }

    private static bool TryIntersectBounds(
        Vector3 origin,
        Vector3 direction,
        VoxelDimensions dimensions,
        out float entry,
        out float exit,
        out VoxelFace entryFace)
    {
        entry = float.NegativeInfinity;
        exit = float.PositiveInfinity;
        entryFace = VoxelFace.Front;
        if (!IntersectAxis(origin.X, direction.X, 0f, dimensions.Width, VoxelFace.Left, VoxelFace.Right, ref entry, ref exit, ref entryFace) ||
            !IntersectAxis(origin.Y, direction.Y, 0f, dimensions.Height, VoxelFace.Bottom, VoxelFace.Top, ref entry, ref exit, ref entryFace) ||
            !IntersectAxis(origin.Z, direction.Z, 0f, dimensions.Depth, VoxelFace.Back, VoxelFace.Front, ref entry, ref exit, ref entryFace))
        {
            return false;
        }

        return exit >= MathF.Max(entry, 0f);
    }

    private static bool IntersectAxis(
        float origin,
        float direction,
        float minimum,
        float maximum,
        VoxelFace minimumFace,
        VoxelFace maximumFace,
        ref float entry,
        ref float exit,
        ref VoxelFace entryFace)
    {
        if (MathF.Abs(direction) < DirectionEpsilon)
        {
            return origin >= minimum && origin <= maximum;
        }

        float first = (minimum - origin) / direction;
        float second = (maximum - origin) / direction;
        VoxelFace nearFace = minimumFace;
        if (first > second)
        {
            (first, second) = (second, first);
            nearFace = maximumFace;
        }

        if (first > entry)
        {
            entry = first;
            entryFace = nearFace;
        }

        exit = MathF.Min(exit, second);
        return entry <= exit;
    }

    private static void ConfigureAxis(
        float point,
        int coordinate,
        float direction,
        out int step,
        out float maximum,
        out float delta)
    {
        if (direction > DirectionEpsilon)
        {
            step = 1;
            maximum = ((coordinate + 1f) - point) / direction;
            delta = 1f / direction;
        }
        else if (direction < -DirectionEpsilon)
        {
            step = -1;
            maximum = (coordinate - point) / direction;
            delta = -1f / direction;
        }
        else
        {
            step = 0;
            maximum = float.PositiveInfinity;
            delta = float.PositiveInfinity;
        }
    }

    private static int ClampCell(float value, int size) =>
        Math.Clamp((int)MathF.Floor(value), 0, size - 1);

    private static bool EditableContains(VoxelDimensions dimensions, VoxelCoordinate coordinate) =>
        coordinate.X >= 0 && coordinate.X < dimensions.Width &&
        coordinate.Y >= 0 && coordinate.Y < dimensions.Height &&
        coordinate.Z >= 0 && coordinate.Z < dimensions.Depth;

    private static VoxelCoordinate OffsetForFace(VoxelCoordinate coordinate, VoxelFace face) =>
        face switch
        {
            VoxelFace.Front => coordinate with { Z = coordinate.Z + 1 },
            VoxelFace.Back => coordinate with { Z = coordinate.Z - 1 },
            VoxelFace.Right => coordinate with { X = coordinate.X + 1 },
            VoxelFace.Left => coordinate with { X = coordinate.X - 1 },
            VoxelFace.Top => coordinate with { Y = coordinate.Y + 1 },
            VoxelFace.Bottom => coordinate with { Y = coordinate.Y - 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(face)),
        };
}
