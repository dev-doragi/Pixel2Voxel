namespace PixelVoxel.Core;

internal sealed class EditableVoxelStorage : IVoxelStorage
{
    private Dictionary<VoxelCoordinate, VoxelCell> _cells;

    public EditableVoxelStorage(VoxelDimensions dimensions, IEnumerable<VoxelEntry> cells)
    {
        ValidateDimensions(dimensions);
        Dimensions = dimensions;
        _cells = [];
        foreach (VoxelEntry entry in cells)
        {
            if (!Contains(dimensions, entry.Coordinate))
            {
                throw new ArgumentException($"Voxel {entry.Coordinate} is outside the document bounds.", nameof(cells));
            }

            if (!_cells.TryAdd(entry.Coordinate, entry.Cell))
            {
                throw new ArgumentException($"Voxel {entry.Coordinate} is duplicated.", nameof(cells));
            }
        }
    }

    public VoxelDimensions Dimensions { get; private set; }

    public int OccupiedCount => _cells.Count;

    public bool TryGetCell(VoxelCoordinate coordinate, out VoxelCell? cell) =>
        _cells.TryGetValue(coordinate, out cell);

    public IEnumerable<VoxelEntry> GetOccupiedCells() =>
        _cells.Select(pair => new VoxelEntry(pair.Key, pair.Value));

    public void Apply(VoxelChangeSet changeSet)
    {
        if (Dimensions != changeSet.BeforeDimensions)
        {
            throw new InvalidOperationException("The change set was created for different document dimensions.");
        }

        ValidateDimensions(changeSet.AfterDimensions);
        Dictionary<VoxelCoordinate, VoxelCell> updated = new(_cells);
        HashSet<VoxelCoordinate> visited = [];
        foreach (VoxelChange change in changeSet.Changes)
        {
            if (!visited.Add(change.Coordinate))
            {
                throw new InvalidOperationException($"The change set contains duplicate coordinate {change.Coordinate}.");
            }

            updated.TryGetValue(change.Coordinate, out VoxelCell? current);
            if (!Equals(current, change.Before))
            {
                throw new InvalidOperationException($"Voxel {change.Coordinate} no longer matches the expected edit state.");
            }

            if (change.After is null)
            {
                updated.Remove(change.Coordinate);
            }
            else
            {
                if (!Contains(changeSet.AfterDimensions, change.Coordinate))
                {
                    throw new InvalidOperationException($"Voxel {change.Coordinate} is outside the resized document bounds.");
                }

                updated[change.Coordinate] = change.After;
            }
        }

        VoxelCoordinate? invalid = updated.Keys.FirstOrDefault(
            coordinate => !Contains(changeSet.AfterDimensions, coordinate));
        if (invalid.HasValue && !Contains(changeSet.AfterDimensions, invalid.Value))
        {
            throw new InvalidOperationException($"Voxel {invalid.Value} would be clipped by the dimension change.");
        }

        _cells = updated;
        Dimensions = changeSet.AfterDimensions;
    }

    internal static bool Contains(VoxelDimensions dimensions, VoxelCoordinate coordinate) =>
        coordinate.X >= 0 && coordinate.X < dimensions.Width &&
        coordinate.Y >= 0 && coordinate.Y < dimensions.Height &&
        coordinate.Z >= 0 && coordinate.Z < dimensions.Depth;

    internal static void ValidateDimensions(VoxelDimensions dimensions)
    {
        if (dimensions.Width <= 0 || dimensions.Height <= 0 || dimensions.Depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Voxel dimensions must be positive.");
        }

        _ = checked((long)dimensions.Width * dimensions.Height * dimensions.Depth);
    }
}
