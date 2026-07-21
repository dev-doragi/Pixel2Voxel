namespace PixelVoxel.Rendering;

/// <summary>Stores object-local rotation independently from the viewport camera.</summary>
public sealed record VoxelModelRotationState(
    float YawDegrees,
    float PitchDegrees,
    float RollDegrees = 0f)
{
    /// <summary>Gets the unrotated model orientation.</summary>
    public static VoxelModelRotationState Identity { get; } = new(0f, 0f, 0f);
}
