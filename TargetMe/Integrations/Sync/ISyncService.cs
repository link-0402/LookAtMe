using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using TargetMe.Core;

namespace TargetMe.Integrations.Sync;

public enum SyncServiceStatusKind
{
    NotLoaded,
    Connected,
    Disconnected,
    Unavailable,
}

/// <param name="Missing">For <see cref="SyncServiceStatusKind.Unavailable"/>: the member TargetMe couldn't find.</param>
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

    /// <summary>Forgets the bound plugin instance; the next call finds and binds it again.</summary>
    void Reconnect();
}
