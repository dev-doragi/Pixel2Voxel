namespace PixelVoxel.App.ViewModels;

/// <summary>Converts relative directional pointer movement into a gizmo rotation delta.</summary>
public static class GizmoDragMotion
{
    public const float DegreesPerPixel = 0.7f;

    public static float GetDegrees(double deltaX, double deltaY)
    {
        double signedDistance = Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX : deltaY;
        if (!double.IsFinite(signedDistance)) return 0f;
        return (float)signedDistance * DegreesPerPixel;
    }
}
