using System.Numerics;
using TargetMe.Core;

namespace TargetMe.Tests;

public class OverlayGeometryTests
{
    private static readonly Vector2 Screen = new(1920, 1080);

    [Fact]
    public void EdgePointClampsRightSide()
    {
        var p = OverlayGeometry.EdgePoint(Screen, new Vector2(3000, 540), behindCamera: false, margin: 40);

        Assert.Equal(1880, p.X, 0.01f);
        Assert.Equal(540, p.Y, 0.01f);
    }

    [Fact]
    public void BehindCameraMirrors()
    {
        var p = OverlayGeometry.EdgePoint(Screen, new Vector2(960, 300), behindCamera: true, margin: 40);

        Assert.Equal(960, p.X, 0.01f);
        Assert.Equal(1040, p.Y, 0.01f);
    }

    [Fact]
    public void ProjectionAtCenterBehindPointsDown()
    {
        var p = OverlayGeometry.EdgePoint(Screen, new Vector2(960, 540), behindCamera: true, margin: 40);

        Assert.Equal(1040, p.Y, 0.01f);
    }

    [Fact]
    public void EdgeAnglePointsFromCenter()
    {
        Assert.Equal(0f, OverlayGeometry.EdgeAngle(Screen, new Vector2(1880, 540)), 0.001f);
        Assert.Equal(MathF.PI / 2, OverlayGeometry.EdgeAngle(Screen, new Vector2(960, 1040)), 0.001f);
    }

    [Fact]
    public void PulseStaysInRange()
    {
        for (var t = 0.0; t <= 5.0; t += 0.05)
        {
            var v = OverlayGeometry.Pulse(t);
            Assert.InRange(v, 0.75f, 1f);
        }
    }
}
