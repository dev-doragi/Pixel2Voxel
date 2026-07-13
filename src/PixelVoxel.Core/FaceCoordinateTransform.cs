namespace PixelVoxel.Core;

/// <summary>
/// Maps image-space horizontal and vertical coordinates onto model-space axes.
/// </summary>
/// <param name="Face">The orthographic face using the transform.</param>
/// <param name="HorizontalAxis">The model axis represented by image U.</param>
/// <param name="VerticalAxis">The model axis represented by image V.</param>
/// <param name="FlipHorizontal">Whether image U runs opposite to the model axis.</param>
/// <param name="FlipVertical">Whether image V runs opposite to the model axis.</param>
public readonly record struct FaceCoordinateTransform(
    VoxelFace Face,
    Axis HorizontalAxis,
    Axis VerticalAxis,
    bool FlipHorizontal,
    bool FlipVertical);
