using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using TargetMe.Core;

namespace TargetMe.Game;

/// <summary>Readies a tell to a viewer in the chat input, without sending anything.</summary>
public static unsafe class TellLauncher
{
    // TellReason "Direct", what the game's own Send Tell context menu entry uses.
    private const ushort DirectReason = 0;

    public static void Open(ViewerRecord record)
    {
        // The game's Send Tell: switches the chat to Tell with this recipient and focuses the input.
        // Chat 2 hooks this same function, so its input is readied too (checked in game).
        if (SetContextTellTarget(record))
            return;

        if (!ChatSender.Send(TellCommand.For(record.Name, record.WorldName), out var error))
            Services.Log.Warning($"Couldn't ready a tell to {record.Name}: {error}");
    }

    private static bool SetContextTellTarget(ViewerRecord record)
    {
        var shell = RaptureShellModule.Instance();
        if (shell == null || record.WorldName.Length == 0)
            return false;

        var name = Utf8String.FromString(record.Name);
        var world = Utf8String.FromString(record.WorldName);
        try
        {
            return shell->SetContextTellTarget(name, world, (ushort)record.WorldId, 0, record.ContentId, DirectReason, true);
        }
        finally
        {
            name->Dtor(true);
            world->Dtor(true);
        }
    }
}
