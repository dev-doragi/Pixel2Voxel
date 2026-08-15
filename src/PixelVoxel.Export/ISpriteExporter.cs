using PixelVoxel.Core;

namespace PixelVoxel.Export;

/// <summary>Contains one logical, unscaled sprite frame and its shared-canvas pivot.</summary>
public sealed class SpriteFrame
{
    private readonly Rgba32Color[] _pixels;

    /// <summary>Initializes a complete RGBA sprite frame.</summary>
    public SpriteFrame(
        string name,
        int directionIndex,
        float yawDegrees,
        int width,
        int height,
        IEnumerable<Rgba32Color> pixels,
        int pivotX,
        int pivotY,
        int durationMilliseconds = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (directionIndex < 0) throw new ArgumentOutOfRangeException(nameof(directionIndex));
        if (!float.IsFinite(yawDegrees)) throw new ArgumentOutOfRangeException(nameof(yawDegrees));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (durationMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
        _pixels = pixels?.ToArray() ?? throw new ArgumentNullException(nameof(pixels));
        if (_pixels.Length != checked(width * height))
        {
            throw new ArgumentException("The pixel count must match width times height.", nameof(pixels));
        }

        Name = name;
        DirectionIndex = directionIndex;
        YawDegrees = yawDegrees;
        Width = width;
        Height = height;
        PivotX = pivotX;
        PivotY = pivotY;
        DurationMilliseconds = durationMilliseconds;
    }

    /// <summary>Gets the stable frame name used by metadata.</summary>
    public string Name { get; }

    /// <summary>Gets the zero-based direction index.</summary>
    public int DirectionIndex { get; }

    /// <summary>Gets the model yaw used to render the frame.</summary>
    public float YawDegrees { get; }

    /// <summary>Gets the logical frame width.</summary>
    public int Width { get; }

    /// <summary>Gets the logical frame height.</summary>
    public int Height { get; }

    /// <summary>Gets pixels in top-left, row-major order.</summary>
    public ReadOnlyMemory<Rgba32Color> Pixels => _pixels;

    /// <summary>Gets the horizontal frame-local pivot.</summary>
    public int PivotX { get; }

    /// <summary>Gets the vertical frame-local pivot.</summary>
    public int PivotY { get; }

    /// <summary>Gets the display time used by GIF and Aseprite metadata.</summary>
    public int DurationMilliseconds { get; }
}

/// <summary>Defines one horizontal PNG sheet export and its optional Aseprite JSON file.</summary>
public sealed class SpriteSheetExportRequest
{
    /// <summary>Initializes a sprite sheet export request.</summary>
    public SpriteSheetExportRequest(
        string destinationPngPath,
        IEnumerable<SpriteFrame> frames,
        bool writeAsepriteJson,
        string? directionTag = null)
    {
        DestinationPngPath = destinationPngPath ??
            throw new ArgumentNullException(nameof(destinationPngPath));
        Frames = frames?.ToArray() ?? throw new ArgumentNullException(nameof(frames));
        WriteAsepriteJson = writeAsepriteJson;
        DirectionTag = directionTag;
    }

    /// <summary>Gets the final PNG path.</summary>
    public string DestinationPngPath { get; }

    /// <summary>Gets frames in left-to-right sheet order.</summary>
    public IReadOnlyList<SpriteFrame> Frames { get; }

    /// <summary>Gets whether Aseprite-compatible JSON metadata is written.</summary>
    public bool WriteAsepriteJson { get; }

    /// <summary>Gets the optional Aseprite frame-tag name.</summary>
    public string? DirectionTag { get; }
}

/// <summary>Reports the files and dimensions produced by one sprite export.</summary>
public sealed record SpriteExportResult(
    string PngPath,
    string? JsonPath,
    int Width,
    int Height,
    int FrameCount);

/// <summary>Writes logical RGBA sprite frames without depending on a rendering backend.</summary>
public interface ISpriteExporter
{
    /// <summary>Writes a horizontal PNG sheet and optional Aseprite metadata.</summary>
    Task<SpriteExportResult> ExportAsync(
        SpriteSheetExportRequest request,
        CancellationToken cancellationToken = default);
}
