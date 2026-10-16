using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TargetMe.Alerts;
using TargetMe.Game;
using TargetMe.Integrations;
using TargetMe.Tracking;

namespace TargetMe.UI;

public sealed class SettingsWindow : Window
{
    private const ImGuiColorEditFlags ColorFlags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar;

    private readonly Configuration config;
    private readonly SoundAlert sound;
    private readonly EmoteWatcher emotes;
    private readonly CameraFocus camera;
    private readonly MoodlesIpc moodles;
    private readonly FileDialogManager fileDialog = new();
    private string editedSoundPath = string.Empty;
    private bool soundPathActive;

    public SettingsWindow(Configuration config, SoundAlert sound, EmoteWatcher emotes, CameraFocus camera,
        MoodlesIpc moodles)
        : base("TargetMe Settings##TargetMeSettings", ImGuiWindowFlags.NoCollapse)
    {
        this.config = config;
        this.sound = sound;
        this.emotes = emotes;
        this.camera = camera;
        this.moodles = moodles;
        Size = new Vector2(580, 430);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        fileDialog.Draw();

        if (!ImGui.BeginTabBar("##tabs"))
            return;

        Tab("General", DrawGeneral);
        Tab("List & Colors", DrawList);
        Tab("Overlay", DrawOverlay);
        Tab("Context Menu", DrawMenu);
        Tab("Sound", DrawSound);
        if (camera.Available)
            Tab("Camera", DrawCamera);
        Tab("Integrations", DrawIntegrations);

        ImGui.EndTabBar();
    }

    private static void Tab(string label, Action draw)
    {
        if (!ImGui.BeginTabItem(label))
            return;
        ImGui.Spacing();
        draw();
        ImGui.EndTabItem();
    }

