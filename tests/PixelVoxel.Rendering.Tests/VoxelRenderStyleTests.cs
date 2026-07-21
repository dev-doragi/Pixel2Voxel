using PixelVoxel.Core;

namespace PixelVoxel.Rendering.Tests;

public sealed class VoxelRenderStyleTests
{
    [Fact]
    public void DisabledLightingKeepsExactSourceRgb()
    {
        VoxelMeshData mesh = CreateMesh();
        Rgba32Color background = new(1, 2, 3, 255);
        PixelFramebuffer frame = Render(
            mesh,
            new VoxelRenderStyle(
                background,
                VoxelLightingSettings.Default with { Enabled = false },
                VoxelOutlineSettings.Default with { Enabled = false }));
        HashSet<Rgba32Color> allowed = Enum.GetValues<VoxelFace>()
            .Select(VoxelSurfaceMesherTests.FaceColor)
            .Append(background)
            .ToHashSet();

        Assert.All(frame.Pixels.ToArray(), pixel => Assert.Contains(pixel, allowed));
        Assert.Contains(frame.Pixels.ToArray(), pixel => pixel != background);
    }

    [Fact]
    public void LightingProducesOnlyFourQuantizedBrightnessLevels()
    {
        VoxelMeshData mesh = CreateMesh();
        Rgba32Color background = new(1, 2, 3, 255);
        PixelFramebuffer frame = Render(
            mesh,
            new VoxelRenderStyle(
                background,
                new VoxelLightingSettings(true, -45f, 45f, 0f, 1f),
                VoxelOutlineSettings.Default with { Enabled = false }));
        HashSet<Rgba32Color> allowed = [background];
        foreach (VoxelFace face in Enum.GetValues<VoxelFace>())
        {
            Rgba32Color source = VoxelSurfaceMesherTests.FaceColor(face);
            foreach (float level in new[] { 0f, 1f / 3f, 2f / 3f, 1f })
            {
                allowed.Add(Scale(source, level));
            }
        }

        Assert.All(frame.Pixels.ToArray(), pixel => Assert.Contains(pixel, allowed));
    }

    [Fact]
    public void SilhouetteUsesCoverageWhenBackgroundMatchesTheModelColor()
    {
        VoxelMeshData mesh = CreateMesh();
        Rgba32Color modelColor = VoxelSurfaceMesherTests.FaceColor(VoxelFace.Front);
        Rgba32Color outlineColor = new(255, 0, 255, 255);
        PixelRenderLayout layout = new PixelRenderLayoutResolver().Resolve(mesh.Dimensions, 8, 8);
        PixelFramebuffer frame = new PixelArtVoxelRasterizer().Render(
            mesh,
            FrontCamera(),
            layout,
            new VoxelRenderStyle(
                modelColor,
                VoxelLightingSettings.Default with { Enabled = false },
                new VoxelOutlineSettings(true, VoxelOutlineMode.Silhouette, outlineColor, 0.5f)));

        Assert.Contains(outlineColor, frame.Pixels.ToArray());
    }

    [Fact]
    public void SilhouetteOutlineStaysWithinOnePixelOfCoverage()
    {
        VoxelMeshData mesh = CreateMesh();
        Rgba32Color background = new(1, 2, 3, 255);
        Rgba32Color outlineColor = new(255, 0, 255, 255);
        PixelRenderLayout layout = new PixelRenderLayoutResolver().Resolve(mesh.Dimensions, 8, 8);
        VoxelLightingSettings noLighting = VoxelLightingSettings.Default with { Enabled = false };
        PixelArtVoxelRasterizer rasterizer = new();
        PixelFramebuffer reference = rasterizer.Render(
            mesh,
            FrontCamera(),
            layout,
            new VoxelRenderStyle(
                background,
                noLighting,
                VoxelOutlineSettings.Default with { Enabled = false }));
        PixelFramebuffer outlined = rasterizer.Render(
            mesh,
            FrontCamera(),
            layout,
            new VoxelRenderStyle(
                background,
                noLighting,
                new VoxelOutlineSettings(true, VoxelOutlineMode.Silhouette, outlineColor, 0.5f)));
        bool[] coverage = reference.Pixels.ToArray().Select(pixel => pixel != background).ToArray();
        int[] outlineIndices = outlined.Pixels.ToArray()
            .Select((pixel, index) => (pixel, index))
            .Where(item => item.pixel == outlineColor)
            .Select(item => item.index)
            .ToArray();

        Assert.NotEmpty(outlineIndices);
        Assert.All(outlineIndices, index => Assert.True(HasCoveredNeighbor(index, outlined.Width, outlined.Height, coverage)));
    }

