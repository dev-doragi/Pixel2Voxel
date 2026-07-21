using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Imaging;

namespace PixelVoxel.Export;

/// <summary>Assembles fixed-size logical frames into a horizontal PNG and Aseprite JSON array.</summary>
public sealed class SpriteSheetExporter : ISpriteExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly PngPixelWriter _pngWriter;

    /// <summary>Initializes an exporter with the ImageSharp-backed PNG boundary.</summary>
    public SpriteSheetExporter(PngPixelWriter pngWriter)
    {
        _pngWriter = pngWriter ?? throw new ArgumentNullException(nameof(pngWriter));
    }

    /// <inheritdoc />
    public async Task<SpriteExportResult> ExportAsync(
        SpriteSheetExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Frames.Count == 0)
        {
            throw new ArgumentException("At least one sprite frame is required.", nameof(request));
        }

        string pngPath = Path.GetFullPath(request.DestinationPngPath);
        if (!Path.GetExtension(pngPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Sprite output must use the .png extension.");
        }

        SpriteFrame first = request.Frames[0];
        if (request.Frames.Any(frame =>
            frame.Width != first.Width ||
            frame.Height != first.Height ||
            frame.PivotX != first.PivotX ||
            frame.PivotY != first.PivotY))
        {
            throw new InvalidDataException("All sprite frames must share one canvas and pivot.");
        }

        int sheetWidth = checked(first.Width * request.Frames.Count);
        int sheetHeight = first.Height;
        Rgba32Color[] sheetPixels = new Rgba32Color[checked(sheetWidth * sheetHeight)];
        for (int frameIndex = 0; frameIndex < request.Frames.Count; frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SpriteFrame frame = request.Frames[frameIndex];
            for (int y = 0; y < frame.Height; y++)
            {
                frame.Pixels.Span.Slice(y * frame.Width, frame.Width).CopyTo(
                    sheetPixels.AsSpan(
                        (y * sheetWidth) + (frameIndex * frame.Width),
                        frame.Width));
            }
        }

        string? directory = Path.GetDirectoryName(pngPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The PNG output directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);
        string token = Guid.NewGuid().ToString("N");
        string temporaryPng = Path.Combine(directory, $".{Path.GetFileName(pngPath)}.{token}.tmp");
        string? jsonPath = request.WriteAsepriteJson
            ? Path.ChangeExtension(pngPath, ".json")
            : null;
        string? temporaryJson = jsonPath is null
            ? null
            : Path.Combine(directory, $".{Path.GetFileName(jsonPath)}.{token}.tmp");

        try
        {
            await _pngWriter.WriteAsync(
                temporaryPng,
                sheetWidth,
                sheetHeight,
                sheetPixels,
                cancellationToken);
            if (temporaryJson is not null)
            {
                object metadata = CreateAsepriteMetadata(
                    request,
                    Path.GetFileName(pngPath),
                    sheetWidth,
                    sheetHeight);
                await File.WriteAllTextAsync(
                    temporaryJson,
                    JsonSerializer.Serialize(metadata, JsonOptions),
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CommitAtomically(temporaryPng, pngPath, temporaryJson, jsonPath);
            return new SpriteExportResult(
                pngPath,
                jsonPath,
                sheetWidth,
                sheetHeight,
                request.Frames.Count);
        }
        finally
        {
            TryDelete(temporaryPng);
            if (temporaryJson is not null) TryDelete(temporaryJson);
        }
    }

    private static object CreateAsepriteMetadata(
        SpriteSheetExportRequest request,
        string imageName,
        int sheetWidth,
        int sheetHeight)
    {
        SpriteFrame first = request.Frames[0];
        object[] frames = request.Frames.Select((frame, index) => (object)new
        {
            filename = frame.Name,
            frame = new { x = index * frame.Width, y = 0, w = frame.Width, h = frame.Height },
            rotated = false,
            trimmed = false,
            spriteSourceSize = new { x = 0, y = 0, w = frame.Width, h = frame.Height },
            sourceSize = new { w = frame.Width, h = frame.Height },
            duration = 100,
        }).ToArray();
        object[] frameTags = request.Frames.Count > 1
            ?
            [
                new
                {
                    name = request.DirectionTag ?? $"directions-{request.Frames.Count}",
                    from = 0,
                    to = request.Frames.Count - 1,
                    direction = "forward",
                },
            ]
            : [];

        return new
        {
            frames,
            meta = new
            {
                app = "Pixel Voxel",
                version = "1",
                image = imageName,
                format = "RGBA8888",
                size = new { w = sheetWidth, h = sheetHeight },
                scale = "1",
                frameTags,
                slices = new[]
                {
                    new
                    {
                        name = "pivot",
                        color = "#0000ffff",
                        keys = new[]
                        {
                            new
                            {
                                frame = 0,
                                bounds = new { x = 0, y = 0, w = first.Width, h = first.Height },
                                pivot = new { x = first.PivotX, y = first.PivotY },
                            },
                        },
                    },
                },
            },
        };
    }

    private static void CommitAtomically(
        string temporaryPng,
        string pngPath,
        string? temporaryJson,
        string? jsonPath)
    {
        string token = Guid.NewGuid().ToString("N");
        string pngBackup = pngPath + $".{token}.bak";
        string? jsonBackup = jsonPath is null ? null : jsonPath + $".{token}.bak";
        bool pngExisted = File.Exists(pngPath);
        bool jsonExisted = jsonPath is not null && File.Exists(jsonPath);

        try
        {
            if (pngExisted) File.Copy(pngPath, pngBackup);
            if (jsonExisted && jsonPath is not null && jsonBackup is not null)
            {
                File.Copy(jsonPath, jsonBackup);
            }

            File.Move(temporaryPng, pngPath, overwrite: true);
            if (temporaryJson is not null && jsonPath is not null)
            {
                File.Move(temporaryJson, jsonPath, overwrite: true);
            }
        }
        catch
        {
            RestoreBackup(pngPath, pngBackup, pngExisted);
            if (jsonPath is not null && jsonBackup is not null)
            {
                RestoreBackup(jsonPath, jsonBackup, jsonExisted);
            }

            throw;
        }
        finally
        {
            TryDelete(pngBackup);
            if (jsonBackup is not null) TryDelete(jsonBackup);
        }
    }

    private static void RestoreBackup(string path, string backup, bool existed)
    {
        if (existed && File.Exists(backup))
        {
            File.Copy(backup, path, overwrite: true);
        }
        else if (!existed)
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
