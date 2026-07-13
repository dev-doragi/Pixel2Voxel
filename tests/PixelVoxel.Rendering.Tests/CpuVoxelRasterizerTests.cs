using PixelVoxel.Core;

namespace PixelVoxel.Rendering.Tests;

public sealed class CpuVoxelRasterizerTests
{
    [Fact]
    public void RendersExactUnshadedSourceFaceColors()
    {
        VoxelMeshData mesh = new VoxelSurfaceMesher()
            .Build(
                VoxelSurfaceMesherTests.CreateSingleVoxelDocument(),
                TestContext.Current.CancellationToken)
            .Mesh!;
        PixelRenderSettings settings = new(width: 64, height: 64, pixelsPerVoxel: 8);
        VoxelRenderTransform transform = new VoxelRenderTransformResolver().Resolve(
            VoxelCameraState.Pixel2To1(),
            mesh.Dimensions,
            settings);

        PixelFramebuffer frame = new CpuVoxelRasterizer().Render(mesh, transform, settings);
        Rgba32Color[] pixels = frame.Pixels.ToArray();

        Assert.Contains(VoxelSurfaceMesherTests.FaceColor(VoxelFace.Front), pixels);
        Assert.Contains(VoxelSurfaceMesherTests.FaceColor(VoxelFace.Right), pixels);
        Assert.Contains(VoxelSurfaceMesherTests.FaceColor(VoxelFace.Top), pixels);
        Assert.DoesNotContain(
            pixels,
            pixel => pixel.Alpha is > 0 and < byte.MaxValue);
    }
}
