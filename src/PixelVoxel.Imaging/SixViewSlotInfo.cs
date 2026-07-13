using PixelVoxel.Core;

namespace PixelVoxel.Imaging;

/// <summary>
/// Describes one validated orthographic image slot without retaining ImageSharp data.
/// </summary>
/// <param name="Face">The face assigned to the slot.</param>
/// <param name="Width">The slot width.</param>
/// <param name="Height">The slot height.</param>
/// <param name="OpaquePixelCount">The number of pixels whose alpha is 255.</param>
/// <param name="SourcePath">The PNG containing the slot.</param>
public sealed record SixViewSlotInfo(
    VoxelFace Face,
    int Width,
    int Height,
    int OpaquePixelCount,
    string SourcePath);
