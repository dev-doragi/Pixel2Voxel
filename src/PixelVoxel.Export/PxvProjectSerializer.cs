using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Imaging;

namespace PixelVoxel.Export;

/// <summary>Reads v1/v2 and writes version 2 portable .pxv ZIP containers.</summary>
public sealed class PxvProjectSerializer : IProjectSerializer
{
    private const int FormatVersion = 2;
    private const long MaximumCandidateCells = 1_048_576;
    private static readonly byte[] DocumentMagic = "PXVD"u8.ToArray();
    private static readonly DateTimeOffset StableEntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private readonly PngPixelWriter _pngWriter;
    private readonly PngPixelReader _pngReader;

    /// <summary>Initializes project persistence with PNG stream boundaries.</summary>
    public PxvProjectSerializer(PngPixelWriter pngWriter, PngPixelReader pngReader)
    {
        _pngWriter = pngWriter ?? throw new ArgumentNullException(nameof(pngWriter));
        _pngReader = pngReader ?? throw new ArgumentNullException(nameof(pngReader));
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string path,
        PixelVoxelProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        string fullPath = ValidatePath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The project directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                VoxelDimensions dimensions = project.Document.Storage.Dimensions;
                ValidateDimensions(dimensions);
                VoxelFace[] views = project.SourceViews?.Faces.OrderBy(face => (int)face).ToArray() ?? [];
                ProjectManifest manifest = new(
                    "PixelVoxel",
                    FormatVersion,
                    "XYZ-RightUpFront-v1",
                    dimensions.Width,
                    dimensions.Height,
                    dimensions.Depth,
                    views.Select(FaceName).ToArray(),
                    project.Settings,
                    project.Palette);
                await WriteJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken);
                WriteDocumentEntry(archive, project.Document, cancellationToken);
                if (project.SourceViews is not null)
                {
                    foreach (VoxelFace face in views)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        OrthographicImage image = project.SourceViews[face];
                        ZipArchiveEntry entry = CreateEntry(archive, $"views/{FaceName(face)}.png");
                        await using Stream entryStream = entry.Open();
                        await _pngWriter.WriteAsync(
                            entryStream,
                            image.Width,
                            image.Height,
                            image.Pixels,
                            cancellationToken);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    /// <inheritdoc />
    public async Task<PixelVoxelProject> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = ValidatePath(path);
        await using FileStream stream = new(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        ProjectManifest manifest = await ReadManifestAsync(archive, cancellationToken);
        if (manifest.Format != "PixelVoxel" || manifest.Version is not (1 or FormatVersion))
        {
            throw new InvalidDataException($"Unsupported Pixel Voxel project version {manifest.Version}.");
        }

        if (manifest.CoordinateSystem != "XYZ-RightUpFront-v1")
        {
            throw new InvalidDataException("The project coordinate system is not supported.");
        }

        VoxelDimensions dimensions = new(manifest.Width, manifest.Height, manifest.Depth);
        ValidateDimensions(dimensions);
        VoxelDocument document = ReadDocumentEntry(archive, dimensions, manifest.Version, cancellationToken);
        Dictionary<VoxelFace, OrthographicImage> views = [];
        foreach (string faceName in manifest.Views)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VoxelFace face = ParseFace(faceName);
            if (views.ContainsKey(face))
            {
                throw new InvalidDataException($"Project view {faceName} is duplicated.");
            }

            ZipArchiveEntry entry = archive.GetEntry($"views/{FaceName(face)}.png") ??
                throw new InvalidDataException($"Project view {faceName} is missing.");
            await using Stream entryStream = entry.Open();
            views.Add(face, await _pngReader.ReadAsync(entryStream, cancellationToken));
        }

        OrthographicViewSet? sourceViews = views.Count == 0 ? null : new OrthographicViewSet(views);
        Rgba32Color[] palette = manifest.Palette?.ToArray() ?? [];
        if (palette.Length > 32 || palette.Any(color => color.Alpha != byte.MaxValue))
        {
            throw new InvalidDataException("Project palette must contain at most 32 opaque colors.");
        }

        return new PixelVoxelProject(document, sourceViews, manifest.Settings, palette);
    }

    private static string ValidatePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Path.GetExtension(fullPath).Equals(".pxv", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Pixel Voxel projects must use the .pxv extension.");
        }

