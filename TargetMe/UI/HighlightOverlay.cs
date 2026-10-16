using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility;
using TargetMe.Core;

namespace TargetMe.UI;

/// <summary>
/// Lights up a player in the world: a line from the local player to them, a ring at their feet,
/// a marker over their head, and an arrow at the screen edge when they're off-screen.
/// </summary>
public sealed class HighlightOverlay
{
    private const int LineSegments = 24;
    private const int RingSegments = 32;
    private const float RingRadius = 0.7f;
    private const float ChestHeight = 1.2f;
    private const float MarkerHeight = 2.1f;
    private const float EdgeMargin = 48f;

    public void Draw(IPlayerCharacter target, IPlayerCharacter self, Configuration config)
    {
        var drawList = ImGui.GetBackgroundDrawList();
        var alpha = config.OverlayPulse ? OverlayGeometry.Pulse(ImGui.GetTime()) : 1f;
        var color = ImGui.GetColorU32(config.OverlayColor with { W = config.OverlayColor.W * alpha });
        var glow = ImGui.GetColorU32(config.OverlayColor with { W = config.OverlayColor.W * alpha * 0.3f });
        var thickness = config.OverlayThickness;

        var targetChest = target.Position + new Vector3(0, ChestHeight, 0);

        if (config.OverlayLine)
        {
            var from = self.Position + new Vector3(0, ChestHeight, 0);
            DrawWorldPolyline(drawList, i => Vector3.Lerp(from, targetChest, i / (float)LineSegments), LineSegments, glow, thickness * 3f);
            DrawWorldPolyline(drawList, i => Vector3.Lerp(from, targetChest, i / (float)LineSegments), LineSegments, color, thickness);
        }

        if (config.OverlayRing)
        {
            Vector3 RingPoint(int i)
            {
                var a = i * MathF.Tau / RingSegments;
                return target.Position + new Vector3(MathF.Cos(a) * RingRadius, 0.05f, MathF.Sin(a) * RingRadius);
            }

            DrawWorldPolyline(drawList, RingPoint, RingSegments, glow, thickness * 3f);
            DrawWorldPolyline(drawList, RingPoint, RingSegments, color, thickness);
        }

        if (config.OverlayMarker
            && Services.GameGui.WorldToScreen(target.Position + new Vector3(0, MarkerHeight, 0), out var head))
        {
            var scale = ImGuiHelpers.GlobalScale;
            var tip = head;
            var left = head + new Vector2(-14, -24) * scale;
            var right = head + new Vector2(14, -24) * scale;
            drawList.AddTriangleFilled(left, right, tip, color);
            drawList.AddTriangle(left, right, tip, ImGui.GetColorU32(new Vector4(0, 0, 0, 0.6f * alpha)), 1.5f);
        }

        var inFront = Services.GameGui.WorldToScreen(targetChest, out var projected, out var inView);
        if (!inFront || !inView)
            DrawEdgeArrow(drawList, target.Name.TextValue, projected, !inFront, color);
    }

    private static void DrawWorldPolyline(ImDrawListPtr drawList, Func<int, Vector3> point, int segments, uint color, float thickness)
    {
        var prevFront = Services.GameGui.WorldToScreen(point(0), out var prev, out _);
        for (var i = 1; i <= segments; i++)
        {
            var front = Services.GameGui.WorldToScreen(point(i), out var cur, out _);
            if (prevFront && front)
                drawList.AddLine(prev, cur, color, thickness);
            prev = cur;
            prevFront = front;
        }
    }

    private static void DrawEdgeArrow(ImDrawListPtr drawList, string name, Vector2 projected, bool behind, uint color)
    {
        var viewport = ImGui.GetMainViewport();
        var local = OverlayGeometry.EdgePoint(viewport.Size, projected - viewport.Pos, behind, EdgeMargin);
        var angle = OverlayGeometry.EdgeAngle(viewport.Size, local);
        var at = local + viewport.Pos;

        var scale = ImGuiHelpers.GlobalScale;
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var side = new Vector2(-dir.Y, dir.X);
        var tip = at + dir * 20 * scale;
        var baseCenter = at - dir * 10 * scale;
        drawList.AddTriangleFilled(tip, baseCenter + side * 15 * scale, baseCenter - side * 15 * scale, color);

        var textSize = ImGui.CalcTextSize(name);
        var textPos = at - dir * (30 * scale + textSize.Length() / 2) - textSize / 2;
        drawList.AddRectFilled(textPos - new Vector2(4, 2), textPos + textSize + new Vector2(4, 2), ImGui.GetColorU32(new Vector4(0, 0, 0, 0.55f)), 4f);
        drawList.AddText(textPos, color, name);
    }
}
