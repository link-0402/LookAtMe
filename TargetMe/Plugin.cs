using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TargetMe.Alerts;
using TargetMe.Game;
using TargetMe.Integrations;
using TargetMe.Integrations.Sync;
using TargetMe.Tracking;
using TargetMe.UI;

namespace TargetMe;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/targetme";

    private readonly WindowSystem windowSystem = new("TargetMe");
    private readonly HighlightOverlay overlay = new();
    private bool hiddenForDuty;

    public Configuration Config { get; }
    public ViewerTracker Tracker { get; }
    public ViewerWindow ViewerWindow { get; }
    public ViewerContextMenu ContextMenu { get; }
    public EmoteWatcher EmoteWatcher { get; }
    public CameraFocus Camera { get; }
    public SoundAlert SoundAlert { get; }
    public SettingsWindow SettingsWindow { get; }
    public IReadOnlyList<ISyncService> SyncServices { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        Config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Tracker = new ViewerTracker();
        SyncServices = [new LightlessSyncService(), new PlayerSyncService()];

        var actions = new PlayerActions(Tracker);
        var moodles = new MoodlesIpc();
        var moodlesPopup = new MoodlesPopup(moodles, Tracker);
        Camera = new CameraFocus(Config, Tracker);
        ContextMenu = new ViewerContextMenu(Config, Tracker, actions, moodles, moodlesPopup, Camera);

        EmoteWatcher = new EmoteWatcher(Config, Tracker);
        var chipView = new EmoteChipView(Config);

        ViewerWindow = new ViewerWindow(Config, Tracker)
        {
            OnLeftClick = actions.Target,
            DrawContextMenu = ContextMenu.Draw,
            DrawEmoteChips = chipView.Draw,
        };
        windowSystem.AddWindow(ViewerWindow);
        windowSystem.AddWindow(moodlesPopup);

        SoundAlert = new SoundAlert(Config, () => ViewerWindow.IsOpen);
        Tracker.ViewerStarted += SoundAlert.OnViewerStarted;

        SettingsWindow = new SettingsWindow(Config, SoundAlert, EmoteWatcher, Camera, moodles);
        windowSystem.AddWindow(SettingsWindow);
        ViewerWindow.OpenSettings = SettingsWindow.Toggle;

        Services.PluginInterface.UiBuilder.Draw += Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleViewerWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi += SettingsWindow.Toggle;
        Services.Framework.Update += OnFrameworkUpdate;
        Services.ClientState.Login += OnLogin;
        Services.PluginInterface.ActivePluginsChanged += OnPluginsChanged;
        Services.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the viewer list. \"/targetme settings\" opens settings, \"/targetme move\" unlocks the window to move or resize it, \"/targetme stop\" stops camera focus.",
        });

        if (Config.OpenOnLogin && Services.ClientState.IsLoggedIn)
            ViewerWindow.IsOpen = true;
    }

    public void Dispose()
    {
        // The camera goes back to the player before its hooks are removed.
        Camera.Stop();
        Camera.Dispose();
        Services.CommandManager.RemoveHandler(Command);
        Services.ClientState.Login -= OnLogin;
        Services.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        Services.Framework.Update -= OnFrameworkUpdate;
        Services.PluginInterface.UiBuilder.OpenMainUi -= ToggleViewerWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= SettingsWindow.Toggle;
        Services.PluginInterface.UiBuilder.Draw -= Draw;
        EmoteWatcher.Dispose();
        Tracker.ViewerStarted -= SoundAlert.OnViewerStarted;
        windowSystem.RemoveAllWindows();
        Tracker.Dispose();
    }

    private void Draw()
    {
        windowSystem.Draw();

        if (ViewerWindow.Hovered is { } hovered
            && Tracker.Resolve(hovered) is { } target
            && Services.ObjectTable.LocalPlayer is { } self)
            overlay.Draw(target, self, Config);
    }

    private void ToggleViewerWindow() => ViewerWindow.Toggle();

    // A sync plugin was loaded, unloaded or updated: rebind on next use.
    private void OnPluginsChanged(IActivePluginsChangedEventArgs args)
    {
        foreach (var service in SyncServices)
            service.Reconnect();
    }

    private void OnLogin()
    {
        if (Config.OpenOnLogin)
            ViewerWindow.IsOpen = true;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        Camera.Update();

        var inDuty = Services.Condition[ConditionFlag.BoundByDuty];
        if (Config.HideInDuty && inDuty && ViewerWindow.IsOpen)
        {
            ViewerWindow.IsOpen = false;
            hiddenForDuty = true;
        }
        else if (hiddenForDuty && (!inDuty || !Config.HideInDuty))
        {
            ViewerWindow.IsOpen = true;
            hiddenForDuty = false;
        }
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "settings":
            case "config":
                SettingsWindow.Toggle();
                break;
            case "move":
                Config.WindowUnlocked = !Config.WindowUnlocked;
                Config.Save();
                Services.ChatGui.Print(Config.WindowUnlocked
                    ? "TargetMe window unlocked: drag it anywhere or resize it, then \"/targetme move\" again to lock it."
                    : "TargetMe window locked.");
                break;
            case "stop":
                Camera.Stop();
                break;
            default:
                ToggleViewerWindow();
                break;
        }
    }
}
