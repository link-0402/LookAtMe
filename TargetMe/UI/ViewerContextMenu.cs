using System;
using Dalamud.Bindings.ImGui;
using TargetMe.Core;
using TargetMe.Game;
using TargetMe.Integrations;
using TargetMe.Tracking;

namespace TargetMe.UI;

/// <summary>The right-click menu on a viewer row. Drawn inside the row's popup.</summary>
public sealed class ViewerContextMenu
{
    private readonly Configuration config;
    private readonly ViewerTracker tracker;
    private readonly PlayerActions actions;
    private readonly MoodlesIpc moodles;
    private readonly MoodlesPopup moodlesPopup;

    /// <summary>Camera focus hooks, null when camera focus isn't available.</summary>
    public Func<ViewerRecord, bool>? IsCameraFocused { get; set; }
    public Action<ViewerRecord>? ToggleCameraFocus { get; set; }

    public ViewerContextMenu(Configuration config, ViewerTracker tracker, PlayerActions actions, MoodlesIpc moodles, MoodlesPopup moodlesPopup)
    {
        this.config = config;
        this.tracker = tracker;
        this.actions = actions;
        this.moodles = moodles;
        this.moodlesPopup = moodlesPopup;
    }

    public void Draw(ViewerRecord record)
    {
        var character = tracker.Resolve(record);
        var loaded = character != null;

        ImGui.TextDisabled(record.WorldName.Length > 0 ? $"{record.Name} · {record.WorldName}" : record.Name);
        ImGui.Separator();

        if (ImGui.MenuItem("Send Tell"))
            TellLauncher.Open(record);
        if (ImGui.MenuItem("Target", string.Empty, false, loaded))
            actions.Target(record);
        if (IsCameraFocused != null && ToggleCameraFocus != null)
        {
            var focused = IsCameraFocused(record);
            if (ImGui.MenuItem(focused ? "Stop Focusing" : "Focus Camera", string.Empty, false, loaded || focused))
                ToggleCameraFocus(record);
        }

        ImGui.Separator();

        if (config.MenuDote && ImGui.MenuItem("Dote", string.Empty, false, loaded))
            actions.Dote(record);
        if (config.MenuExamine && ImGui.MenuItem("Examine", string.Empty, false, loaded))
            actions.Examine(record);
        if (config.MenuAdventurePlate && ImGui.MenuItem("Adventure Plate", string.Empty, false, loaded || record.ContentId != 0))
            actions.AdventurePlate(record);
        if (config.MenuGameMenu && ImGui.MenuItem("Game Context Menu", string.Empty, false, loaded))
            actions.GameMenu(record);
        if (config.MenuMoodles && character != null && moodles.Available && moodles.Get(character).Count > 0
            && ImGui.MenuItem("View Moodles"))
            moodlesPopup.Show(record);

        ImGui.Separator();

        if (ImGui.MenuItem("Copy Name"))
            actions.CopyName(record);
    }
}
