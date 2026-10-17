using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.ImGuiNotification;
using TargetMe.Core;
using TargetMe.Integrations.Sync;
using TargetMe.Tracking;
using TargetMe.UI;

namespace TargetMe.Game;

/// <summary>
/// The sync pause hotkey: while held, marks the player under the cursor with their sync state;
/// on release, pauses them where they're synced and asks before unpausing them where they're paused.
/// </summary>
public sealed class PauseHotkey : IDisposable
{
    private const int KeyDownBit = 0x8000;
    private const int VkEscape = 0x1B;
    private const int VkMouse4 = 0x05;
    private const int VkMouse5 = 0x06;

    private readonly Configuration config;
    private readonly ViewerTracker tracker;
    private readonly ViewerWindow viewerWindow;
    private readonly IReadOnlyList<ISyncService> services;
    private readonly UnpauseConfirmWindow confirm;
    private readonly HoldTracker hold = new();
    private IPlayerCharacter? target;
    private IPlayerCharacter? previewTarget;
    private IReadOnlyList<SyncPluginState>? previewStates;
    private bool hidingKey;
    private bool disposed;

    public PauseHotkey(Configuration config, ViewerTracker tracker, ViewerWindow viewerWindow,
        IReadOnlyList<ISyncService> services, UnpauseConfirmWindow confirm)
    {
        this.config = config;
        this.tracker = tracker;
        this.viewerWindow = viewerWindow;
        this.services = services;
        this.confirm = confirm;
        confirm.OnConfirm = OnConfirm;
    }

    /// <summary>True when the marker was drawn this frame (the hover overlay then stays out of the way).</summary>
    public bool HasTarget { get; private set; }

    /// <summary>Set by settings while it records a new binding; the hotkey stays inactive meanwhile.</summary>
    public bool Capturing { get; set; }

    public void Update()
    {
        var binding = config.PauseHotkey;
        var phase = binding.Phase(IsDown);
        var released = hold.Update(phase, IsActive(binding));

        // Keep the game from acting on the key while it's ours, until it's physically up (even if the
        // hold ended early). Mouse buttons aren't in its key array.
        hidingKey = hold.Holding || (hidingKey && phase != KeyPhase.Up);
        if (hidingKey && !binding.IsMouse && Services.KeyState.IsVirtualKeyValid((VirtualKey)binding.Key))
            Services.KeyState[(VirtualKey)binding.Key] = false;

        target = hold.Holding || released ? FindTarget() : null;
        if (released && target != null)
            Release(target);
        if (released)
            target = null;
    }

    public void Draw(HighlightOverlay overlay)
    {
        HasTarget = false;
        if (Services.ObjectTable.LocalPlayer is not { } self)
            return;

        IPlayerCharacter who;
        IReadOnlyList<SyncPluginState> states;
        if (previewTarget != null && previewStates != null)
        {
            if (!previewTarget.IsValid())
                return;
            who = previewTarget;
            states = previewStates;
        }
        else
        {
            if (!hold.Holding || target == null || !target.IsValid())
                return;
            who = target;
            states = States(target, fresh: false);
        }

        var color = SyncPauseRules.Marker(states) switch
        {
            MarkerKind.Pause => config.PauseColor,
            MarkerKind.Unpause => config.UnpauseColor,
            _ => config.NotPairedColor,
        };
        overlay.Draw(who, self, OverlayStyle.From(config) with
        {
            Color = color,
            Line = false,
            Ring = true,
            Marker = true,
            Label = SyncPauseRules.Label(states),
        });
        HasTarget = true;
    }

    /// <summary>Test seam: draws the marker on <paramref name="target"/> with fixed states. Null clears it.</summary>
    internal void Preview(IPlayerCharacter? target, IReadOnlyList<SyncPluginState>? states)
    {
        previewTarget = target;
        previewStates = states;
    }

    /// <summary>
    /// While capturing: the first non-modifier key (or Mouse 4/5) that's down, with the current modifiers.
    /// Esc cancels. Returns null while nothing is pressed yet, and after cancelling.
    /// </summary>
    public HotkeyBinding? PollCapture()
    {
        if (IsDown(VkEscape))
        {
            Capturing = false;
            return null;
        }

        foreach (var vk in CaptureCandidates())
        {
            if (!IsDown(vk))
                continue;

            Capturing = false;
            // The key just recorded is still down; it only counts after it's been let go.
            hold.Reset();
            return new HotkeyBinding
            {
                Key = vk,
                Ctrl = IsDown(HotkeyBinding.VkControl),
                Shift = IsDown(HotkeyBinding.VkShift),
                Alt = IsDown(HotkeyBinding.VkMenu),
            };
        }

        return null;
    }

