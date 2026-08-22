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
    public void ImportsPartialAlphaWithoutBlockingReconstruction()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                image[2, 1] = new ImageSharpColor(1, 2, 3, 128);
                image.SaveAsPng(path);
            }

            SixViewImportResult result = new AsepriteSpriteSheetImporter().ImportHorizontalSheet(path);

            Assert.Equal(128, result.Views[VoxelFace.Right].GetPixel(0, 1).Alpha);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InspectionPreservesPartialAlphaWithoutAnErrorDiagnostic()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                image[2, 1] = new ImageSharpColor(1, 2, 3, 128);
                image.SaveAsPng(path);
            }

            SixViewImportDraft draft =
                new AsepriteSpriteSheetImporter().InspectHorizontalSheet(path);

            Assert.DoesNotContain(draft.Diagnostics, item => item.Code == "non-binary-alpha");
            Assert.Equal(128, draft.Slots[1].Image.GetPixel(0, 1).Alpha);
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
            Assert.Contains("no visible pixels", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AlignmentCanSwapAndFlipAsymmetricSources()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                image[0, 0] = new ImageSharpColor(200, 10, 20, 255);
                image.SaveAsPng(path);
            }

            AsepriteSpriteSheetImporter importer = new();
            SixViewImportDraft draft = importer.InspectHorizontalSheet(path);
            SixViewAlignment alignment = new(
            [
                new(0, VoxelFace.Back, FlipHorizontal: true, FlipVertical: true),
                new(1, VoxelFace.Right),
                new(2, VoxelFace.Front),
                new(3, VoxelFace.Left),
                new(4, VoxelFace.Top),
                new(5, VoxelFace.Bottom),
            ]);

            SixViewImportResult result = importer.ApplyAlignment(draft, alignment);

            Assert.Equal(new Rgba32Color(200, 10, 20, 255), result.Views[VoxelFace.Back].GetPixel(1, 1));
            Assert.Equal(new Rgba32Color(22, 0, 0, 255), result.Views[VoxelFace.Front].GetPixel(0, 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OffsetUsesFixedCanvasAndReportsClippedOpaquePixels()
    {
        string path = NewTemporaryPngPath();

        try
        {
            using (Image<ImageSharpColor> image = CreateOpaqueSheet(2, 2))
            {
                image.SaveAsPng(path);
            }

            AsepriteSpriteSheetImporter importer = new();
            SixViewImportDraft draft = importer.InspectHorizontalSheet(path);
            SixViewAlignment alignment = new(
                importer.CreateDefaultAlignment(draft).Faces.Select(item =>
                    item.TargetFace == VoxelFace.Front
                        ? item with { OffsetX = 1 }
                        : item));

            SixViewAlignmentPreview preview = importer.PreviewAlignment(draft, alignment);

            Assert.True(preview.CanApply);
            Assert.Contains(preview.Diagnostics, item =>
                item.Code == "clipped-opaque-pixels" &&
                item.Face == VoxelFace.Front &&
                item.Severity == ImportDiagnosticSeverity.Warning);
            Assert.Equal(2, preview.Slots.Single(item => item.Face == VoxelFace.Front).OpaquePixelCount);
            Assert.Equal(2, preview.Views[VoxelFace.Front].Width);
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
    public void ImportsOneExplicitFaceWithoutInventingMissingFaceErrors()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"pixel-voxel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string frontPath = Path.Combine(directory, "front.png");

        try
        {
            using (Image<ImageSharpColor> image = new(2, 3, new ImageSharpColor(1, 2, 3, 128)))
            {
                image.SaveAsPng(frontPath);
            }

            SixViewImportResult result = new AsepriteSpriteSheetImporter().ImportSeparate(
                new Dictionary<VoxelFace, string> { [VoxelFace.Front] = frontPath });

            Assert.Equal(1, result.Views.Count);
            Assert.Single(result.Slots);
            Assert.DoesNotContain(result.Diagnostics, message => message.Contains("missing", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(6, result.Slots[0].OpaquePixelCount);
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

    [Fact]
    public void SeparateFilenameInferenceUsesWholeFaceTokens()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"pixel-voxel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string frontPath = Path.Combine(directory, "hero_front_01.png");
        string unrelatedPath = Path.Combine(directory, "bright.png");

        try
        {
            using (Image<ImageSharpColor> image = new(1, 1, new ImageSharpColor(1, 2, 3, 255)))
            {
                image.SaveAsPng(frontPath);
                image.SaveAsPng(unrelatedPath);
            }

            SixViewImportDraft draft = new AsepriteSpriteSheetImporter()
                .InspectSeparate([frontPath, unrelatedPath]);

            Assert.Equal(VoxelFace.Front, draft.Slots[0].SuggestedFace);
            Assert.Null(draft.Slots[1].SuggestedFace);
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
