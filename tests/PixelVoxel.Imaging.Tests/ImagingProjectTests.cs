using System.Reflection;

namespace PixelVoxel.Imaging.Tests;

public sealed class ImagingProjectTests
{
    [Fact]
    public void ImagingAssemblyCanBeLoaded()
    {
        Assembly assembly = Assembly.Load("PixelVoxel.Imaging");

        Assert.Equal("PixelVoxel.Imaging", assembly.GetName().Name);
    }
}

