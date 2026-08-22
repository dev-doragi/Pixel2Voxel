using System.Text.Json;
using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging;

/// <summary>Identifies one face animation exported as an Aseprite PNG and JSON pair.</summary>
public sealed record AsepriteFaceAnimationSource(string PngPath, string JsonPath);

/// <summary>Contains one synchronized set of orthographic animation frames.</summary>
public sealed class OrthographicAnimation
{
    public OrthographicAnimation(
        IEnumerable<OrthographicViewSet> frames,
        IEnumerable<int> durationsMilliseconds,
        IEnumerable<string> frameNames)
    {
        Frames = frames?.ToArray() ?? throw new ArgumentNullException(nameof(frames));
        DurationsMilliseconds = durationsMilliseconds?.ToArray() ??
            throw new ArgumentNullException(nameof(durationsMilliseconds));
        FrameNames = frameNames?.ToArray() ?? throw new ArgumentNullException(nameof(frameNames));
        if (Frames.Count == 0 || Frames.Count != DurationsMilliseconds.Count || Frames.Count != FrameNames.Count)
        {
            throw new ArgumentException("Animation frames, durations, and names must have the same non-zero length.");
        }
    }

    public IReadOnlyList<OrthographicViewSet> Frames { get; }
    public IReadOnlyList<int> DurationsMilliseconds { get; }
    public IReadOnlyList<string> FrameNames { get; }
}

/// <summary>Reads and synchronizes per-face Aseprite PNG and JSON animations.</summary>
public sealed class AsepriteAnimationImporter
{
    public OrthographicAnimation Import(
        IReadOnlyDictionary<VoxelFace, AsepriteFaceAnimationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count is < 1 or > 6)
        {
            throw new ArgumentException("Assign between one and six animated face sources.", nameof(sources));
        }

