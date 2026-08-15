using PixelVoxel.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Export;

/// <summary>Writes fixed-canvas logical frames as a looping GIF.</summary>
public sealed class GifAnimationExporter
{
    public async Task<GifExportResult> ExportAsync(
        string path,
        IReadOnlyList<SpriteFrame> frames,
        CancellationToken cancellationToken = default,
        int resizePercent = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
        if (resizePercent is < 25 or > 1000) throw new ArgumentOutOfRangeException(nameof(resizePercent));
        SpriteFrame first = frames[0];
        if (frames.Any(frame => frame.Width != first.Width || frame.Height != first.Height))
        {
            throw new InvalidDataException("All GIF frames must share one canvas.");
        }

        string fullPath = Path.GetFullPath(path);
        if (!Path.GetExtension(fullPath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Animation output must use the .gif extension.");
        }

        string directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("GIF directory is unavailable.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        bool quantized = frames.SelectMany(frame => frame.Pixels.ToArray()).Distinct().Take(257).Count() > 256;
        try
        {
            using Image<ImageSharpColor> image = CreateImage(first, resizePercent);
            SetDelay(image.Frames.RootFrame, first.DurationMilliseconds);
            for (int index = 1; index < frames.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Image<ImageSharpColor> next = CreateImage(frames[index], resizePercent);
                ImageFrame<ImageSharpColor> added = image.Frames.AddFrame(next.Frames.RootFrame);
                SetDelay(added, frames[index].DurationMilliseconds);
            }

            image.Metadata.GetGifMetadata().RepeatCount = 0;
            await image.SaveAsync(temporary, new GifEncoder(), cancellationToken);
            File.Move(temporary, fullPath, overwrite: true);
            return new GifExportResult(fullPath, frames.Count, quantized);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
        }
    }

    private static Image<ImageSharpColor> CreateImage(SpriteFrame frame, int resizePercent)
    {
        int width = Math.Max(1, checked((int)Math.Round(
            frame.Width * (resizePercent / 100d),
            MidpointRounding.AwayFromZero)));
        int height = Math.Max(1, checked((int)Math.Round(
            frame.Height * (resizePercent / 100d),
            MidpointRounding.AwayFromZero)));
        ImageSharpColor[] pixels = new ImageSharpColor[checked(width * height)];
        ReadOnlySpan<Rgba32Color> source = frame.Pixels.Span;
        for (int y = 0; y < height; y++)
        {
            int sourceY = Math.Min(frame.Height - 1, y * frame.Height / height);
            for (int x = 0; x < width; x++)
            {
                int sourceX = Math.Min(frame.Width - 1, x * frame.Width / width);
                Rgba32Color color = source[(sourceY * frame.Width) + sourceX];
                pixels[(y * width) + x] = new ImageSharpColor(color.Red, color.Green, color.Blue, color.Alpha);
            }
        }
        return Image.LoadPixelData<ImageSharpColor>(pixels, width, height);
    }

    private static void SetDelay(ImageFrame<ImageSharpColor> frame, int milliseconds) =>
        frame.Metadata.GetGifMetadata().FrameDelay = Math.Max(1, (int)Math.Round(milliseconds / 10d));
}

public sealed record GifExportResult(string GifPath, int FrameCount, bool WasQuantized);
