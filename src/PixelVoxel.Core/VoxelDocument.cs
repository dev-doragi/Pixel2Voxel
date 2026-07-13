namespace PixelVoxel.Core;

/// <summary>
/// Represents an editable Pixel Voxel document without defining persistence or rendering behavior.
/// </summary>
public sealed class VoxelDocument
{
    /// <summary>
    /// Initializes a document with reconstructed voxel storage.
    /// </summary>
    /// <param name="storage">The voxel storage owned by the document.</param>
    public VoxelDocument(IVoxelStorage storage)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Gets the voxel storage exposed by this document.
    /// </summary>
    public IVoxelStorage Storage { get; }
}