        Dictionary<VoxelFace, FaceAnimation> animations = sources
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => ReadFace(pair.Key, pair.Value));
        FaceAnimation reference = animations.First().Value;
        foreach ((VoxelFace face, FaceAnimation animation) in animations.Skip(1))
        {
            if (animation.Frames.Count != reference.Frames.Count)
            {
                throw new InvalidDataException(
                    $"The {face} animation has {animation.Frames.Count} frames; expected {reference.Frames.Count}.");
            }

            for (int index = 0; index < reference.Frames.Count; index++)
            {
                if (animation.Durations[index] != reference.Durations[index])
                {
                    throw new InvalidDataException(
                        $"The {face} frame {index} duration is {animation.Durations[index]} ms; " +
                        $"expected {reference.Durations[index]} ms.");
                }

                OrthographicImage expected = reference.Frames[index];
                OrthographicImage actual = animation.Frames[index];
                if (actual.Width != expected.Width || actual.Height != expected.Height)
                {
                    throw new InvalidDataException(
                        $"The {face} frame {index} canvas is {actual.Width}x{actual.Height}; " +
                        $"expected {expected.Width}x{expected.Height}.");
                }
            }
        }

        OrthographicViewSet[] frames = new OrthographicViewSet[reference.Frames.Count];
        for (int index = 0; index < frames.Length; index++)
        {
            frames[index] = new OrthographicViewSet(
                animations.ToDictionary(pair => pair.Key, pair => pair.Value.Frames[index]));
        }

        return new OrthographicAnimation(frames, reference.Durations, reference.Names);
    }

    private static FaceAnimation ReadFace(VoxelFace face, AsepriteFaceAnimationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string pngPath = ValidateFile(source.PngPath, ".png");
        string jsonPath = ValidateFile(source.JsonPath, ".json");
        using Image<ImageSharpColor> sheet = Image.Load<ImageSharpColor>(pngPath);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(jsonPath));
        JsonElement root = json.RootElement;
        if (!root.TryGetProperty("frames", out JsonElement framesElement) ||
            framesElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
        {
            throw new InvalidDataException($"The {face} Aseprite JSON has no frames array or object.");
        }

        List<(string Name, JsonElement Value)> entries = framesElement.ValueKind == JsonValueKind.Array
            ? framesElement.EnumerateArray().Select((value, index) =>
                (ReadOptionalString(value, "filename") ?? $"frame_{index:D4}", value)).ToList()
            : framesElement.EnumerateObject().Select(property => (property.Name, property.Value)).ToList();
        if (entries.Count == 0) throw new InvalidDataException($"The {face} animation contains no frames.");

        List<OrthographicImage> frames = [];
        List<int> durations = [];
        List<string> names = [];
        for (int index = 0; index < entries.Count; index++)
        {
            (string name, JsonElement value) = entries[index];
            if (ReadOptionalBoolean(value, "rotated"))
            {
                throw new InvalidDataException($"The {face} frame {index} is rotated; rotated atlas frames are unsupported.");
            }

            JsonElement rectangle = RequireObject(value, "frame", face, index);
            int x = RequireInt(rectangle, "x", face, index);
            int y = RequireInt(rectangle, "y", face, index);
            int width = RequirePositiveInt(rectangle, "w", face, index);
            int height = RequirePositiveInt(rectangle, "h", face, index);
            if (x < 0 || y < 0 || x + width > sheet.Width || y + height > sheet.Height)
            {
                throw new InvalidDataException($"The {face} frame {index} rectangle is outside the PNG sheet.");
            }

            JsonElement sourceSize = RequireObject(value, "sourceSize", face, index);
            int canvasWidth = RequirePositiveInt(sourceSize, "w", face, index);
            int canvasHeight = RequirePositiveInt(sourceSize, "h", face, index);
            JsonElement placement = value.TryGetProperty("spriteSourceSize", out JsonElement spriteSourceSize)
                ? spriteSourceSize
                : default;
            int targetX = placement.ValueKind == JsonValueKind.Object ? RequireInt(placement, "x", face, index) : 0;
            int targetY = placement.ValueKind == JsonValueKind.Object ? RequireInt(placement, "y", face, index) : 0;
            if (targetX < 0 || targetY < 0 || targetX + width > canvasWidth || targetY + height > canvasHeight)
            {
                throw new InvalidDataException($"The {face} frame {index} trim placement is outside its source canvas.");
            }

            Rgba32Color[] pixels = new Rgba32Color[checked(canvasWidth * canvasHeight)];
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                for (int sourceX = 0; sourceX < width; sourceX++)
                {
                    ImageSharpColor color = sheet[x + sourceX, y + sourceY];
                    pixels[((targetY + sourceY) * canvasWidth) + targetX + sourceX] =
                        new Rgba32Color(color.R, color.G, color.B, color.A);
                }
            }

            frames.Add(new OrthographicImage(canvasWidth, canvasHeight, pixels));
            durations.Add(RequirePositiveInt(value, "duration", face, index));
            names.Add(name);
        }

        return new FaceAnimation(frames, durations, names);
    }

    private static string ValidateFile(string path, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!Path.GetExtension(fullPath).Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Expected a {extension} file: {path}");
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Animation source file was not found.", fullPath);
        return fullPath;
    }

    private static JsonElement RequireObject(JsonElement parent, string property, VoxelFace face, int index) =>
        parent.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException($"The {face} frame {index} has no {property} object.");

    private static int RequireInt(JsonElement parent, string property, VoxelFace face, int index) =>
        parent.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int result)
            ? result
            : throw new InvalidDataException($"The {face} frame {index} has no integer {property}.");

    private static int RequirePositiveInt(JsonElement parent, string property, VoxelFace face, int index)
    {
        int value = RequireInt(parent, property, face, index);
        return value > 0
            ? value
            : throw new InvalidDataException($"The {face} frame {index} {property} must be positive.");
    }

    private static string? ReadOptionalString(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadOptionalBoolean(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private sealed record FaceAnimation(
        IReadOnlyList<OrthographicImage> Frames,
        IReadOnlyList<int> Durations,
        IReadOnlyList<string> Names);
}
