using PixelVoxel.Core;

namespace PixelVoxel.Imaging;

/// <summary>
/// Contains a validated static six-view import and its non-fatal diagnostics.
/// </summary>
public sealed class SixViewImportResult
{
    /// <summary>Initializes a successful six-view import result.</summary>
    public SixViewImportResult(
        string sourcePath,
        OrthographicViewSet views,
        IEnumerable<SixViewSlotInfo> slots,
        IEnumerable<string>? diagnostics = null)
    {
        SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        Views = views ?? throw new ArgumentNullException(nameof(views));
        Slots = slots?.ToArray() ?? throw new ArgumentNullException(nameof(slots));
        Diagnostics = diagnostics?.ToArray() ?? [];
    }

    /// <summary>Gets the user-selected sheet path or a display name for separate files.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the six framework-independent source images.</summary>
    public OrthographicViewSet Views { get; }

    /// <summary>Gets per-face validation statistics.</summary>
    public IReadOnlyList<SixViewSlotInfo> Slots { get; }

    /// <summary>Gets non-fatal import diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
