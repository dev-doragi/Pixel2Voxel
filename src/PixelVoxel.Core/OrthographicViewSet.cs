namespace PixelVoxel.Core;

/// <summary>
/// Groups the orthographic source images available for one reconstruction.
/// </summary>
public sealed class OrthographicViewSet
{
    private readonly Dictionary<VoxelFace, OrthographicImage> _views;

    /// <summary>
    /// Initializes a non-empty source view set.
    /// </summary>
    public OrthographicViewSet(IEnumerable<KeyValuePair<VoxelFace, OrthographicImage>> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        _views = new Dictionary<VoxelFace, OrthographicImage>(views);

        if (_views.Count == 0)
        {
            throw new ArgumentException("At least one orthographic view is required.", nameof(views));
        }
    }

    /// <summary>Gets the number of supplied views.</summary>
    public int Count => _views.Count;

    /// <summary>Gets the supplied face names.</summary>
    public IReadOnlyCollection<VoxelFace> Faces => _views.Keys;

    /// <summary>Enumerates supplied face-image pairs.</summary>
    public IEnumerable<KeyValuePair<VoxelFace, OrthographicImage>> Views => _views;

    /// <summary>Gets a supplied image by face.</summary>
    public OrthographicImage this[VoxelFace face] => _views[face];

    /// <summary>Tries to get a supplied image by face.</summary>
    public bool TryGetView(VoxelFace face, out OrthographicImage? image) =>
        _views.TryGetValue(face, out image);
}

