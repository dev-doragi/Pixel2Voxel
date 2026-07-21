using PixelVoxel.Core;

namespace PixelVoxel.Imaging;

/// <summary>Identifies how a six-view draft obtains its source slots.</summary>
public enum SixViewImportSourceKind
{
    /// <summary>Uses six equal regions from one horizontal PNG.</summary>
    HorizontalSheet,

    /// <summary>Uses separately selected PNG files.</summary>
    SeparateFiles,
}

/// <summary>Identifies whether an import diagnostic blocks reconstruction.</summary>
public enum ImportDiagnosticSeverity
{
    /// <summary>Describes non-blocking import information.</summary>
    Info,

    /// <summary>Describes a non-blocking condition that may lose source pixels.</summary>
    Warning,

    /// <summary>Describes a condition that must be fixed before reconstruction.</summary>
    Error,
}

/// <summary>Describes one actionable import issue and its optional source pixel location.</summary>
public sealed record ImportDiagnostic(
    string Code,
    ImportDiagnosticSeverity Severity,
    int SourceSlotIndex,
    VoxelFace? Face,
    int? X,
    int? Y,
    string Message);

/// <summary>Stores one source image before its target voxel face and alignment are finalized.</summary>
public sealed class SixViewSourceSlot
{
    /// <summary>Initializes an inspected source slot.</summary>
    public SixViewSourceSlot(
        int sourceIndex,
        string sourcePath,
        OrthographicImage image,
        VoxelFace? suggestedFace,
        int opaquePixelCount,
        IEnumerable<ImportDiagnostic>? diagnostics = null)
    {
        if (sourceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        SourceIndex = sourceIndex;
        SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        Image = image ?? throw new ArgumentNullException(nameof(image));
        SuggestedFace = suggestedFace;
        OpaquePixelCount = opaquePixelCount;
        Diagnostics = diagnostics?.ToArray() ?? [];
    }

    /// <summary>Gets the stable zero-based source slot index.</summary>
    public int SourceIndex { get; }

    /// <summary>Gets the PNG that contains this source image.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the unmodified source pixels.</summary>
    public OrthographicImage Image { get; }

    /// <summary>Gets the face inferred from sheet position or file name, when available.</summary>
    public VoxelFace? SuggestedFace { get; }

    /// <summary>Gets the number of fully opaque source pixels.</summary>
    public int OpaquePixelCount { get; }

    /// <summary>Gets source-local validation diagnostics.</summary>
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }
}

/// <summary>Stores inspected six-view sources without committing them to model faces.</summary>
public sealed class SixViewImportDraft
{
    /// <summary>Initializes an import draft.</summary>
    public SixViewImportDraft(
        string sourcePath,
        SixViewImportSourceKind sourceKind,
        IEnumerable<SixViewSourceSlot> slots,
        IEnumerable<ImportDiagnostic>? diagnostics = null)
    {
        SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        SourceKind = sourceKind;
        Slots = slots?.OrderBy(slot => slot.SourceIndex).ToArray() ??
            throw new ArgumentNullException(nameof(slots));
        Diagnostics = (diagnostics ?? [])
            .Concat(Slots.SelectMany(slot => slot.Diagnostics))
            .ToArray();
    }

    /// <summary>Gets a display path for the inspected source set.</summary>
    public string SourcePath { get; }

    /// <summary>Gets whether the draft came from one sheet or separate files.</summary>
    public SixViewImportSourceKind SourceKind { get; }

    /// <summary>Gets the source slots in stable source order.</summary>
    public IReadOnlyList<SixViewSourceSlot> Slots { get; }

    /// <summary>Gets all inspection diagnostics.</summary>
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }
}

/// <summary>Defines the target face and pixel-space adjustment for one source slot.</summary>
public sealed record SixViewFaceAlignment(
    int SourceSlotIndex,
    VoxelFace TargetFace,
    bool FlipHorizontal = false,
    bool FlipVertical = false,
    int OffsetX = 0,
    int OffsetY = 0);

/// <summary>Groups source-to-face assignments for one six-view draft.</summary>
public sealed class SixViewAlignment
{
    /// <summary>Initializes a set of face assignments.</summary>
    public SixViewAlignment(IEnumerable<SixViewFaceAlignment> faces)
    {
        Faces = faces?.ToArray() ?? throw new ArgumentNullException(nameof(faces));
    }

    /// <summary>Gets the source-to-face assignments.</summary>
    public IReadOnlyList<SixViewFaceAlignment> Faces { get; }
}

/// <summary>Contains transformed face previews and diagnostics before reconstruction.</summary>
public sealed class SixViewAlignmentPreview
{
    /// <summary>Initializes an alignment preview.</summary>
    public SixViewAlignmentPreview(
        IReadOnlyDictionary<VoxelFace, OrthographicImage> views,
        IEnumerable<SixViewSlotInfo> slots,
        IEnumerable<ImportDiagnostic> diagnostics)
    {
        Views = views ?? throw new ArgumentNullException(nameof(views));
        Slots = slots?.ToArray() ?? throw new ArgumentNullException(nameof(slots));
        Diagnostics = diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>Gets transformed images indexed by target face.</summary>
    public IReadOnlyDictionary<VoxelFace, OrthographicImage> Views { get; }

    /// <summary>Gets transformed per-face statistics.</summary>
    public IReadOnlyList<SixViewSlotInfo> Slots { get; }

    /// <summary>Gets inspection and alignment diagnostics.</summary>
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether reconstruction may proceed.</summary>
    public bool CanApply => Diagnostics.All(item => item.Severity != ImportDiagnosticSeverity.Error);
}
