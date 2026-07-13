namespace PixelVoxel.Rendering;

/// <summary>Contains deterministic orbit and animation math independent from UI input events.</summary>
public static class VoxelCameraMotion
{
    /// <summary>Applies the user-facing horizontal drag direction and wraps the result.</summary>
    public static float RotateYaw(float yawDegrees, float deltaX, float sensitivity) =>
        WrapAngle(yawDegrees + (deltaX * sensitivity));

    /// <summary>Applies the reversed vertical drag direction and clamps inspection pitch.</summary>
    public static float RotatePitch(float pitchDegrees, float deltaY, float sensitivity) =>
        Math.Clamp(pitchDegrees - (deltaY * sensitivity), -89f, 89f);

    /// <summary>Snaps a render angle to a deterministic step.</summary>
    public static float Snap(float angle, float stepDegrees = 1f)
    {
        if (!float.IsFinite(stepDegrees) || stepDegrees <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(stepDegrees));
        }

        return MathF.Round(angle / stepDegrees, MidpointRounding.AwayFromZero) * stepDegrees;
    }

    /// <summary>Wraps a full-rotation angle to the half-open interval [-180, 180).</summary>
    public static float WrapAngle(float angle)
    {
        float wrapped = (angle + 180f) % 360f;
        if (wrapped < 0f) wrapped += 360f;
        return wrapped - 180f;
    }
}
