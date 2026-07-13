using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging;

/// <summary>
/// Imports static Aseprite PNG exports as a fixed horizontal sheet or six explicit face files.
/// </summary>
public sealed class AsepriteSpriteSheetImporter
{
    private static readonly VoxelFace[] HorizontalOrder =
    [
        VoxelFace.Front,
        VoxelFace.Right,
        VoxelFace.Back,
        VoxelFace.Left,
        VoxelFace.Top,
        VoxelFace.Bottom,
    ];

    /// <summary>
    /// Imports a horizontal 6x1 PNG in Front, Right, Back, Left, Top, Bottom order.
    /// </summary>
    public SixViewImportResult ImportHorizontalSheet(string path)
    {
        string fullPath = ValidatePngPath(path);
        using Image<ImageSharpColor> sheet = Image.Load<ImageSharpColor>(fullPath);

        if (sheet.Width % HorizontalOrder.Length != 0)
        {
            throw new InvalidDataException(
                $"The sheet width {sheet.Width} is not divisible by six.");
        }

        int slotWidth = sheet.Width / HorizontalOrder.Length;
        if (slotWidth <= 0)
        {
            throw new InvalidDataException("The sheet slots have no width.");
        }

        Dictionary<VoxelFace, OrthographicImage> views = [];
        List<SixViewSlotInfo> slots = [];

        for (int slotIndex = 0; slotIndex < HorizontalOrder.Length; slotIndex++)
        {
            VoxelFace face = HorizontalOrder[slotIndex];
            (OrthographicImage image, int opaquePixelCount) = ReadRegion(
                sheet,
                slotIndex * slotWidth,
                0,
                slotWidth,
                sheet.Height,
                face);
            views.Add(face, image);
            slots.Add(new SixViewSlotInfo(
                face,
                slotWidth,
                sheet.Height,
                opaquePixelCount,
                fullPath));
        }

        return new SixViewImportResult(fullPath, new OrthographicViewSet(views), slots);
    }

    /// <summary>
    /// Imports six explicitly assigned PNG files that share an identical canvas size.
    /// </summary>
    public SixViewImportResult ImportSeparate(
        IReadOnlyDictionary<VoxelFace, string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        VoxelFace[] missingFaces = HorizontalOrder.Where(face => !paths.ContainsKey(face)).ToArray();
        if (missingFaces.Length > 0 || paths.Count != HorizontalOrder.Length)
        {
            throw new ArgumentException(
                $"Exactly six face paths are required. Missing: {string.Join(", ", missingFaces)}.",
                nameof(paths));
        }

        Dictionary<VoxelFace, OrthographicImage> views = [];
        List<SixViewSlotInfo> slots = [];
        int? commonWidth = null;
        int? commonHeight = null;

        foreach (VoxelFace face in HorizontalOrder)
        {
            string fullPath = ValidatePngPath(paths[face]);
            using Image<ImageSharpColor> source = Image.Load<ImageSharpColor>(fullPath);

            commonWidth ??= source.Width;
            commonHeight ??= source.Height;
            if (source.Width != commonWidth || source.Height != commonHeight)
            {
                throw new InvalidDataException(
                    $"The {face} image is {source.Width}x{source.Height}; " +
                    $"the common canvas is {commonWidth}x{commonHeight}.");
            }

            (OrthographicImage image, int opaquePixelCount) = ReadRegion(
                source,
                0,
                0,
                source.Width,
                source.Height,
                face);
            views.Add(face, image);
            slots.Add(new SixViewSlotInfo(
                face,
                source.Width,
                source.Height,
                opaquePixelCount,
                fullPath));
        }

        return new SixViewImportResult(
            "Six separate PNG files",
            new OrthographicViewSet(views),
            slots);
    }

    private static string ValidatePngPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The six-view PNG input was not found.", fullPath);
        }

        if (!Path.GetExtension(fullPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only static PNG inputs are supported.");
        }

        return fullPath;
    }

    private static (OrthographicImage Image, int OpaquePixelCount) ReadRegion(
        Image<ImageSharpColor> source,
        int startX,
        int startY,
        int width,
        int height,
        VoxelFace face)
    {
        Rgba32Color[] pixels = new Rgba32Color[checked(width * height)];
        int opaquePixelCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ImageSharpColor sourceColor = source[startX + x, startY + y];
                if (sourceColor.A is > 0 and < byte.MaxValue)
                {
                    throw new InvalidDataException(
                        $"The {face} view contains non-binary alpha {sourceColor.A} at ({x}, {y}).");
                }

                if (sourceColor.A == byte.MaxValue)
                {
                    opaquePixelCount++;
                }

                pixels[(y * width) + x] = new Rgba32Color(
                    sourceColor.R,
                    sourceColor.G,
                    sourceColor.B,
                    sourceColor.A);
            }
        }

        if (opaquePixelCount == 0)
        {
            throw new InvalidDataException($"The {face} view contains no opaque pixels.");
        }

        return (new OrthographicImage(width, height, pixels), opaquePixelCount);
    }
}
