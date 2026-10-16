using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TargetMe.Game;
using TargetMe.Integrations;
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

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        Config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Tracker = new ViewerTracker();

        var actions = new PlayerActions(Tracker);
        var moodles = new MoodlesIpc();
        var moodlesPopup = new MoodlesPopup(moodles, Tracker);
        ContextMenu = new ViewerContextMenu(Config, Tracker, actions, moodles, moodlesPopup);

        ViewerWindow = new ViewerWindow(Config, Tracker)
        {
            OnLeftClick = actions.Target,
            DrawContextMenu = ContextMenu.Draw,
        };
        windowSystem.AddWindow(ViewerWindow);
        windowSystem.AddWindow(moodlesPopup);

        Services.PluginInterface.UiBuilder.Draw += Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleViewerWindow;
        Services.Framework.Update += OnFrameworkUpdate;
        Services.ClientState.Login += OnLogin;
        Services.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the viewer list. \"/targetme settings\" opens settings, \"/targetme move\" unlocks the window to move or resize it, \"/targetme stop\" stops camera focus.",
        });

        if (Config.OpenOnLogin && Services.ClientState.IsLoggedIn)
            ViewerWindow.IsOpen = true;
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(Command);
        Services.ClientState.Login -= OnLogin;
        Services.Framework.Update -= OnFrameworkUpdate;
        Services.PluginInterface.UiBuilder.OpenMainUi -= ToggleViewerWindow;
        Services.PluginInterface.UiBuilder.Draw -= Draw;
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

    private void OnLogin()
    {
        if (Config.OpenOnLogin)
            ViewerWindow.IsOpen = true;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
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
                break;
            case "move":
                Config.WindowUnlocked = !Config.WindowUnlocked;
                Config.Save();
                Services.ChatGui.Print(Config.WindowUnlocked
                    ? "TargetMe window unlocked: drag it anywhere or resize it, then \"/targetme move\" again to lock it."
                    : "TargetMe window locked.");
                break;
            case "stop":
                break;
            default:
                ToggleViewerWindow();
                break;
        }
    }
}
