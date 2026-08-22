namespace PixelVoxel.Core;

/// <summary>
/// Defines the boundary for reconstructing voxel data from unresolved orthographic inputs.
/// </summary>
public interface IVoxelReconstructor
{
    /// <summary>
    /// Reconstructs a voxel document from one or more orthographic source views.
    /// </summary>
    /// <param name="views">The available source views.</param>
    /// <returns>A reconstructed voxel document.</returns>
    VoxelDocument Reconstruct(OrthographicViewSet views);

    /// <summary>Reconstructs using explicit lengths for axes not observed by the supplied faces.</summary>
    VoxelDocument Reconstruct(OrthographicViewSet views, VoxelReconstructionOptions options);
}
