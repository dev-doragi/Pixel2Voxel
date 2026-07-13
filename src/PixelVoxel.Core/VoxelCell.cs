namespace PixelVoxel.Core;

/// <summary>
/// Represents the semantic place for voxel cell data without fixing its final memory layout.
/// </summary>
public sealed class VoxelCell
{
    private readonly IReadOnlyDictionary<VoxelFace, Rgba32Color> _faceColors;

    /// <summary>
    /// Initializes a reconstructed cell with colors sampled from available source views.
    /// </summary>
    /// <param name="faceColors">Colors keyed by their source orthographic face.</param>
    public VoxelCell(IEnumerable<KeyValuePair<VoxelFace, Rgba32Color>> faceColors)
    {
        ArgumentNullException.ThrowIfNull(faceColors);

        Dictionary<VoxelFace, Rgba32Color> colors = new(faceColors);
        if (colors.Count == 0)
        {
            throw new ArgumentException("At least one face color is required.", nameof(faceColors));
        }

        _faceColors = colors;
    }

    /// <summary>
    /// Gets colors sampled from the source views.
    /// </summary>
    public IReadOnlyDictionary<VoxelFace, Rgba32Color> FaceColors => _faceColors;

    /// <summary>
    /// Gets the color sampled from the matching source face.
    /// </summary>
    /// <param name="face">The rendered voxel face.</param>
    /// <returns>A source color for the face.</returns>
    public Rgba32Color GetColor(VoxelFace face)
    {
        if (_faceColors.TryGetValue(face, out Rgba32Color color))
        {
            return color;
        }

        throw new KeyNotFoundException($"The voxel cell has no {face} source color.");
    }

    /// <summary>Tries to get the color sampled from a source face.</summary>
    public bool TryGetColor(VoxelFace face, out Rgba32Color color) =>
        _faceColors.TryGetValue(face, out color);
}
