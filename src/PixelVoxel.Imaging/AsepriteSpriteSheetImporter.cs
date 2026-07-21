using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging;

/// <summary>
/// Inspects and imports static Aseprite PNG exports as a horizontal sheet or explicit face files.
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

    /// <summary>Inspects a horizontal 6x1 sheet without rejecting fixable pixel diagnostics.</summary>
    public SixViewImportDraft InspectHorizontalSheet(string path)
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

        List<SixViewSourceSlot> slots = [];
        for (int slotIndex = 0; slotIndex < HorizontalOrder.Length; slotIndex++)
        {
            VoxelFace face = HorizontalOrder[slotIndex];
            slots.Add(ReadRegion(
                sheet,
                slotIndex * slotWidth,
                0,
                slotWidth,
                sheet.Height,
                slotIndex,
                fullPath,
                face));
        }

        return new SixViewImportDraft(
            fullPath,
            SixViewImportSourceKind.HorizontalSheet,
            slots);
    }

    /// <summary>Inspects explicitly assigned PNG files and reports missing or mismatched faces.</summary>
    public SixViewImportDraft InspectSeparate(IReadOnlyDictionary<VoxelFace, string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<SixViewSourceSlot> slots = [];
        List<ImportDiagnostic> diagnostics = [];
        int? commonWidth = null;
        int? commonHeight = null;

        foreach (VoxelFace face in HorizontalOrder)
        {
            if (!paths.TryGetValue(face, out string? path))
            {
                diagnostics.Add(new ImportDiagnostic(
                    "missing-face",
                    ImportDiagnosticSeverity.Error,
                    -1,
                    face,
                    null,
                    null,
                    $"The {face} view is missing."));
                continue;
            }

            string fullPath = ValidatePngPath(path);
            using Image<ImageSharpColor> source = Image.Load<ImageSharpColor>(fullPath);
            commonWidth ??= source.Width;
            commonHeight ??= source.Height;
            if (source.Width != commonWidth || source.Height != commonHeight)
            {
                diagnostics.Add(new ImportDiagnostic(
                    "canvas-size-mismatch",
                    ImportDiagnosticSeverity.Error,
                    slots.Count,
                    face,
                    null,
                    null,
                    $"The {face} image is {source.Width}x{source.Height}; " +
                    $"the common canvas is {commonWidth}x{commonHeight}."));
            }

            slots.Add(ReadRegion(
                source,
                0,
                0,
                source.Width,
                source.Height,
                slots.Count,
                fullPath,
                face));
        }

        return new SixViewImportDraft(
            "Six separate PNG files",
            SixViewImportSourceKind.SeparateFiles,
            slots,
            diagnostics);
    }

    /// <summary>Inspects an unordered set of separate PNG files for drag-and-drop assignment.</summary>
    public SixViewImportDraft InspectSeparate(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string[] sourcePaths = paths.ToArray();
        List<SixViewSourceSlot> slots = [];
        List<ImportDiagnostic> diagnostics = [];
        HashSet<VoxelFace> inferredFaces = [];
        int? commonWidth = null;
        int? commonHeight = null;

        if (sourcePaths.Length > HorizontalOrder.Length)
        {
            diagnostics.Add(new ImportDiagnostic(
                "too-many-sources",
                ImportDiagnosticSeverity.Error,
                -1,
                null,
                null,
                null,
                "At most six separate PNG files can be assigned."));
        }

        for (int index = 0; index < Math.Min(sourcePaths.Length, HorizontalOrder.Length); index++)
        {
            string fullPath = ValidatePngPath(sourcePaths[index]);
            using Image<ImageSharpColor> source = Image.Load<ImageSharpColor>(fullPath);
            VoxelFace? suggested = InferFace(fullPath);
            if (suggested is { } inferred && !inferredFaces.Add(inferred))
            {
                suggested = null;
            }

            commonWidth ??= source.Width;
            commonHeight ??= source.Height;
            if (source.Width != commonWidth || source.Height != commonHeight)
            {
                diagnostics.Add(new ImportDiagnostic(
                    "canvas-size-mismatch",
                    ImportDiagnosticSeverity.Error,
                    index,
                    suggested,
                    null,
                    null,
                    $"{Path.GetFileName(fullPath)} is {source.Width}x{source.Height}; " +
                    $"the common canvas is {commonWidth}x{commonHeight}."));
            }

            slots.Add(ReadRegion(
                source,
                0,
                0,
                source.Width,
                source.Height,
                index,
                fullPath,
                suggested));
        }

        return new SixViewImportDraft(
            "Separate PNG files",
            SixViewImportSourceKind.SeparateFiles,
            slots,
            diagnostics);
    }

    /// <summary>Creates a unique best-effort face mapping from source suggestions and source order.</summary>
    public SixViewAlignment CreateDefaultAlignment(SixViewImportDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        HashSet<VoxelFace> used = [];
        List<SixViewFaceAlignment> faces = [];

        foreach (SixViewSourceSlot slot in draft.Slots)
        {
            VoxelFace face = slot.SuggestedFace is { } suggested && used.Add(suggested)
                ? suggested
                : HorizontalOrder.First(candidate => !used.Contains(candidate));
            used.Add(face);
            faces.Add(new SixViewFaceAlignment(slot.SourceIndex, face));
        }

        return new SixViewAlignment(faces);
    }

    /// <summary>Transforms a draft for pixel-perfect preview without reconstructing a voxel model.</summary>
    public SixViewAlignmentPreview PreviewAlignment(
        SixViewImportDraft draft,
        SixViewAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(alignment);
        List<ImportDiagnostic> diagnostics = [.. draft.Diagnostics];
        Dictionary<VoxelFace, OrthographicImage> views = [];
        List<SixViewSlotInfo> slots = [];
        Dictionary<int, SixViewSourceSlot> sources = draft.Slots.ToDictionary(slot => slot.SourceIndex);
        HashSet<int> assignedSources = [];
        HashSet<VoxelFace> assignedFaces = [];

        foreach (SixViewFaceAlignment faceAlignment in alignment.Faces)
        {
            if (!sources.TryGetValue(faceAlignment.SourceSlotIndex, out SixViewSourceSlot? source))
            {
                diagnostics.Add(new ImportDiagnostic(
                    "unknown-source",
                    ImportDiagnosticSeverity.Error,
                    faceAlignment.SourceSlotIndex,
                    faceAlignment.TargetFace,
                    null,
                    null,
                    $"Source slot {faceAlignment.SourceSlotIndex} does not exist."));
                continue;
            }

            if (!assignedSources.Add(faceAlignment.SourceSlotIndex))
            {
                diagnostics.Add(new ImportDiagnostic(
                    "duplicate-source",
                    ImportDiagnosticSeverity.Error,
                    faceAlignment.SourceSlotIndex,
                    faceAlignment.TargetFace,
                    null,
                    null,
                    $"Source slot {faceAlignment.SourceSlotIndex} is assigned more than once."));
                continue;
            }

            if (!assignedFaces.Add(faceAlignment.TargetFace))
            {
                diagnostics.Add(new ImportDiagnostic(
                    "duplicate-face",
                    ImportDiagnosticSeverity.Error,
                    faceAlignment.SourceSlotIndex,
                    faceAlignment.TargetFace,
                    null,
                    null,
                    $"The {faceAlignment.TargetFace} face is assigned more than once."));
                continue;
            }

            (OrthographicImage transformed, int opaqueCount, int clippedOpaqueCount) =
                Transform(source.Image, faceAlignment);
            if (clippedOpaqueCount > 0)
            {
                diagnostics.Add(new ImportDiagnostic(
                    "clipped-opaque-pixels",
                    ImportDiagnosticSeverity.Warning,
                    source.SourceIndex,
                    faceAlignment.TargetFace,
                    null,
                    null,
                    $"The {faceAlignment.TargetFace} offset clips {clippedOpaqueCount:N0} opaque pixels."));
            }

            if (opaqueCount == 0)
            {
                diagnostics.Add(new ImportDiagnostic(
                    "empty-face",
                    ImportDiagnosticSeverity.Error,
                    source.SourceIndex,
                    faceAlignment.TargetFace,
                    null,
                    null,
                    $"The {faceAlignment.TargetFace} view contains no opaque pixels."));
            }

            views.Add(faceAlignment.TargetFace, transformed);
            slots.Add(new SixViewSlotInfo(
                faceAlignment.TargetFace,
                transformed.Width,
                transformed.Height,
                opaqueCount,
                source.SourcePath));
        }

        foreach (VoxelFace missingFace in HorizontalOrder.Where(face => !assignedFaces.Contains(face)))
        {
            diagnostics.Add(new ImportDiagnostic(
                "missing-face",
                ImportDiagnosticSeverity.Error,
                -1,
                missingFace,
                null,
                null,
                $"The {missingFace} view is missing."));
        }

        if (assignedSources.Count != draft.Slots.Count)
        {
            diagnostics.Add(new ImportDiagnostic(
                "unassigned-source",
                ImportDiagnosticSeverity.Error,
                -1,
                null,
                null,
                null,
                "Every source slot must be assigned exactly once."));
        }

        return new SixViewAlignmentPreview(views, slots, diagnostics);
    }

    /// <summary>Applies a valid alignment and produces the strict reconstruction input.</summary>
    public SixViewImportResult ApplyAlignment(
        SixViewImportDraft draft,
        SixViewAlignment alignment)
    {
        SixViewAlignmentPreview preview = PreviewAlignment(draft, alignment);
        ImportDiagnostic? error = preview.Diagnostics.FirstOrDefault(
            item => item.Severity == ImportDiagnosticSeverity.Error);
        if (error is not null)
        {
            throw new InvalidDataException(error.Message);
        }

        return new SixViewImportResult(
            draft.SourcePath,
            new OrthographicViewSet(preview.Views),
            preview.Slots,
            preview.Diagnostics
                .Where(item => item.Severity != ImportDiagnosticSeverity.Error)
                .Select(item => item.Message));
    }

    /// <summary>Imports a horizontal sheet using its default fixed order.</summary>
    public SixViewImportResult ImportHorizontalSheet(string path)
    {
        SixViewImportDraft draft = InspectHorizontalSheet(path);
        return ApplyAlignment(draft, CreateDefaultAlignment(draft));
    }

    /// <summary>Imports exactly six explicitly assigned PNG files.</summary>
    public SixViewImportResult ImportSeparate(IReadOnlyDictionary<VoxelFace, string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        VoxelFace[] missingFaces = HorizontalOrder.Where(face => !paths.ContainsKey(face)).ToArray();
        if (missingFaces.Length > 0 || paths.Count != HorizontalOrder.Length)
        {
            throw new ArgumentException(
                $"Exactly six face paths are required. Missing: {string.Join(", ", missingFaces)}.",
                nameof(paths));
        }

        SixViewImportDraft draft = InspectSeparate(paths);
        return ApplyAlignment(draft, CreateDefaultAlignment(draft));
    }

    private static SixViewSourceSlot ReadRegion(
        Image<ImageSharpColor> source,
        int startX,
        int startY,
        int width,
        int height,
        int sourceIndex,
        string sourcePath,
        VoxelFace? suggestedFace)
    {
        Rgba32Color[] pixels = new Rgba32Color[checked(width * height)];
        List<ImportDiagnostic> diagnostics = [];
        int opaquePixelCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ImageSharpColor sourceColor = source[startX + x, startY + y];
                if (sourceColor.A is > 0 and < byte.MaxValue)
                {
                    diagnostics.Add(new ImportDiagnostic(
                        "non-binary-alpha",
                        ImportDiagnosticSeverity.Error,
                        sourceIndex,
                        suggestedFace,
                        x,
                        y,
                        $"The {suggestedFace?.ToString() ?? $"source slot {sourceIndex}"} view " +
                        $"contains non-binary alpha {sourceColor.A} at ({x}, {y})."));
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
            diagnostics.Add(new ImportDiagnostic(
                "empty-face",
                ImportDiagnosticSeverity.Error,
                sourceIndex,
                suggestedFace,
                null,
                null,
                $"The {suggestedFace?.ToString() ?? $"source slot {sourceIndex}"} view contains no opaque pixels."));
        }

        return new SixViewSourceSlot(
            sourceIndex,
            sourcePath,
            new OrthographicImage(width, height, pixels),
            suggestedFace,
            opaquePixelCount,
            diagnostics);
    }

    private static (OrthographicImage Image, int OpaqueCount, int ClippedOpaqueCount) Transform(
        OrthographicImage source,
        SixViewFaceAlignment alignment)
    {
        Rgba32Color[] output = new Rgba32Color[checked(source.Width * source.Height)];
        int opaqueCount = 0;
        int clippedOpaqueCount = 0;

        for (int sourceY = 0; sourceY < source.Height; sourceY++)
        {
            for (int sourceX = 0; sourceX < source.Width; sourceX++)
            {
                Rgba32Color pixel = source.GetPixel(sourceX, sourceY);
                int flippedX = alignment.FlipHorizontal
                    ? source.Width - 1 - sourceX
                    : sourceX;
                int flippedY = alignment.FlipVertical
                    ? source.Height - 1 - sourceY
                    : sourceY;
                int targetX = flippedX + alignment.OffsetX;
                int targetY = flippedY + alignment.OffsetY;

                if ((uint)targetX >= (uint)source.Width || (uint)targetY >= (uint)source.Height)
                {
                    if (pixel.Alpha == byte.MaxValue) clippedOpaqueCount++;
                    continue;
                }

                output[(targetY * source.Width) + targetX] = pixel;
                if (pixel.Alpha == byte.MaxValue) opaqueCount++;
            }
        }

        return (new OrthographicImage(source.Width, source.Height, output), opaqueCount, clippedOpaqueCount);
    }

    private static VoxelFace? InferFace(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string[] tokens = name.Split(
            ['_', '-', '.', ' ', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (VoxelFace face in HorizontalOrder)
        {
            if (tokens.Contains(face.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                return face;
            }
        }

        return null;
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
}
