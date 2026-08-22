namespace PixelVoxel.Core;

/// <summary>Supplies explicit lengths for model axes not represented by the selected views.</summary>
public sealed record VoxelReconstructionOptions(
    int UnobservedXLength,
    int UnobservedYLength,
    int UnobservedZLength)
{
    /// <summary>Creates validated unobserved-axis lengths.</summary>
    public VoxelReconstructionOptions Validate()
    {
        if (UnobservedXLength <= 0) throw new ArgumentOutOfRangeException(nameof(UnobservedXLength));
        if (UnobservedYLength <= 0) throw new ArgumentOutOfRangeException(nameof(UnobservedYLength));
        if (UnobservedZLength <= 0) throw new ArgumentOutOfRangeException(nameof(UnobservedZLength));
        return this;
    }

    /// <summary>Preserves the legacy one-voxel fallback for callers that do not specify dimensions.</summary>
    public static VoxelReconstructionOptions UnitFallback { get; } = new(1, 1, 1);
}
