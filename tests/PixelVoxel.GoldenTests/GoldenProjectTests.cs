using System.Security.Cryptography;
using PixelVoxel.Core;
using PixelVoxel.Export;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;

namespace PixelVoxel.GoldenTests;

public sealed class GoldenProjectTests
{
    [Fact]
    public void GoldenTestDependenciesCanBeLoaded()
    {
        Assert.True(typeof(IProjectSerializer).IsInterface);
        Assert.True(typeof(ISpriteExporter).IsInterface);
        Assert.NotNull(typeof(CpuVoxelRasterizer).Assembly);
    }

    [Fact]
    public void ReferenceSheetMeetsImportNormalizationAndReconstructionAcceptance()
    {
        SixViewImportResult import = ImportReferenceSheet();

        Assert.Equal(6, import.Views.Count);
        Assert.All(import.Slots, slot =>
        {
            Assert.Equal(32, slot.Width);
            Assert.Equal(32, slot.Height);
        });
        Assert.Equal(
            [427, 264, 427, 264, 240, 240],
            import.Slots.Select(slot => slot.OpaquePixelCount));

        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(import.Views);

        Assert.Equal(new VoxelDimensions(20, 22, 12), document.Storage.Dimensions);
        Assert.Equal(5_280, checked(
            document.Storage.Dimensions.Width *
            document.Storage.Dimensions.Height *
            document.Storage.Dimensions.Depth));
        Assert.Equal(5_124, document.Storage.OccupiedCount);
    }

    [Fact]
    public void ReferenceSheetCpuFramesMatchByteExactGoldenHashes()
    {
        SixViewImportResult import = ImportReferenceSheet();
        VoxelDocument document = new VisualHullVoxelReconstructor().Reconstruct(import.Views);
        VoxelMeshData mesh = new VoxelSurfaceMesher()
            .Build(document, TestContext.Current.CancellationToken)
            .Mesh!;
        PixelRenderSettings settings = new();
        VoxelRenderTransformResolver resolver = new();
        CpuVoxelRasterizer rasterizer = new();

        PixelFramebuffer pixelTwoToOne = rasterizer.Render(
            mesh,
            resolver.Resolve(VoxelCameraState.Pixel2To1(), mesh.Dimensions, settings),
            settings);
        VoxelCameraState trueIsometricCamera = VoxelCameraState.Pixel2To1() with
        {
            Preset = VoxelCameraPreset.TrueIsometric,
            PitchDegrees = -35.2643897f,
        };
        PixelFramebuffer trueIsometric = rasterizer.Render(
            mesh,
            resolver.Resolve(trueIsometricCamera, mesh.Dimensions, settings),
            settings);

        string pixelTwoToOneHash = ComputeHash(pixelTwoToOne);
        string trueIsometricHash = ComputeHash(trueIsometric);
        Assert.Equal(
            "F7AEBE08E015EFF67FF542EB6D6DBED3D7AF0951C44F3E73BB6E5F57FBE90756",
            pixelTwoToOneHash);
        Assert.Equal(
            "44F9AB8704DFCC905C9AF6F2AA1F71057A5CDE8A46FE0EF0D48CA47B70713609",
            trueIsometricHash);
    }

    private static SixViewImportResult ImportReferenceSheet() =>
        new AsepriteSpriteSheetImporter().ImportHorizontalSheet(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "ZZZ_Sheet.png"));

    private static string ComputeHash(PixelFramebuffer frame)
    {
        ReadOnlySpan<Rgba32Color> pixels = frame.Pixels.Span;
        byte[] rgba = new byte[checked(pixels.Length * 4)];

        for (int index = 0; index < pixels.Length; index++)
        {
            int offset = index * 4;
            rgba[offset] = pixels[index].Red;
            rgba[offset + 1] = pixels[index].Green;
            rgba[offset + 2] = pixels[index].Blue;
            rgba[offset + 3] = pixels[index].Alpha;
        }

        return Convert.ToHexString(SHA256.HashData(rgba));
    }
}
