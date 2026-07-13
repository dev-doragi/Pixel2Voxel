namespace PixelVoxel.Core;

internal sealed class ReconstructedVoxelStorage : IVoxelStorage
{
    private readonly Dictionary<VoxelCoordinate, VoxelCell> _cells;

    public ReconstructedVoxelStorage(
        VoxelDimensions dimensions,
        Dictionary<VoxelCoordinate, VoxelCell> cells)
    {
        Dimensions = dimensions;
        _cells = cells;
    }

    public VoxelDimensions Dimensions { get; }

    public int OccupiedCount => _cells.Count;

    public bool TryGetCell(VoxelCoordinate coordinate, out VoxelCell? cell) =>
        _cells.TryGetValue(coordinate, out cell);

    public IEnumerable<VoxelEntry> GetOccupiedCells()
    {
        foreach ((VoxelCoordinate coordinate, VoxelCell cell) in _cells)
        {
            yield return new VoxelEntry(coordinate, cell);
        }
    }
}

