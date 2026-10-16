using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using TargetMe.Tracking;

namespace TargetMe;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/targetme";

    private readonly WindowSystem windowSystem = new("TargetMe");

    public Configuration Config { get; }
    public ViewerTracker Tracker { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        Config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Tracker = new ViewerTracker();

        Services.PluginInterface.UiBuilder.Draw += Draw;
        Services.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the viewer list. \"/targetme settings\" opens settings, \"/targetme move\" unlocks the window to move or resize it, \"/targetme stop\" stops camera focus.",
        });
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(Command);
        Services.PluginInterface.UiBuilder.Draw -= Draw;
        windowSystem.RemoveAllWindows();
        Tracker.Dispose();
    }

    private void Draw() => windowSystem.Draw();

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "settings":
            case "config":
                break;
            case "move":
                break;
            case "stop":
                break;
            default:
                break;
        }
    }
}
