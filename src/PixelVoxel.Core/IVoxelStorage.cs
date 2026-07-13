namespace PixelVoxel.Core;

/// <summary>
/// Defines the boundary for voxel storage without selecting a dense or sparse representation.
/// </summary>
public interface IVoxelStorage
{
    /// <summary>
    /// Gets the bounds of the reconstructed voxel volume.
    /// </summary>
    VoxelDimensions Dimensions { get; }

    /// <summary>
    /// Gets the number of occupied cells.
    /// </summary>
    int OccupiedCount { get; }

    /// <summary>
    /// Tries to get an occupied cell at a coordinate.
    /// </summary>
    /// <param name="coordinate">The coordinate to query.</param>
    /// <param name="cell">The occupied cell when found.</param>
    /// <returns><see langword="true" /> when the coordinate is occupied.</returns>
    bool TryGetCell(VoxelCoordinate coordinate, out VoxelCell? cell);

    /// <summary>
    /// Enumerates occupied cells without exposing the underlying storage layout.
    /// </summary>
    /// <returns>The occupied voxel entries.</returns>
    IEnumerable<VoxelEntry> GetOccupiedCells();
}
