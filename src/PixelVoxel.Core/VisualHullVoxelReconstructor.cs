namespace PixelVoxel.Core;

/// <summary>
/// Builds a visual-hull voxel volume from the intersection of supplied opaque silhouettes.
/// </summary>
public sealed class VisualHullVoxelReconstructor : IVoxelReconstructor
{
    /// <summary>The largest candidate volume accepted by the reconstruction pass.</summary>
    public const long MaximumCandidateCellCount = 1_048_576;

    /// <inheritdoc />
    public VoxelDocument Reconstruct(OrthographicViewSet views)
    {
        ArgumentNullException.ThrowIfNull(views);

        ValidateCommonCanvas(views);
        ReconstructionSpace space = ResolveSpace(views);
        long candidateCount = checked(
            (long)space.Dimensions.Width *
            space.Dimensions.Height *
            space.Dimensions.Depth);

        if (candidateCount > MaximumCandidateCellCount)
        {
            throw new InvalidOperationException(
                $"The inferred volume contains {candidateCount:N0} candidate cells; " +
                $"the limit is {MaximumCandidateCellCount:N0}.");
        }

        Dictionary<VoxelCoordinate, VoxelCell> cells = [];

        for (int z = 0; z < space.Dimensions.Depth; z++)
        {
            for (int y = 0; y < space.Dimensions.Height; y++)
            {
                for (int x = 0; x < space.Dimensions.Width; x++)
                {
                    VoxelCoordinate coordinate = new(x, y, z);
                    Dictionary<VoxelFace, Rgba32Color> colors = [];
                    bool occupied = true;

                    foreach ((VoxelFace face, OrthographicImage image) in views.Views)
                    {
                        (int imageX, int imageY) = ProjectToImage(face, image, coordinate, space);
                        Rgba32Color color = image.GetPixel(imageX, imageY);

                        if (color.Alpha != byte.MaxValue)
                        {
                            occupied = false;
                            break;
                        }

                        colors[face] = color;
                    }

                    if (occupied)
                    {
                        cells[coordinate] = new VoxelCell(colors);
                    }
                }
            }
        }

        return new VoxelDocument(new ReconstructedVoxelStorage(space.Dimensions, cells));
    }

    private static void ValidateCommonCanvas(OrthographicViewSet views)
    {
        OrthographicImage first = views.Views.First().Value;

        foreach ((VoxelFace face, OrthographicImage image) in views.Views)
        {
            if (image.Width != first.Width || image.Height != first.Height)
            {
                throw new ArgumentException(
                    $"The {face} view must use the common {first.Width}x{first.Height} canvas.",
                    nameof(views));
            }
        }
    }

    private static ReconstructionSpace ResolveSpace(OrthographicViewSet views)
    {
        AxisExtent x = new();
        AxisExtent y = new();
        AxisExtent z = new();

        foreach ((VoxelFace face, OrthographicImage image) in views.Views)
        {
            FaceCoordinateTransform transform = FaceCoordinateTransforms.Get(face);
            bool hasOpaquePixel = false;

            for (int imageY = 0; imageY < image.Height; imageY++)
            {
                for (int imageX = 0; imageX < image.Width; imageX++)
                {
                    Rgba32Color color = image.GetPixel(imageX, imageY);
                    if (color.Alpha is > 0 and < byte.MaxValue)
                    {
                        throw new ArgumentException(
                            $"The {face} view contains non-binary alpha {color.Alpha} at " +
                            $"({imageX}, {imageY}).",
                            nameof(views));
                    }

                    if (color.Alpha == 0)
                    {
                        continue;
                    }

                    hasOpaquePixel = true;
                    int horizontal = transform.FlipHorizontal
                        ? image.Width - 1 - imageX
                        : imageX;
                    int vertical = transform.FlipVertical
                        ? image.Height - 1 - imageY
                        : imageY;

                    GetExtent(transform.HorizontalAxis, x, y, z).Include(horizontal);
                    GetExtent(transform.VerticalAxis, x, y, z).Include(vertical);
                }
            }

            if (!hasOpaquePixel)
            {
                throw new ArgumentException($"The {face} view contains no opaque pixels.", nameof(views));
            }
        }

        return new ReconstructionSpace(
            new VoxelDimensions(x.Length, y.Length, z.Length),
            x.Minimum,
            y.Minimum,
            z.Minimum);
    }

    private static (int X, int Y) ProjectToImage(
        VoxelFace face,
        OrthographicImage image,
        VoxelCoordinate coordinate,
        ReconstructionSpace space)
    {
        FaceCoordinateTransform transform = FaceCoordinateTransforms.Get(face);
        int horizontal = GetAxisValue(transform.HorizontalAxis, coordinate, space);
        int vertical = GetAxisValue(transform.VerticalAxis, coordinate, space);

        return (
            transform.FlipHorizontal ? image.Width - 1 - horizontal : horizontal,
            transform.FlipVertical ? image.Height - 1 - vertical : vertical);
    }

    private static int GetAxisValue(
        Axis axis,
        VoxelCoordinate coordinate,
        ReconstructionSpace space) => axis switch
    {
        Axis.X => space.XMinimum + coordinate.X,
        Axis.Y => space.YMinimum + coordinate.Y,
        Axis.Z => space.ZMinimum + coordinate.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };

    private static AxisExtent GetExtent(
        Axis axis,
        AxisExtent x,
        AxisExtent y,
        AxisExtent z) => axis switch
    {
        Axis.X => x,
        Axis.Y => y,
        Axis.Z => z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };

    private sealed class AxisExtent
    {
        private int _minimum = int.MaxValue;
        private int _maximum = int.MinValue;

        public int Minimum => _minimum == int.MaxValue ? 0 : _minimum;

        public int Length => _minimum == int.MaxValue ? 1 : checked(_maximum - _minimum + 1);

        public void Include(int value)
        {
            _minimum = Math.Min(_minimum, value);
            _maximum = Math.Max(_maximum, value);
        }
    }

    private sealed record ReconstructionSpace(
        VoxelDimensions Dimensions,
        int XMinimum,
        int YMinimum,
        int ZMinimum);
}
