namespace PixelVoxel.Core;

/// <summary>
/// Provides the single coordinate-transform table used by reconstruction and projection.
/// </summary>
public static class FaceCoordinateTransforms
{
    private static readonly IReadOnlyDictionary<VoxelFace, FaceCoordinateTransform> Transforms =
        new Dictionary<VoxelFace, FaceCoordinateTransform>
        {
            [VoxelFace.Front] = new(VoxelFace.Front, Axis.X, Axis.Y, false, true),
            [VoxelFace.Back] = new(VoxelFace.Back, Axis.X, Axis.Y, true, true),
            [VoxelFace.Right] = new(VoxelFace.Right, Axis.Z, Axis.Y, true, true),
            [VoxelFace.Left] = new(VoxelFace.Left, Axis.Z, Axis.Y, false, true),
            [VoxelFace.Top] = new(VoxelFace.Top, Axis.X, Axis.Z, false, false),
            [VoxelFace.Bottom] = new(VoxelFace.Bottom, Axis.X, Axis.Z, false, true),
        };

    /// <summary>Gets the coordinate transform for an orthographic face.</summary>
    public static FaceCoordinateTransform Get(VoxelFace face) =>
        Transforms.TryGetValue(face, out FaceCoordinateTransform transform)
            ? transform
            : throw new ArgumentOutOfRangeException(nameof(face));
}
