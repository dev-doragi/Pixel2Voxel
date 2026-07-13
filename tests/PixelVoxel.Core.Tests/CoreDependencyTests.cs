using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class CoreDependencyTests
{
    [Fact]
    public void CoreDoesNotReferenceUiOrRenderingFrameworks()
    {
        string[] references = typeof(VoxelDocument).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        string[] forbiddenPrefixes = ["Avalonia", "Silk.NET", "PixelVoxel.App"];

        foreach (string prefix in forbiddenPrefixes)
        {
            Assert.DoesNotContain(
                references,
                reference => reference.StartsWith(prefix, StringComparison.Ordinal));
        }
    }
}

