using PixelVoxel.Core;
using SixLabors.ImageSharp;
using ImageSharpColor = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace PixelVoxel.Imaging.Tests;

public sealed class AsepriteSpriteSheetImporterTests
{
    private static readonly VoxelFace[] ExpectedOrder =
    [
        VoxelFace.Front,
        VoxelFace.Right,
        VoxelFace.Back,
        VoxelFace.Left,
        VoxelFace.Top,
        VoxelFace.Bottom,
    ];

    [Fact]
    public void ImportsFixedHorizontalSixViewOrder()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                image.SaveAsPng(path);
            }

            SixViewImportResult result =
                new AsepriteSpriteSheetImporter().ImportHorizontalSheet(path);

            Assert.Equal(ExpectedOrder, result.Slots.Select(slot => slot.Face));
            Assert.All(result.Slots, slot => Assert.Equal(4, slot.OpaquePixelCount));
            for (int index = 0; index < ExpectedOrder.Length; index++)
            {
                Assert.Equal(
                    new Rgba32Color((byte)(20 + index), 0, 0, 255),
                    result.Views[ExpectedOrder[index]].GetPixel(0, 0));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsSheetWidthThatIsNotDivisibleBySix()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = new(11, 2, new ImageSharpColor(1, 2, 3, 255)))
            {
                image.SaveAsPng(path);
            }

            Assert.Throws<InvalidDataException>(() =>
                new AsepriteSpriteSheetImporter().ImportHorizontalSheet(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsNonBinaryAlphaWithFaceAndPixelCoordinate()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                image[2, 1] = new ImageSharpColor(1, 2, 3, 128);
                image.SaveAsPng(path);
            }

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                new AsepriteSpriteSheetImporter().ImportHorizontalSheet(path));

            Assert.Contains("Right", exception.Message);
            Assert.Contains("(0, 1)", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsAnEmptySlot()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 6; x < 8; x++)
                    {
                        image[x, y] = new ImageSharpColor(0, 0, 0, 0);
                    }
                }

                image.SaveAsPng(path);
            }

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                new AsepriteSpriteSheetImporter().ImportHorizontalSheet(path));

            Assert.Contains("Left", exception.Message);
            Assert.Contains("no opaque pixels", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportsSixExplicitFilesWithACommonCanvas()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"pixel-voxel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            Dictionary<VoxelFace, string> paths = WriteSeparateFiles(directory, 2, 2);

            SixViewImportResult result =
                new AsepriteSpriteSheetImporter().ImportSeparate(paths);

            Assert.Equal(6, result.Views.Count);
            Assert.All(result.Slots, slot =>
            {
                Assert.Equal(2, slot.Width);
                Assert.Equal(2, slot.Height);
                Assert.Equal(4, slot.OpaquePixelCount);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsSeparateFilesWithDifferentCanvasSizes()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"pixel-voxel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            Dictionary<VoxelFace, string> paths = WriteSeparateFiles(directory, 2, 2);
            using (Image<ImageSharpColor> bottom = new(3, 2, new ImageSharpColor(1, 2, 3, 255)))
            {
                bottom.SaveAsPng(paths[VoxelFace.Bottom]);
            }

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                new AsepriteSpriteSheetImporter().ImportSeparate(paths));

            Assert.Contains("common canvas", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Image<ImageSharpColor> CreateOpaqueSheet(int slotWidth, int slotHeight)
    {
        Image<ImageSharpColor> image = new(slotWidth * 6, slotHeight);
        for (int slot = 0; slot < 6; slot++)
        {
            ImageSharpColor color = new((byte)(20 + slot), 0, 0, 255);
            for (int y = 0; y < slotHeight; y++)
            {
                for (int x = 0; x < slotWidth; x++)
                {
                    image[(slot * slotWidth) + x, y] = color;
                }
            }
        }

        return image;
    }

    private static Dictionary<VoxelFace, string> WriteSeparateFiles(
        string directory,
        int width,
        int height)
    {
        Dictionary<VoxelFace, string> paths = [];
        foreach (VoxelFace face in ExpectedOrder)
        {
            string path = Path.Combine(directory, $"{face}.png");
            using Image<ImageSharpColor> image = new(
                width,
                height,
                new ImageSharpColor((byte)(20 + (int)face), 0, 0, 255));
            image.SaveAsPng(path);
            paths.Add(face, path);
        }

        return paths;
    }

    private static string NewTemporaryPngPath() =>
        Path.Combine(Path.GetTempPath(), $"pixel-voxel-{Guid.NewGuid():N}.png");
}
