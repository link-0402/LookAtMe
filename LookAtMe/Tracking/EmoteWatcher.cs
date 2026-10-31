using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace LookAtMe.Tracking;

/// <summary>
/// Hooks <see cref="EmoteController.PlayEmote"/>, which the game calls when another character
/// plays an emote, and passes emotes aimed at the local player to the tracker.
/// </summary>
public sealed unsafe class EmoteWatcher : IDisposable
{
    // byte, not bool: the native function returns a one-byte bool.
    private delegate byte PlayEmoteDelegate(EmoteController* self, uint emoteId, EmoteController.PlayEmoteOption* option);

    private readonly Configuration config;
    private readonly ViewerTracker tracker;
    private readonly Hook<PlayEmoteDelegate>? hook;

    public bool Available => hook is { IsEnabled: true };

    public EmoteWatcher(Configuration config, ViewerTracker tracker)
    {
        this.config = config;
        this.tracker = tracker;

        try
        {
            hook = Services.GameInterop.HookFromAddress<PlayEmoteDelegate>((nint)EmoteController.Addresses.PlayEmote.Value, PlayEmoteDetour);
            hook.Enable();
            Services.Log.Information("Emote hook installed");
        }
        catch (Exception ex)
        {
            hook = null;
            Services.Log.Warning(ex, "Emote detection unavailable: couldn't hook EmoteController.PlayEmote");
        }
    }

    public void Dispose() => hook?.Dispose();

    private byte PlayEmoteDetour(EmoteController* self, uint emoteId, EmoteController.PlayEmoteOption* option)
    {
        var result = hook!.Original(self, emoteId, option);

        try
        {
            if (config.ShowEmotes && option != null && self != null && self->OwnerObject != null
                && Services.ObjectTable.LocalPlayer is { } local
                && (ulong)option->TargetId == local.GameObjectId
                && Services.ObjectTable.CreateObjectReference((nint)self->OwnerObject) is IPlayerCharacter instigator
                && instigator.GameObjectId != local.GameObjectId)
            {
                tracker.AddEmote(instigator, (ushort)emoteId);
            }
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Emote detour failed");
        }

        return result;
    }
}
