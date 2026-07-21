using System.Numerics;

namespace PixelVoxel.Rendering;

/// <summary>Maps a presented integer-scaled viewport back to logical framebuffer pixels.</summary>
public sealed record PixelViewportMapping(
    int TargetWidth,
    int TargetHeight,
    int LogicalWidth,
    int LogicalHeight,
    int Scale,
    int DestinationX,
    int DestinationY)
{
    /// <summary>Creates the centered presentation mapping shared by rendering and input.</summary>
    public static PixelViewportMapping Create(
        int targetWidth,
        int targetHeight,
        int logicalWidth,
        int logicalHeight,
        int? manualScale)
    {
        if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
        if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));
        if (logicalWidth <= 0) throw new ArgumentOutOfRangeException(nameof(logicalWidth));
        if (logicalHeight <= 0) throw new ArgumentOutOfRangeException(nameof(logicalHeight));
        int fitScale = Math.Max(1, Math.Min(targetWidth / logicalWidth, targetHeight / logicalHeight));
        int scale = manualScale.HasValue ? Math.Clamp(manualScale.Value, 1, 16) : fitScale;
        int destinationWidth = checked(logicalWidth * scale);
        int destinationHeight = checked(logicalHeight * scale);
        return new PixelViewportMapping(
            targetWidth,
            targetHeight,
            logicalWidth,
            logicalHeight,
            scale,
            (targetWidth - destinationWidth) / 2,
            (targetHeight - destinationHeight) / 2);
    }

    /// <summary>Maps a top-left target position into logical framebuffer coordinates.</summary>
    public bool TryMapToLogical(float targetX, float targetY, out Vector2 logicalPosition)
    {
        float x = (targetX - DestinationX) / Scale;
        float y = (targetY - DestinationY) / Scale;
        if (x < 0f || x >= LogicalWidth || y < 0f || y >= LogicalHeight)
        {
            logicalPosition = default;
            return false;
        }

        logicalPosition = new Vector2(x, y);
        return true;
    }
}
