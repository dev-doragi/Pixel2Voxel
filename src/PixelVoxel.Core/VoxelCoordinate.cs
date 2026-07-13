namespace PixelVoxel.Core;

/// <summary>
/// Identifies an integer voxel position while axis orientation remains unspecified.
/// </summary>
/// <param name="X">The coordinate on the unresolved X axis.</param>
/// <param name="Y">The coordinate on the unresolved Y axis.</param>
/// <param name="Z">The coordinate on the unresolved Z axis.</param>
public readonly record struct VoxelCoordinate(int X, int Y, int Z);

