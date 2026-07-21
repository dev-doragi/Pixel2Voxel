namespace PixelVoxel.Rendering;

/// <summary>Contains deterministic orbit and animation math independent from UI input events.</summary>
public static class VoxelCameraMotion
{
    /// <summary>The default angular distance used for face-aligned camera detents.</summary>
    public const float DefaultFaceSnapThresholdDegrees = 5f;

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

    /// <summary>Snaps a camera near one of the six axis-aligned voxel faces using its canonical planar orientation.</summary>
    public static VoxelCameraFaceSnap SnapToFace(
        float yawDegrees,
        float pitchDegrees,
        float thresholdDegrees = DefaultFaceSnapThresholdDegrees)
    {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(yawDegrees));
        }

        ValidateThreshold(thresholdDegrees);

        float yaw = WrapAngle(yawDegrees);
        float pitch = Math.Clamp(pitchDegrees, -90f, 90f);
        float topOrBottomDistance = MathF.Abs(90f - MathF.Abs(pitch));
        if (topOrBottomDistance <= thresholdDegrees)
        {
            float nearestQuarterTurnYaw = WrapAngle(Snap(yaw, 90f));
            return new VoxelCameraFaceSnap(
                nearestQuarterTurnYaw,
                MathF.CopySign(90f, pitch),
                true);
        }

        float nearestSideYaw = WrapAngle(Snap(yaw, 90f));
        if (MathF.Abs(pitch) <= thresholdDegrees &&
            AngularDistance(yaw, nearestSideYaw) <= thresholdDegrees)
        {
            return new VoxelCameraFaceSnap(nearestSideYaw, 0f, true);
        }

        return new VoxelCameraFaceSnap(yaw, pitch, false);
    }

    private static float AngularDistance(float first, float second) =>
        MathF.Abs(WrapAngle(first - second));

    private static void ValidateThreshold(float thresholdDegrees)
    {
        if (!float.IsFinite(thresholdDegrees) || thresholdDegrees is <= 0f or >= 45f)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdDegrees));
        }
    }

}

/// <summary>Contains the face-aligned camera result and whether a detent was applied.</summary>
public readonly record struct VoxelCameraFaceSnap(
    float YawDegrees,
    float PitchDegrees,
    bool IsSnapped);
