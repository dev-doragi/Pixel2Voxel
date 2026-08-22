namespace PixelVoxel.Core;

/// <summary>
/// Builds a visual-hull voxel volume from the intersection of supplied non-transparent silhouettes.
/// </summary>
public sealed class VisualHullVoxelReconstructor : IVoxelReconstructor
{
    /// <summary>The largest candidate volume accepted by the reconstruction pass.</summary>
    public const long MaximumCandidateCellCount = 1_048_576;

    /// <inheritdoc />
    public VoxelDocument Reconstruct(OrthographicViewSet views) =>
        Reconstruct(views, VoxelReconstructionOptions.UnitFallback);

    /// <inheritdoc />
    public VoxelDocument Reconstruct(OrthographicViewSet views, VoxelReconstructionOptions options)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        ValidateCommonCanvas(views);
        ReconstructionSpace space = ResolveSpace(views, options);
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

                        if (color.Alpha == 0)
                        {
                            occupied = false;
                            break;
                        }

                        colors[face] = color;
                    }

                    if (occupied)
                    {
                        if (colors.Count < Enum.GetValues<VoxelFace>().Length)
                        {
                            Rgba32Color fallback = AveragePremultiplied(colors.Values);
                            foreach (VoxelFace missingFace in Enum.GetValues<VoxelFace>().Where(face => !colors.ContainsKey(face)))
                            {
                                colors[missingFace] = fallback;
                            }
                        }

                        cells[coordinate] = new VoxelCell(colors);
                    }
                }
            }
        }

        return new VoxelDocument(new ReconstructedVoxelStorage(space.Dimensions, cells));
    }

    private static Rgba32Color AveragePremultiplied(IEnumerable<Rgba32Color> colors)
    {
        Rgba32Color[] source = colors.ToArray();
        if (source.Length == 0) throw new InvalidOperationException("An occupied voxel must have a source color.");
        double alpha = source.Average(color => color.Alpha / 255d);
        if (alpha <= 0d) return default;
        byte Channel(Func<Rgba32Color, byte> select) =>
            (byte)Math.Clamp((int)Math.Round(
                source.Average(color => select(color) * (color.Alpha / 255d)) / alpha,
                MidpointRounding.AwayFromZero), 0, 255);
        return new Rgba32Color(
            Channel(color => color.Red),
            Channel(color => color.Green),
            Channel(color => color.Blue),
            (byte)Math.Clamp((int)Math.Round(alpha * 255d, MidpointRounding.AwayFromZero), 0, 255));
    }

    internal static void ValidateCommonCanvas(OrthographicViewSet views)
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

    internal static ReconstructionSpace ResolveSpace(
        OrthographicViewSet views,
        VoxelReconstructionOptions options)
    {
        AxisExtent x = new();
        AxisExtent y = new();
        AxisExtent z = new();

        foreach ((VoxelFace face, OrthographicImage image) in views.Views)
        {
            FaceCoordinateTransform transform = FaceCoordinateTransforms.Get(face);
            bool hasVisiblePixel = false;

            for (int imageY = 0; imageY < image.Height; imageY++)
            {
                for (int imageX = 0; imageX < image.Width; imageX++)
                {
                    Rgba32Color color = image.GetPixel(imageX, imageY);
                    if (color.Alpha == 0)
                    {
                        continue;
                    }

                    hasVisiblePixel = true;
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

            if (!hasVisiblePixel)
            {
                throw new ArgumentException($"The {face} view contains no visible pixels.", nameof(views));
            }
        }

        return new ReconstructionSpace(
            new VoxelDimensions(
                x.ResolveLength(options.UnobservedXLength),
                y.ResolveLength(options.UnobservedYLength),
                z.ResolveLength(options.UnobservedZLength)),
            x.Minimum,
            y.Minimum,
            z.Minimum);
    }

    internal static (int X, int Y) ProjectToImage(
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

        public int ResolveLength(int unobservedLength) =>
            _minimum == int.MaxValue ? unobservedLength : Length;

        public void Include(int value)
        {
            _minimum = Math.Min(_minimum, value);
            _maximum = Math.Max(_maximum, value);
        }
    }

    internal sealed record ReconstructionSpace(
        VoxelDimensions Dimensions,
        int XMinimum,
        int YMinimum,
        int ZMinimum);
}
