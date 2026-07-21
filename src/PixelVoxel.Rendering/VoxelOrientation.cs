using System.Numerics;

namespace PixelVoxel.Rendering;

/// <summary>Creates normalized Unity-style orientations and their orthonormal basis vectors.</summary>
public static class VoxelOrientation
{
    public static Quaternion FromYawPitchRoll(float yawDegrees, float pitchDegrees, float rollDegrees)
    {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees) || !float.IsFinite(rollDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(yawDegrees));
        }

        Matrix4x4 rowVectorRotation =
            Matrix4x4.CreateRotationY(ToRadians(yawDegrees)) *
            Matrix4x4.CreateRotationX(ToRadians(-pitchDegrees)) *
            Matrix4x4.CreateRotationZ(ToRadians(rollDegrees));
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rowVectorRotation));
    }

    public static VoxelOrientationBasis GetBasis(Quaternion orientation)
    {
        Quaternion normalized = Normalize(orientation);
        return new VoxelOrientationBasis(
            Vector3.Normalize(Vector3.Transform(Vector3.UnitX, normalized)),
            Vector3.Normalize(Vector3.Transform(Vector3.UnitY, normalized)),
            Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, normalized)));
    }

    public static Quaternion RotateLocal(Quaternion orientation, Vector3 localAxis, float degrees)
    {
        if (!float.IsFinite(degrees) || localAxis.LengthSquared() < 0.000001f)
        {
            throw new ArgumentOutOfRangeException(nameof(degrees));
        }

        Matrix4x4 current = Matrix4x4.CreateFromQuaternion(Normalize(orientation));
        Matrix4x4 delta = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(localAxis), ToRadians(degrees));
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(current * delta));
    }

    public static Quaternion Normalize(Quaternion orientation)
    {
        if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) ||
            !float.IsFinite(orientation.Z) || !float.IsFinite(orientation.W) ||
            orientation.LengthSquared() < 0.000001f)
        {
            throw new ArgumentOutOfRangeException(nameof(orientation));
        }

        return Quaternion.Normalize(orientation);
    }

    private static float ToRadians(float degrees) => degrees * (MathF.PI / 180f);
}

public readonly record struct VoxelOrientationBasis(Vector3 Right, Vector3 Up, Vector3 Forward);
