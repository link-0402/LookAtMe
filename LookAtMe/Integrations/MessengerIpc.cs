using System;
using Dalamud.Plugin.Ipc;
using LookAtMe.Core;

namespace LookAtMe.Integrations;

/// <summary>Optional: opens someone's conversation window when XIV Instant Messenger is loaded.</summary>
public sealed class MessengerIpc
{
    // Takes "Name@World" and whether to focus the window's input.
    private readonly ICallGateSubscriber<string, bool, object> openMessenger =
        Services.PluginInterface.GetIpcSubscriber<string, bool, object>("Messenger.OpenMessenger");

    public bool Available => openMessenger.HasAction;

    /// <summary>Messenger needs the world to find the conversation.</summary>
    public static bool CanOpen(ViewerRecord record) => record.WorldName.Length > 0;

    public void Open(ViewerRecord record)
    {
        try
        {
            openMessenger.InvokeAction($"{record.Name}@{record.WorldName}", true);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, $"Couldn't open a Messenger window for {record.Name}");
        }
    }
}
