using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using LookAtMe.Core;

namespace LookAtMe.Integrations.Sync;

public enum SyncServiceStatusKind
{
    NotLoaded,
    Connected,
    Disconnected,
    Unavailable,
}

/// <param name="Missing">For <see cref="SyncServiceStatusKind.Unavailable"/>: the member Look At Me couldn't find.</param>
public readonly record struct SyncServiceStatus(SyncServiceStatusKind Kind, string? Missing = null);

/// <summary>One sync plugin (Lightless, PlayerSync) as far as pausing a player goes.</summary>
public interface ISyncService
{
    string Name { get; }
    SyncServiceStatus Status { get; }

    /// <summary>This player's state in the plugin; cached for 250 ms unless <paramref name="fresh"/>.</summary>
    SyncState GetState(IPlayerCharacter player, bool fresh = false);

    /// <summary>Pauses or unpauses the player's pair. Failures come back as a faulted task, never as a throw.</summary>
    Task SetPaused(IPlayerCharacter player, bool paused);

    /// <summary>Blocks the player the way the plugin's own block item does; this unpairs them. Failures as for <see cref="SetPaused"/>.</summary>
    Task Block(IPlayerCharacter player);

    /// <summary>Forgets the bound plugin instance; the next call finds and binds it again.</summary>
    void Reconnect();
}
