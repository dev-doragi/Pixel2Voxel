namespace PixelVoxel.Core;

/// <summary>
/// Describes the three integer extents of a voxel document without assigning world-axis semantics.
/// </summary>
/// <param name="Width">The first document extent.</param>
/// <param name="Height">The second document extent.</param>
/// <param name="Depth">The third document extent.</param>
public readonly record struct VoxelDimensions(int Width, int Height, int Depth);