    [Fact]
    public void EditorSelectionAndHoverUseViewportOnlyPixelOutlines()
    {
        VoxelMeshData baseMesh = CreateMesh();
        VoxelSelectionBox selection = new(new VoxelCoordinate(0, 0, 0), new VoxelCoordinate(0, 0, 0));
        VoxelMeshData selected = baseMesh.WithEditorOverlay(selection, null);
        VoxelMeshData hovered = baseMesh.WithEditorOverlay(
            null,
            new VoxelPickResult(
                new VoxelCoordinate(0, 0, 0),
                VoxelFace.Front,
                new VoxelCoordinate(0, 0, 1),
                1f));
        VoxelRenderStyle style = VoxelRenderStyle.Default with
        {
            Lighting = VoxelLightingSettings.Default with { Enabled = false },
            Outline = VoxelOutlineSettings.Default with { Enabled = false },
        };

        PixelFramebuffer selectedFrame = Render(selected, style);
        PixelFramebuffer hoveredFrame = new PixelArtVoxelRasterizer().Render(
            hovered,
            FrontCamera(),
            new PixelRenderLayoutResolver().Resolve(hovered.Dimensions, 8, 8),
            style);

        Assert.Contains(new Rgba32Color(0, 215, 255, 255), selectedFrame.Pixels.ToArray());
        Assert.Contains(new Rgba32Color(255, 213, 74, 255), hoveredFrame.Pixels.ToArray());
        Assert.DoesNotContain(baseMesh.Vertices.ToArray(), vertex => vertex.EditorMask != 0f);
    }

    [Fact]
    public void BrushStrokePreviewMarksEverySampleWithoutChangingBaseMesh()
    {
        VoxelMeshData baseMesh = CreateMesh();
        VoxelPickResult sample = new(
            new VoxelCoordinate(0, 0, 0),
            VoxelFace.Front,
            new VoxelCoordinate(0, 0, 1),
            1f);

        VoxelMeshData preview = baseMesh.WithEditorOverlay(null, null, [sample]);

        Assert.Contains(preview.Vertices.ToArray(), vertex => vertex.EditorMask == 1f);
        Assert.DoesNotContain(baseMesh.Vertices.ToArray(), vertex => vertex.EditorMask != 0f);
    }

    [Fact]
    public void PaintBrushPreviewUsesTheSelectedColorInsteadOfYellowMask()
    {
        VoxelMeshData baseMesh = CreateMesh();
        Rgba32Color paint = new(220, 30, 80, 255);
        VoxelPickResult sample = new(
            new VoxelCoordinate(0, 0, 0),
            VoxelFace.Front,
            new VoxelCoordinate(0, 0, 1),
            1f);

        VoxelMeshData preview = baseMesh.WithEditorOverlay(null, null, [sample], paint);
        VoxelMeshVertex[] front = preview.Vertices.ToArray()[..4];

        Assert.All(front, vertex => Assert.Equal(paint, vertex.Color));
        Assert.All(front, vertex => Assert.Equal(0f, vertex.EditorMask));
    }

    private static PixelFramebuffer Render(VoxelMeshData mesh, VoxelRenderStyle style)
    {
        PixelRenderLayout layout = new PixelRenderLayoutResolver().Resolve(mesh.Dimensions, 8, 8);
        return new PixelArtVoxelRasterizer().Render(mesh, VoxelCameraState.Pixel2To1(), layout, style);
    }

    private static VoxelMeshData CreateMesh() =>
        new VoxelSurfaceMesher()
            .Build(VoxelSurfaceMesherTests.CreateSingleVoxelDocument(), TestContext.Current.CancellationToken)
            .Mesh!;

    private static VoxelCameraState FrontCamera() =>
        new(VoxelViewMode.PixelPreview, VoxelCameraPreset.Front, 0f, 0f, 0f, 0f, 1f);

    private static Rgba32Color Scale(Rgba32Color color, float brightness) =>
        new(
            (byte)MathF.Round(color.Red * brightness, MidpointRounding.AwayFromZero),
            (byte)MathF.Round(color.Green * brightness, MidpointRounding.AwayFromZero),
            (byte)MathF.Round(color.Blue * brightness, MidpointRounding.AwayFromZero),
            color.Alpha);

    private static bool HasCoveredNeighbor(int index, int width, int height, bool[] coverage)
    {
        int x = index % width;
        int y = index / width;
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int neighborX = x + offsetX;
                int neighborY = y + offsetY;
                if ((offsetX != 0 || offsetY != 0) &&
                    neighborX >= 0 && neighborY >= 0 && neighborX < width && neighborY < height &&
                    coverage[(neighborY * width) + neighborX])
                {
                    return true;
                }
            }
        }

        return false;
    }
}
