using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using TargetMe.Core;
using TargetMe.Tracking;

namespace TargetMe.UI;

/// <summary>The header-less list of everyone who has looked at the local player.</summary>
public sealed class ViewerWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing;
    private const ImGuiWindowFlags LockedFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

    private readonly Configuration config;
    private readonly ViewerTracker tracker;

    private ViewerRecord? hovered;
    private int hoveredFrame = -10;

    /// <summary>
    /// The row hovered, or whose context menu is open, during this or the previous frame.
    /// Null once the window stops drawing (closed, or hidden with the game UI).
    /// </summary>
    public ViewerRecord? Hovered => ImGui.GetFrameCount() - hoveredFrame <= 1 ? hovered : null;

    public Action<ViewerRecord>? OnLeftClick { get; set; }
    public Action<ViewerRecord>? DrawContextMenu { get; set; }
    /// <summary>Draws a row's emote chips from the cursor; the float is the x they must stay left of.</summary>
    public Action<ViewerRecord, float>? DrawEmoteChips { get; set; }
    public Action? OpenSettings { get; set; }

    public ViewerWindow(Configuration config, ViewerTracker tracker) : base("TargetMe##viewers", BaseFlags)
    {
        this.config = config;
        this.tracker = tracker;
        Size = new Vector2(240, 150);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(150, 40), MaximumSize = new Vector2(800, 1000) };
        RespectCloseHotkey = false;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    public override void PreDraw()
    {
        Flags = config.WindowUnlocked ? BaseFlags : BaseFlags | LockedFlags;
        BgAlpha = config.BackgroundOpacity;
    }

    public override void Draw()
    {
        hovered = null;
        hoveredFrame = ImGui.GetFrameCount();
        var now = DateTime.Now;
        var rows = ViewerList.Visible(tracker.All, config.ListCap);

        if (rows.Count == 0)
        {
            ImGui.TextDisabled("No one is looking at you.");
        }
        else
        {
            foreach (var row in rows)
            {
                if (ViewerRow.Draw(row, ViewerList.HasNamesake(rows, row), now, config, !config.WindowUnlocked,
                        OnLeftClick, DrawEmoteChips, DrawContextMenu))
                    hovered = row;
            }
        }

        if (config.WindowUnlocked)
            DrawDashedBorder();

        if (OpenSettings != null && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
            DrawSettingsButton();
    }

    private void DrawSettingsButton()
    {
        var min = ImGui.GetWindowContentRegionMin();
        var max = ImGui.GetWindowContentRegionMax();
        var size = ImGui.GetFrameHeight();
        ImGui.SetCursorPos(new Vector2(max.X - size, min.Y));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        if (ImGuiComponents.IconButton("##settings", FontAwesomeIcon.Cog))
            OpenSettings?.Invoke();
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("TargetMe settings");
    }

    private void DrawDashedBorder()
    {
        const float dash = 6f, gap = 4f, inset = 1f;
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos() + new Vector2(inset);
        var max = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(inset);
        var color = ImGui.GetColorU32(config.ActiveColor with { W = 0.6f });

        drawList.PushClipRectFullScreen();
        DashedLine(drawList, min, new Vector2(max.X, min.Y));
        DashedLine(drawList, new Vector2(max.X, min.Y), max);
        DashedLine(drawList, max, new Vector2(min.X, max.Y));
        DashedLine(drawList, new Vector2(min.X, max.Y), min);
        drawList.PopClipRect();

        void DashedLine(ImDrawListPtr list, Vector2 from, Vector2 to)
        {
            var length = Vector2.Distance(from, to);
            var dir = (to - from) / length;
            for (var d = 0f; d < length; d += dash + gap)
                list.AddLine(from + dir * d, from + dir * MathF.Min(d + dash, length), color, 1.5f);
        }
    }
}