        return fullPath;
    }

    private static void ValidateDimensions(VoxelDimensions dimensions)
    {
        if (dimensions.Width <= 0 || dimensions.Height <= 0 || dimensions.Depth <= 0)
        {
            throw new InvalidDataException("Project voxel dimensions must be positive.");
        }

        long candidates = checked((long)dimensions.Width * dimensions.Height * dimensions.Depth);
        if (candidates > MaximumCandidateCells)
        {
            throw new InvalidDataException($"Project candidate volume {candidates:N0} exceeds the supported limit.");
        }
    }

    private static ZipArchiveEntry CreateEntry(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = StableEntryTime;
        return entry;
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string name,
        T value,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = CreateEntry(archive, name);
        await using Stream stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static void WriteDocumentEntry(
        ZipArchive archive,
        VoxelDocument document,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = CreateEntry(archive, "document.bin");
        using Stream stream = entry.Open();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(DocumentMagic);
        writer.Write(FormatVersion);
        VoxelEntry[] cells = document.Storage.GetOccupiedCells()
            .OrderBy(item => item.Coordinate.X)
            .ThenBy(item => item.Coordinate.Y)
            .ThenBy(item => item.Coordinate.Z)
            .ToArray();
        writer.Write(cells.Length);
        foreach (VoxelEntry entryValue in cells)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(entryValue.Coordinate.X);
            writer.Write(entryValue.Coordinate.Y);
            writer.Write(entryValue.Coordinate.Z);
            byte mask = 0;
            foreach (VoxelFace face in Enum.GetValues<VoxelFace>())
            {
                if (entryValue.Cell.TryGetColor(face, out _)) mask |= checked((byte)(1 << (int)face));
            }

            if (mask == 0) throw new InvalidDataException($"Voxel {entryValue.Coordinate} has no face colors.");
            writer.Write(mask);
            foreach (VoxelFace face in Enum.GetValues<VoxelFace>())
            {
                if (!entryValue.Cell.TryGetColor(face, out Rgba32Color color)) continue;
                writer.Write(color.Red);
                writer.Write(color.Green);
                writer.Write(color.Blue);
                writer.Write(color.Alpha);
            }
        }
    }

    private static VoxelDocument ReadDocumentEntry(
        ZipArchive archive,
        VoxelDimensions dimensions,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.GetEntry("document.bin") ??
            throw new InvalidDataException("Project document.bin is missing.");
        using Stream stream = entry.Open();
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        if (!reader.ReadBytes(DocumentMagic.Length).SequenceEqual(DocumentMagic))
        {
            throw new InvalidDataException("Project voxel data has an invalid header.");
        }

        int version = reader.ReadInt32();
        if (version != expectedVersion || version is not (1 or FormatVersion))
        {
            throw new InvalidDataException($"Unsupported voxel data version {version}.");
        }
        int count = reader.ReadInt32();
        long volume = checked((long)dimensions.Width * dimensions.Height * dimensions.Depth);
        if (count < 0 || count > volume) throw new InvalidDataException("Project occupied voxel count is invalid.");
        List<VoxelEntry> cells = new(count);
        HashSet<VoxelCoordinate> coordinates = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VoxelCoordinate coordinate = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (coordinate.X < 0 || coordinate.X >= dimensions.Width ||
                coordinate.Y < 0 || coordinate.Y >= dimensions.Height ||
                coordinate.Z < 0 || coordinate.Z >= dimensions.Depth)
            {
                throw new InvalidDataException($"Project voxel {coordinate} is outside the document bounds.");
            }

            if (!coordinates.Add(coordinate)) throw new InvalidDataException($"Project voxel {coordinate} is duplicated.");
            byte mask = reader.ReadByte();
            if (mask == 0 || (mask & ~0x3f) != 0) throw new InvalidDataException($"Project voxel {coordinate} has an invalid face mask.");
            Dictionary<VoxelFace, Rgba32Color> colors = [];
            foreach (VoxelFace face in Enum.GetValues<VoxelFace>())
            {
                if ((mask & (1 << (int)face)) == 0) continue;
                colors.Add(face, new Rgba32Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
            }

            cells.Add(new VoxelEntry(coordinate, new VoxelCell(colors)));
        }

        if (stream.ReadByte() != -1) throw new InvalidDataException("Project voxel data contains trailing bytes.");
        return new VoxelDocument(dimensions, cells);
    }

    private static async Task<ProjectManifest> ReadManifestAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.GetEntry("manifest.json") ??
            throw new InvalidDataException("Project manifest.json is missing.");
        await using Stream stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<ProjectManifest>(stream, JsonOptions, cancellationToken) ??
            throw new InvalidDataException("Project manifest is empty.");
    }

    private static string FaceName(VoxelFace face) => face.ToString().ToLowerInvariant();

    private static VoxelFace ParseFace(string value) =>
        Enum.TryParse(value, ignoreCase: true, out VoxelFace face) && Enum.IsDefined(face)
            ? face
            : throw new InvalidDataException($"Project view face '{value}' is invalid.");

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

    private sealed record ProjectManifest(
        string Format,
        int Version,
        string CoordinateSystem,
        int Width,
        int Height,
        int Depth,
        IReadOnlyList<string> Views,
        PixelVoxelProjectSettings Settings,
        IReadOnlyList<Rgba32Color>? Palette = null);
}
