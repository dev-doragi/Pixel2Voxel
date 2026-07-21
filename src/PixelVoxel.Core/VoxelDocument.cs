namespace PixelVoxel.Core;

/// <summary>
/// Represents an editable Pixel Voxel document without defining persistence or rendering behavior.
/// </summary>
public sealed class VoxelDocument
{
    private readonly EditableVoxelStorage _storage;

    /// <summary>
    /// Initializes a document with reconstructed voxel storage.
    /// </summary>
    /// <param name="storage">The voxel storage owned by the document.</param>
    public VoxelDocument(IVoxelStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = new EditableVoxelStorage(storage.Dimensions, storage.GetOccupiedCells());
    }

    /// <summary>Initializes a document from a representation-neutral voxel snapshot.</summary>
    public VoxelDocument(VoxelDimensions dimensions, IEnumerable<VoxelEntry> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        _storage = new EditableVoxelStorage(dimensions, cells);
    }

    /// <summary>
    /// Gets the voxel storage exposed by this document.
    /// </summary>
    public IVoxelStorage Storage => _storage;

    /// <summary>Gets the revision incremented after each applied edit.</summary>
    public long Revision { get; private set; }

    /// <summary>Occurs after one atomic change set has been applied.</summary>
    public event EventHandler<VoxelDocumentChangedEventArgs>? Changed;

    /// <summary>Applies an already validated, reversible document change atomically.</summary>
    public void Apply(VoxelChangeSet changeSet)
    {
        ArgumentNullException.ThrowIfNull(changeSet);
        if (changeSet.IsEmpty) return;
        _storage.Apply(changeSet);
        Revision++;
        Changed?.Invoke(this, new VoxelDocumentChangedEventArgs(Revision, changeSet));
    }
}

/// <summary>Reports one committed document revision and its reversible changes.</summary>
public sealed record VoxelDocumentChangedEventArgs(long Revision, VoxelChangeSet ChangeSet);
