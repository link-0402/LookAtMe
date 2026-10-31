using System;
using System.Numerics;

namespace LookAtMe.Core;

public static class OverlayGeometry
{
    private const double PulsePeriodSeconds = 1.2;

    /// <summary>
    /// Where on the screen edge (inset by <paramref name="margin"/>) to put an arrow toward a point
    /// that is off-screen. Points behind the camera project mirrored, so they are flipped back.
    /// </summary>
    public static Vector2 EdgePoint(Vector2 screenSize, Vector2 projected, bool behindCamera, float margin)
    {
        var center = screenSize / 2;
        var dir = projected - center;
        if (behindCamera)
            dir = -dir;
        if (dir.LengthSquared() < 1e-6f)
            dir = new Vector2(0, 1);

        var half = center - new Vector2(margin);
        var scaleX = dir.X == 0 ? float.PositiveInfinity : half.X / MathF.Abs(dir.X);
        var scaleY = dir.Y == 0 ? float.PositiveInfinity : half.Y / MathF.Abs(dir.Y);
        return center + dir * MathF.Min(scaleX, scaleY);
    }

    /// <summary>Angle in radians of the direction from the screen centre to <paramref name="edgePoint"/>.</summary>
    public static float EdgeAngle(Vector2 screenSize, Vector2 edgePoint)
    {
        var d = edgePoint - screenSize / 2;
        return MathF.Atan2(d.Y, d.X);
    }

    /// <summary>A gentle 0.75–1.0 alpha pulse.</summary>
    public static float Pulse(double seconds) =>
        (float)(0.875 + 0.125 * Math.Sin(seconds * 2 * Math.PI / PulsePeriodSeconds));
}
