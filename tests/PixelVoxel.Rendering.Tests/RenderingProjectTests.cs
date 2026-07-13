using System.Reflection;

namespace PixelVoxel.Rendering.Tests;

public sealed class RenderingProjectTests
{
    [Fact]
    public void RenderingAssemblyAndBothBackendsCanBeLoaded()
    {
        Assembly assembly = Assembly.Load("PixelVoxel.Rendering");

        Assert.Equal("PixelVoxel.Rendering", assembly.GetName().Name);
        Assert.NotNull(assembly.GetType("PixelVoxel.Rendering.CpuVoxelRasterizer"));
        Assert.NotNull(assembly.GetType("PixelVoxel.Rendering.SilkVoxelRenderer"));
    }
}
