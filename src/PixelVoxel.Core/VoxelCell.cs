namespace PixelVoxel.Core;

/// <summary>
/// Represents the semantic place for voxel cell data without fixing its final memory layout.
/// </summary>
public sealed class VoxelCell : IEquatable<VoxelCell>
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

    /// <summary>Creates a cell whose six directional faces use one color.</summary>
    public static VoxelCell CreateUniform(Rgba32Color color) =>
        new(Enum.GetValues<VoxelFace>().Select(face =>
            new KeyValuePair<VoxelFace, Rgba32Color>(face, color)));

    /// <summary>Creates a cell with one directional color replaced.</summary>
    public VoxelCell WithFaceColor(VoxelFace face, Rgba32Color color)
    {
        Dictionary<VoxelFace, Rgba32Color> colors = new(_faceColors)
        {
            [face] = color,
        };
        return new VoxelCell(colors);
    }

    /// <summary>Creates a cell whose existing directional faces use one color.</summary>
    public VoxelCell WithAllFaceColors(Rgba32Color color) =>
        new(_faceColors.Keys.Select(face =>
            new KeyValuePair<VoxelFace, Rgba32Color>(face, color)));

    /// <inheritdoc />
    public bool Equals(VoxelCell? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || _faceColors.Count != other._faceColors.Count) return false;
        return _faceColors.All(pair =>
            other._faceColors.TryGetValue(pair.Key, out Rgba32Color color) && color == pair.Value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as VoxelCell);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (VoxelFace face in Enum.GetValues<VoxelFace>())
        {
            if (_faceColors.TryGetValue(face, out Rgba32Color color))
            {
                hash.Add(face);
                hash.Add(color);
            }
        }

        return hash.ToHashCode();
    }
}