    // Keyboard keys the game knows, plus the mouse side buttons; never the left/right/middle button, Esc or a modifier.
    private static IEnumerable<int> CaptureCandidates() =>
        Services.KeyState.GetValidVirtualKeys()
            .Select(k => (int)k)
            .Concat([VkMouse4, VkMouse5])
            .Where(vk => vk > 0x04 && vk != VkEscape && !HotkeyBinding.IsModifier(vk))
            .Distinct();

    /// <summary>Whether a key (virtual-key code) is down, and only while the game window is in front.</summary>
    public static bool IsDown(int vk) => IsGameForeground() && (GetAsyncKeyState(vk) & KeyDownBit) != 0;

    // By process rather than by window handle, so an ImGui window dragged outside the game still counts.
    private static bool IsGameForeground()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    public void Dispose()
    {
        disposed = true;
        confirm.OnConfirm = null;
    }

    private bool IsActive(HotkeyBinding binding) =>
        !binding.IsEmpty
        && !Capturing
        && !confirm.IsOpen
        && IsGameForeground()
        && Services.ClientState.IsLoggedIn
        && Services.ObjectTable.LocalPlayer != null
        && !TextInput.IsActive()
        && !Services.ClientState.IsGPosing
        && !Services.Condition[ConditionFlag.WatchingCutscene]
        && !Services.Condition[ConditionFlag.WatchingCutscene78]
        && !Services.Condition[ConditionFlag.OccupiedInCutSceneEvent];

    /// <summary>The viewer-list row under the cursor, else the game's mouseover player (not yourself).</summary>
    private IPlayerCharacter? FindTarget()
    {
        if (viewerWindow.Hovered is { } row && tracker.Resolve(row) is { } viewer)
            return viewer;

        return Services.TargetManager.MouseOverTarget is IPlayerCharacter hovered
               && Services.ObjectTable.LocalPlayer is { } self
               && hovered.GameObjectId != self.GameObjectId
            ? hovered
            : null;
    }

    private void Release(IPlayerCharacter player)
    {
        var action = SyncPauseRules.OnRelease(States(player, fresh: true));
        var name = player.Name.TextValue;
        foreach (var plugin in action.Pause)
            Report(Service(plugin).SetPaused(player, true), $"Paused {name} ({plugin})", $"Couldn't pause {name} in {plugin}");

        if (action.ConfirmUnpause.Count > 0)
            confirm.Open(player, action.ConfirmUnpause);
    }

    private void OnConfirm(ulong objectId, string name, IReadOnlyList<string> plugins)
    {
        if (Services.ObjectTable.SearchById(objectId) is not IPlayerCharacter player || player.Name.TextValue != name)
        {
            Notify($"{name} is no longer nearby", NotificationType.Warning);
            return;
        }

        foreach (var plugin in SyncPauseRules.StillPaused(States(player, fresh: true), plugins))
            Report(Service(plugin).SetPaused(player, false), $"Unpaused {name} ({plugin})", $"Couldn't unpause {name} in {plugin}");
    }

    private List<SyncPluginState> States(IPlayerCharacter player, bool fresh) =>
        services.Select(s => new SyncPluginState(s.Name, s.GetState(player, fresh))).ToList();

    private ISyncService Service(string name) => services.First(s => s.Name == name);

    /// <summary>Shows a toast once the plugin's call finishes, back on the framework thread.</summary>
    private void Report(Task task, string success, string failure)
    {
        task.ContinueWith(t => Services.Framework.RunOnFrameworkThread(() =>
        {
            if (disposed)
                return;
            if (t.IsCompletedSuccessfully)
            {
                Notify(success, NotificationType.Success);
                return;
            }

            Services.Log.Error(t.Exception?.GetBaseException() ?? new TaskCanceledException(), failure);
            Notify(failure, NotificationType.Warning);
        }), TaskScheduler.Default);
    }

    private static void Notify(string text, NotificationType type) =>
        Services.Notifications.AddNotification(new Notification { Title = "TargetMe", Content = text, Type = type });

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
