namespace PixelVoxel.Core;

/// <summary>
/// Pairs an occupied coordinate with its reconstructed cell data.
/// </summary>
/// <param name="Coordinate">The occupied coordinate.</param>
/// <param name="Cell">The reconstructed cell.</param>
public readonly record struct VoxelEntry(VoxelCoordinate Coordinate, VoxelCell Cell);

