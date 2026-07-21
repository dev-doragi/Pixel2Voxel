using System.Numerics;

namespace PixelVoxel.Rendering;

/// <summary>Stores a normalized object-local orientation with compatibility Euler values.</summary>
public sealed record VoxelModelRotationState
{
    public VoxelModelRotationState(float yawDegrees, float pitchDegrees, float rollDegrees = 0f)
    {
        YawDegrees = VoxelCameraMotion.WrapAngle(yawDegrees);
        PitchDegrees = VoxelCameraMotion.WrapAngle(pitchDegrees);
        RollDegrees = VoxelCameraMotion.WrapAngle(rollDegrees);
    }

    private VoxelModelRotationState(
        float yawDegrees,
        float pitchDegrees,
        float rollDegrees,
        Quaternion orientation)
    {
        YawDegrees = VoxelCameraMotion.WrapAngle(yawDegrees);
        PitchDegrees = VoxelCameraMotion.WrapAngle(pitchDegrees);
        RollDegrees = VoxelCameraMotion.WrapAngle(rollDegrees);
        _orientation = VoxelOrientation.Normalize(orientation);
    }

    private readonly Quaternion? _orientation;

    public float YawDegrees { get; init; }
    public float PitchDegrees { get; init; }
    public float RollDegrees { get; init; }

    /// <summary>Gets the authoritative normalized object-local orientation.</summary>
    public Quaternion Orientation => _orientation ??
        VoxelOrientation.FromYawPitchRoll(YawDegrees, PitchDegrees, RollDegrees);

    /// <summary>Applies a rotation around an axis in the current object-local frame.</summary>
    public VoxelModelRotationState RotateLocal(Vector3 localAxis, float degrees)
    {
        float yaw = YawDegrees;
        float pitch = PitchDegrees;
        float roll = RollDegrees;
        if (localAxis == Vector3.UnitX) pitch = VoxelCameraMotion.WrapAngle(pitch + degrees);
        else if (localAxis == Vector3.UnitY) yaw = VoxelCameraMotion.WrapAngle(yaw + degrees);
        else if (localAxis == Vector3.UnitZ) roll = VoxelCameraMotion.WrapAngle(roll + degrees);
        float appliedDegrees = localAxis == Vector3.UnitX ? -degrees : degrees;
        return new VoxelModelRotationState(
            yaw,
            pitch,
            roll,
            VoxelOrientation.RotateLocal(Orientation, localAxis, appliedDegrees));
    }

    /// <summary>Applies a rotation around an axis expressed in world space.</summary>
    public VoxelModelRotationState RotateWorld(Vector3 worldAxis, float degrees)
    {
        if (!float.IsFinite(degrees) || worldAxis.LengthSquared() < 0.000001f)
        {
            throw new ArgumentOutOfRangeException(nameof(degrees));
        }

        Matrix4x4 delta = Matrix4x4.CreateFromAxisAngle(
            Vector3.Normalize(worldAxis),
            degrees * (MathF.PI / 180f));
        Matrix4x4 current = Matrix4x4.CreateFromQuaternion(Orientation);
        return new VoxelModelRotationState(
            YawDegrees,
            PitchDegrees,
            VoxelCameraMotion.WrapAngle(RollDegrees + degrees),
            Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(delta * current)));
    }

    /// <summary>Gets the unrotated model orientation.</summary>
    public static VoxelModelRotationState Identity { get; } = new(0f, 0f, 0f);
}
