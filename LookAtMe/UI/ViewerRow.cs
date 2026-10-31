using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LookAtMe.Core;

namespace LookAtMe.UI;

/// <summary>One line in the viewer list: status dot, name, emote chips, time since they last looked.</summary>
public static class ViewerRow
{
    private const float DotRadius = 4f;
    private const float NameIndent = 18f;
    private const float TimePadding = 4f;

    /// <param name="showWorld">Another row has the same name, so the world is shown (dimmed) after the name.</param>
    /// <param name="interactive">
    /// False while the window is unlocked for moving: the row then takes no clicks, so dragging
    /// on it moves the window.
    /// </param>
    /// <returns>Whether the row is hovered or its context menu is open.</returns>
    public static bool Draw(ViewerRecord record, bool showWorld, DateTime now, Configuration config, bool interactive,
        Action<ViewerRecord>? onLeftClick, Action<ViewerRecord, float>? drawChips, Action<ViewerRecord>? drawMenu)
    {
        ImGui.PushID(record.Key);

        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight();
        var end = start + new Vector2(width, height);

        if (record.IsTargeting)
            drawList.AddRectFilled(start, end, ImGui.GetColorU32(config.ActiveColor with { W = 0.12f }), 4f);

        var hovered = false;
        if (interactive)
        {
            ImGui.Selectable("##row", false, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(width, height));
            hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                onLeftClick?.Invoke(record);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup("##ctx");
        }
        else
        {
            ImGui.Dummy(new Vector2(width, height));
        }

        var next = ImGui.GetCursorScreenPos();

        var color = record.IsTargeting ? config.ActiveColor
            : record.IsLoaded ? config.EarlierColor
            : config.AwayColor;
        var colorU32 = ImGui.GetColorU32(color);

        var dotCenter = new Vector2(start.X + NameIndent / 2, start.Y + height / 2);
        if (record.IsTargeting)
            drawList.AddCircleFilled(dotCenter, DotRadius, colorU32);
        else
            drawList.AddCircle(dotCenter, DotRadius, colorU32, 0, 1.5f);

        var textY = start.Y + (height - ImGui.GetTextLineHeight()) / 2;
        var time = record.IsTargeting ? "now" : RelativeTime.Format(now - record.LastSeen);
        var timeWidth = ImGui.CalcTextSize(time).X;
        drawList.AddText(new Vector2(end.X - timeWidth - TimePadding, textY), ImGui.GetColorU32(color with { W = color.W * 0.7f }), time);

        var nameStart = new Vector2(start.X + NameIndent, textY);
        drawList.AddText(nameStart, colorU32, record.Name);
        var nameWidth = ImGui.CalcTextSize(record.Name).X;

        if (showWorld && record.WorldName.Length > 0)
        {
            var world = $"@{record.WorldName}";
            drawList.AddText(nameStart with { X = nameStart.X + nameWidth }, ImGui.GetColorU32(color with { W = color.W * 0.5f }), world);
            nameWidth += ImGui.CalcTextSize(world).X;
        }

        if (drawChips != null)
        {
            ImGui.SetCursorScreenPos(new Vector2(nameStart.X + nameWidth + 6f, start.Y));
            drawChips(record, end.X - timeWidth - TimePadding * 3);
        }

        var menuOpen = false;
        if (ImGui.BeginPopup("##ctx"))
        {
            menuOpen = true;
            drawMenu?.Invoke(record);
            ImGui.EndPopup();
        }

        ImGui.SetCursorScreenPos(next);
        ImGui.PopID();
        return hovered || menuOpen;
    }
}
