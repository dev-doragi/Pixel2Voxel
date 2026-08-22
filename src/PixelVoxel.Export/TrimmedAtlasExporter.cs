using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Imaging;

namespace PixelVoxel.Export;

public sealed record TrimmedAtlasOptions(int Padding = 2, int Extrusion = 1, int MaximumSize = 4096)
{
    public void Validate()
    {
        if (Padding < 0) throw new ArgumentOutOfRangeException(nameof(Padding));
        if (Extrusion < 0) throw new ArgumentOutOfRangeException(nameof(Extrusion));
        if (MaximumSize is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaximumSize));
    }
}

public sealed record TrimmedAtlasExportRequest(
    string DestinationPngPath,
    IReadOnlyList<SpriteFrame> Frames,
    TrimmedAtlasOptions Options);

/// <summary>Writes trimmed sprites into one deterministic MaxRects atlas and Aseprite-compatible JSON.</summary>
public sealed class TrimmedAtlasExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly PngPixelWriter _pngWriter;

    public TrimmedAtlasExporter(PngPixelWriter pngWriter) =>
        _pngWriter = pngWriter ?? throw new ArgumentNullException(nameof(pngWriter));

    public async Task<SpriteExportResult> ExportAsync(
        TrimmedAtlasExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Frames is null || request.Frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(request));
        if (request.Frames.Select(frame => frame.Name).Distinct(StringComparer.Ordinal).Count() != request.Frames.Count)
            throw new InvalidDataException("Atlas frame names must be unique.");
        request.Options.Validate();
        string pngPath = Path.GetFullPath(request.DestinationPngPath);
        if (!Path.GetExtension(pngPath).Equals(".png", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Atlas output must use .png.");

        Trimmed[] trimmed = request.Frames.Select(Trim).ToArray();
        int margin = request.Options.Padding + request.Options.Extrusion;
        Packed[] packed = Pack(trimmed, margin, request.Options.MaximumSize, out int width, out int height);
        Rgba32Color[] atlas = new Rgba32Color[checked(width * height)];
        foreach (Packed item in packed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BlitExtruded(atlas, width, item, request.Options.Extrusion);
        }

        string directory = Path.GetDirectoryName(pngPath) ?? throw new InvalidOperationException("Atlas directory could not be resolved.");
        Directory.CreateDirectory(directory);
        string jsonPath = Path.ChangeExtension(pngPath, ".json");
        string token = Guid.NewGuid().ToString("N");
        string tempPng = Path.Combine(directory, $".{Path.GetFileName(pngPath)}.{token}.tmp");
        string tempJson = Path.Combine(directory, $".{Path.GetFileName(jsonPath)}.{token}.tmp");
        try
        {
            await _pngWriter.WriteAsync(tempPng, width, height, atlas, cancellationToken);
            object metadata = new
            {
                frames = packed.OrderBy(item => item.Index).Select(item => new
                {
                    filename = item.Frame.Name,
                    frame = new { x = item.ContentX, y = item.ContentY, w = item.Trim.Width, h = item.Trim.Height },
                    rotated = false,
                    trimmed = item.Trim.IsTrimmed,
                    spriteSourceSize = new { x = item.Trim.X, y = item.Trim.Y, w = item.Trim.Width, h = item.Trim.Height },
                    sourceSize = new { w = item.Frame.Width, h = item.Frame.Height },
                    duration = item.Frame.DurationMilliseconds,
                    pivot = new { x = item.Frame.PivotX, y = item.Frame.PivotY },
                }),
                meta = new
                {
                    app = "Pixel2Voxel",
                    version = "1",
                    image = Path.GetFileName(pngPath),
                    format = "RGBA8888",
                    size = new { w = width, h = height },
                    scale = "1",
                    frameTags = request.Frames.Count > 1
                        ? new[] { new { name = "timeline", from = 0, to = request.Frames.Count - 1, direction = "forward" } }
                        : [],
                    slices = new[]
                    {
                        new
                        {
                            name = "pivot",
                            color = "#0000ffff",
                            keys = request.Frames.Select((frame, index) => new
                            {
                                frame = index,
                                bounds = new { x = 0, y = 0, w = frame.Width, h = frame.Height },
                                pivot = new { x = frame.PivotX, y = frame.PivotY },
                            }),
                        },
                    },
                },
            };
            await File.WriteAllTextAsync(tempJson, JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CommitAtomically(tempPng, pngPath, tempJson, jsonPath);
            return new SpriteExportResult(pngPath, jsonPath, width, height, request.Frames.Count);
        }
        finally
        {
            TryDelete(tempPng);
            TryDelete(tempJson);
        }
    }

    private static void CommitAtomically(string tempPng, string pngPath, string tempJson, string jsonPath)
    {
        string token = Guid.NewGuid().ToString("N");
        string pngBackup = pngPath + $".{token}.bak";
        string jsonBackup = jsonPath + $".{token}.bak";
        bool hadPng = File.Exists(pngPath), hadJson = File.Exists(jsonPath);
        try
        {
            if (hadPng) File.Copy(pngPath, pngBackup);
            if (hadJson) File.Copy(jsonPath, jsonBackup);
            File.Move(tempPng, pngPath, true);
            File.Move(tempJson, jsonPath, true);
        }
        catch
        {
            Restore(pngPath, pngBackup, hadPng);
            Restore(jsonPath, jsonBackup, hadJson);
            throw;
        }
        finally
        {
            TryDelete(pngBackup); TryDelete(jsonBackup);
        }
    }

    private static void Restore(string path, string backup, bool existed)
    {
        if (existed && File.Exists(backup)) File.Copy(backup, path, true);
        else if (!existed) TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static Trimmed Trim(SpriteFrame frame)
    {
        int minX = frame.Width, minY = frame.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < frame.Height; y++) for (int x = 0; x < frame.Width; x++)
        {
            if (frame.Pixels.Span[(y * frame.Width) + x].Alpha == 0) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        if (maxX < 0) return new Trimmed(frame, 0, 0, 1, 1, [default]);
        int width = maxX - minX + 1, height = maxY - minY + 1;
        Rgba32Color[] pixels = new Rgba32Color[width * height];
        for (int y = 0; y < height; y++) frame.Pixels.Span.Slice(((minY + y) * frame.Width) + minX, width).CopyTo(pixels.AsSpan(y * width, width));
        return new Trimmed(frame, minX, minY, width, height, pixels);
    }

    private static Packed[] Pack(Trimmed[] items, int margin, int max, out int width, out int height)
    {
        int largest = items.Max(item => Math.Max(item.Width, item.Height) + (margin * 2));
        int size = 1; while (size < largest) size *= 2;
        for (width = size, height = size; width <= max && height <= max;)
        {
            if (TryPack(items, margin, width, height, out Packed[]? result)) return result;
            if (width <= height) width *= 2; else height *= 2;
        }
        throw new InvalidOperationException($"Frames do not fit in one {max}x{max} atlas. Reduce frame count or output scale.");
    }

    private static bool TryPack(Trimmed[] source, int margin, int width, int height, out Packed[] result)
    {
        List<Rect> free = [new(0, 0, width, height)];
        List<Packed> packed = [];
        foreach ((Trimmed item, int index) in source.Select((item, index) => (item, index)).OrderByDescending(pair => pair.item.Width * pair.item.Height).ThenBy(pair => pair.index))
        {
            int w = item.Width + margin * 2, h = item.Height + margin * 2;
            Rect? chosen = free.Where(rect => w <= rect.W && h <= rect.H)
                .OrderBy(rect => Math.Min(rect.W - w, rect.H - h)).ThenBy(rect => Math.Max(rect.W - w, rect.H - h)).ThenBy(rect => rect.Y).ThenBy(rect => rect.X).FirstOrDefault();
            if (chosen is null) { result = []; return false; }
            Rect used = new(chosen.X, chosen.Y, w, h);
            SplitFree(free, used);
            packed.Add(new Packed(index, item.Frame, item, used.X + margin, used.Y + margin));
        }
        result = packed.ToArray(); return true;
    }

    private static void SplitFree(List<Rect> free, Rect used)
    {
        for (int i = free.Count - 1; i >= 0; i--)
        {
            Rect r = free[i];
            if (used.X >= r.X + r.W || used.X + used.W <= r.X || used.Y >= r.Y + r.H || used.Y + used.H <= r.Y) continue;
            free.RemoveAt(i);
            if (used.X > r.X) free.Add(new(r.X, r.Y, used.X - r.X, r.H));
            if (used.X + used.W < r.X + r.W) free.Add(new(used.X + used.W, r.Y, r.X + r.W - used.X - used.W, r.H));
            if (used.Y > r.Y) free.Add(new(r.X, r.Y, r.W, used.Y - r.Y));
            if (used.Y + used.H < r.Y + r.H) free.Add(new(r.X, used.Y + used.H, r.W, r.Y + r.H - used.Y - used.H));
        }
        free.RemoveAll(a => free.Any(b => !ReferenceEquals(a, b) && a != b && a.X >= b.X && a.Y >= b.Y && a.X + a.W <= b.X + b.W && a.Y + a.H <= b.Y + b.H));
    }

    private static void BlitExtruded(Rgba32Color[] atlas, int atlasWidth, Packed item, int extrusion)
    {
        for (int y = -extrusion; y < item.Trim.Height + extrusion; y++) for (int x = -extrusion; x < item.Trim.Width + extrusion; x++)
        {
            int sx = Math.Clamp(x, 0, item.Trim.Width - 1), sy = Math.Clamp(y, 0, item.Trim.Height - 1);
            atlas[((item.ContentY + y) * atlasWidth) + item.ContentX + x] = item.Trim.Pixels[(sy * item.Trim.Width) + sx];
        }
    }

    private sealed record Trimmed(SpriteFrame Frame, int X, int Y, int Width, int Height, Rgba32Color[] Pixels)
    { public bool IsTrimmed => X != 0 || Y != 0 || Width != Frame.Width || Height != Frame.Height; }
    private sealed record Packed(int Index, SpriteFrame Frame, Trimmed Trim, int ContentX, int ContentY);
    private sealed record Rect(int X, int Y, int W, int H);
}