    private void DrawGeneral()
    {
        Checkbox("Allow moving and resizing the list", config.WindowUnlocked, v => config.WindowUnlocked = v,
            "The list has no title bar. Turn this on to drag it by its background and resize it from the corner, then turn it off again to lock it. \"/targetme move\" does the same.");
        Checkbox("Open the list when logging in", config.OpenOnLogin, v => config.OpenOnLogin = v);
        Checkbox("Hide the list in duties", config.HideInDuty, v => config.HideInDuty = v);

        var opacity = config.BackgroundOpacity;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("Background opacity", ref opacity, 0.2f, 1f, "%.2f"))
            Save(() => config.BackgroundOpacity = opacity);
    }

    private void DrawList()
    {
        var cap = config.ListCap;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt("Players shown", ref cap, 1, 50))
            Save(() => config.ListCap = cap);
        Help("Whoever is targeting you now comes first, then the most recent. The list lasts until you log out.");

        ImGui.Spacing();
        Color("Targeting you now", config.ActiveColor, v => config.ActiveColor = v);
        Color("Looked earlier", config.EarlierColor, v => config.EarlierColor = v);
        Color("No longer nearby", config.AwayColor, v => config.AwayColor = v);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (!emotes.Available)
        {
            ImGui.TextDisabled("Emote detection unavailable on this game version");
            return;
        }

        Checkbox("Show emotes aimed at you", config.ShowEmotes, v => config.ShowEmotes = v);
        var lifetime = config.EmoteLifetimeSeconds;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt("Emotes stay for (seconds)", ref lifetime, 0, 120))
            Save(() => config.EmoteLifetimeSeconds = lifetime);
        Help("0 = keep for the session.");
    }

    private void DrawOverlay()
    {
        ImGui.TextWrapped("While you hover a name (or its menu is open), the player is highlighted in the world.");
        ImGui.Spacing();
        Color("Highlight color", config.OverlayColor, v => config.OverlayColor = v);
        var thickness = config.OverlayThickness;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("Line thickness", ref thickness, 1f, 8f, "%.1f"))
            Save(() => config.OverlayThickness = thickness);
        Checkbox("Line from you to them", config.OverlayLine, v => config.OverlayLine = v);
        Checkbox("Ring at their feet", config.OverlayRing, v => config.OverlayRing = v);
        Checkbox("Marker over their head", config.OverlayMarker, v => config.OverlayMarker = v);
        Checkbox("Gentle pulse", config.OverlayPulse, v => config.OverlayPulse = v);
    }

    private void DrawMenu()
    {
        ImGui.TextWrapped("Send Tell, Target, Focus Camera and Copy Name are always there. Choose the rest:");
        ImGui.Spacing();
        Checkbox("Dote", config.MenuDote, v => config.MenuDote = v);
        Checkbox("Examine", config.MenuExamine, v => config.MenuExamine = v);
        Checkbox("Adventure Plate", config.MenuAdventurePlate, v => config.MenuAdventurePlate = v);
        Checkbox("Game Context Menu", config.MenuGameMenu, v => config.MenuGameMenu = v);
        Checkbox("View Moodles", config.MenuMoodles, v => config.MenuMoodles = v);
    }

    private void DrawSound()
    {
        Checkbox("Play a sound when someone starts targeting you", config.SoundEnabled, v => config.SoundEnabled = v);
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##test", FontAwesomeIcon.Play))
            sound.Test();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Test sound");

        using var disabled = ImRaii.Disabled(!config.SoundEnabled);
        ImGui.Spacing();

        var useCustom = !string.IsNullOrWhiteSpace(config.CustomSoundPath);
        if (!useCustom)
        {
            var effect = config.GameSoundEffect - 1;
            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("Game sound", ref effect, SoundEffectNames, SoundEffectNames.Length))
                Save(() => config.GameSoundEffect = effect + 1);
        }

        ImGui.TextUnformatted("Custom sound file:");
        var path = config.CustomSoundPath ?? string.Empty;
        var buttonWidth = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.FolderOpen, string.Empty);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (buttonWidth + ImGui.GetStyle().ItemSpacing.X));
        // Typed edits are kept in the field and saved once it loses focus, not on every keystroke.
        if (!soundPathActive)
            editedSoundPath = path;
        ImGui.InputTextWithHint("##path", "None: uses the game sound", ref editedSoundPath, 1024);
        soundPathActive = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit())
            Save(() => config.CustomSoundPath = string.IsNullOrWhiteSpace(editedSoundPath) ? null : editedSoundPath.Trim());
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##browse", FontAwesomeIcon.FolderOpen))
        {
            fileDialog.OpenFileDialog("Choose an alert sound", "Audio{.wav,.mp3,.aif,.aiff,.wma,.aac,.m4a}",
                (ok, selected) =>
                {
                    if (ok)
                        Save(() => config.CustomSoundPath = selected);
                });
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("##clear", FontAwesomeIcon.Times))
            Save(() => config.CustomSoundPath = null);

        if (useCustom)
        {
            var volume = config.SoundVolume;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("Volume", ref volume, 0f, 1f, "%.2f"))
                Save(() => config.SoundVolume = volume);
        }

        var cooldown = config.SoundCooldownSeconds;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputInt("Quiet time per player (seconds)", ref cooldown))
            Save(() => config.SoundCooldownSeconds = Math.Clamp(cooldown, 0, 3600));
        Help("The same player won't trigger the sound again within this time.");

        Checkbox("Also when the list is closed", config.SoundWhenWindowClosed, v => config.SoundWhenWindowClosed = v);
        Checkbox("Mute in duties", config.SoundMuteInDuty, v => config.SoundMuteInDuty = v);
    }

    private void DrawCamera()
    {
        ImGui.TextWrapped("Focus Camera (right-click a name) makes the camera orbit that player. Esc, \"/targetme stop\" or the menu again stops it.");
        ImGui.Spacing();
        var distance = config.ZoomDistance;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("Zoom distance", ref distance, 1.5f, 20f, "%.1f"))
            Save(() => config.ZoomDistance = distance);
        Checkbox("Start in front of them", config.StartInFront, v => config.StartInFront = v);
        Checkbox("Allow close-ups", config.AllowCloseUps, v => config.AllowCloseUps = v);
        if (config.AllowCloseUps)
        {
            var limit = config.CloseUpLimit;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("Closest zoom", ref limit, 0.1f, 1.5f, "%.2f"))
                Save(() => config.CloseUpLimit = limit);
        }

        Checkbox("Stop when combat starts", config.StopOnCombat, v => config.StopOnCombat = v);

        var onStop = (int)config.CameraOnStop;
        ImGui.SetNextItemWidth(200);
        if (ImGui.Combo("When it stops", ref onStop, CameraOnStopNames, CameraOnStopNames.Length))
            Save(() => config.CameraOnStop = (CameraOnStop)onStop);
    }

    private void DrawIntegrations()
    {
        ImGui.TextUnformatted("Moodles");
        ImGui.TextDisabled(moodles.Available ? "Loaded: \"View Moodles\" shows in the menu for players with statuses." : "Moodles not loaded");
    }

    private static readonly string[] SoundEffectNames =
    [
        "<se.1>", "<se.2>", "<se.3>", "<se.4>", "<se.5>", "<se.6>", "<se.7>", "<se.8>",
        "<se.9>", "<se.10>", "<se.11>", "<se.12>", "<se.13>", "<se.14>", "<se.15>", "<se.16>",
    ];

    private static readonly string[] CameraOnStopNames = ["Put it back where it was", "Behind me", "Leave it where it is"];

    private void Checkbox(string label, bool value, Action<bool> set, string? help = null)
    {
        if (ImGui.Checkbox(label, ref value))
            Save(() => set(value));
        if (help != null)
            Help(help);
    }

    private void Color(string label, Vector4 value, Action<Vector4> set)
    {
        if (ImGui.ColorEdit4(label, ref value, ColorFlags))
            Save(() => set(value));
    }

    private static void Help(string text)
    {
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(text);
    }

    private void Save(Action change)
    {
        change();
        config.Save();
    }
}
