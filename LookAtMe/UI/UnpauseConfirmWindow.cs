using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;

namespace LookAtMe.UI;

/// <summary>Asks before unpausing someone through the sync pause hotkey. Esc cancels.</summary>
public sealed class UnpauseConfirmWindow : Window
{
    private ulong objectId;
    private string name = string.Empty;
    private IReadOnlyList<string> plugins = [];
    private bool justOpened;

    public UnpauseConfirmWindow()
        : base("Look At Me##UnpauseConfirm",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)
    {
        ShowCloseButton = false;
    }

    /// <summary>Called with the player's object id and name and the plugins to unpause them in.</summary>
    public Action<ulong, string, IReadOnlyList<string>>? OnConfirm { get; set; }

    public void Open(IPlayerCharacter player, IReadOnlyList<string> plugins)
    {
        objectId = player.GameObjectId;
        name = player.Name.TextValue;
        this.plugins = plugins;
        justOpened = true;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        // Every frame, not just on appearing: auto-resize widens it after the first frame.
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos + viewport.Size / 2, ImGuiCond.Always, new Vector2(0.5f));
        if (justOpened)
        {
            ImGui.SetNextWindowFocus();
            justOpened = false;
        }
    }

    public override void Draw()
    {
        ImGui.TextUnformatted($"Unpause {name} in {string.Join(" and ", plugins)}?");
        ImGui.Spacing();

        if (ImGui.Button("Unpause"))
        {
            IsOpen = false;
            OnConfirm?.Invoke(objectId, name, plugins);
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel")
            || (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsKeyPressed(ImGuiKey.Escape)))
            IsOpen = false;
    }
}
