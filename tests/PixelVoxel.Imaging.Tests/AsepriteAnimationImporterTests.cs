using System.Text.Json;
using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging.Tests;

public sealed class AsepriteAnimationImporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "PixelVoxel.AnimationImport", Guid.NewGuid().ToString("N"));

    public AsepriteAnimationImporterTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ImportsSynchronizedFaceFramesAndPreservesDurationAndAlpha()
    {
        AsepriteFaceAnimationSource front = WriteAnimation("front", [80, 120], 90);
        AsepriteFaceAnimationSource right = WriteAnimation("right", [80, 120], 140);

        OrthographicAnimation animation = new AsepriteAnimationImporter().Import(
            new Dictionary<VoxelFace, AsepriteFaceAnimationSource>
            {
                [VoxelFace.Front] = front,
                [VoxelFace.Right] = right,
            });

        Assert.Equal(2, animation.Frames.Count);
        Assert.Equal([80, 120], animation.DurationsMilliseconds);
        Assert.Equal(["frame_0000", "frame_0001"], animation.FrameNames);
        Assert.Equal(90, animation.Frames[0][VoxelFace.Front].GetPixel(0, 0).Alpha);
        Assert.Equal(140, animation.Frames[1][VoxelFace.Right].GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void RestoresTrimmedFrameIntoItsSourceCanvas()
    {
        AsepriteFaceAnimationSource source = WriteAnimation("trimmed", [100], 255, trimmed: true);

        OrthographicAnimation animation = new AsepriteAnimationImporter().Import(
            new Dictionary<VoxelFace, AsepriteFaceAnimationSource> { [VoxelFace.Top] = source });

        OrthographicImage frame = animation.Frames[0][VoxelFace.Top];
        Assert.Equal(4, frame.Width);
        Assert.Equal(3, frame.Height);
        Assert.Equal(0, frame.GetPixel(0, 0).Alpha);
        Assert.Equal(255, frame.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void RejectsDurationMismatchWithFaceAndFrameContext()
    {
        AsepriteFaceAnimationSource front = WriteAnimation("front", [80, 120], 255);
        AsepriteFaceAnimationSource right = WriteAnimation("right", [80, 121], 255);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new AsepriteAnimationImporter().Import(
                new Dictionary<VoxelFace, AsepriteFaceAnimationSource>
                {
                    [VoxelFace.Front] = front,
                    [VoxelFace.Right] = right,
                }));

        Assert.Contains("Right frame 1 duration", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private AsepriteFaceAnimationSource WriteAnimation(
        string name,
        IReadOnlyList<int> durations,
        byte alpha,
        bool trimmed = false)
    {
        string pngPath = Path.Combine(_directory, name + ".png");
        string jsonPath = Path.Combine(_directory, name + ".json");
        using (Image<ImageSharpColor> image = new(durations.Count * 2, 1))
        {
            for (int index = 0; index < durations.Count; index++)
            {
                image[index * 2, 0] = new ImageSharpColor((byte)(20 + index), 30, 40, alpha);
                image[(index * 2) + 1, 0] = new ImageSharpColor(50, 60, 70, alpha);
            }
            image.SaveAsPng(pngPath);
        }

        object metadata = new
        {
            frames = durations.Select((duration, index) => new
            {
                filename = $"frame_{index:D4}",
                frame = new { x = index * 2, y = 0, w = 2, h = 1 },
                rotated = false,
                trimmed,
                spriteSourceSize = new { x = trimmed ? 1 : 0, y = trimmed ? 1 : 0, w = 2, h = 1 },
                sourceSize = new { w = trimmed ? 4 : 2, h = trimmed ? 3 : 1 },
                duration,
            }).ToArray(),
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(metadata));
        return new AsepriteFaceAnimationSource(pngPath, jsonPath);
    }
}
