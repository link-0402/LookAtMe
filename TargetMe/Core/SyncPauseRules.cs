using System.Collections.Generic;
using System.Linq;

namespace TargetMe.Core;

public enum SyncState
{
    /// <summary>The plugin isn't loaded, or TargetMe can't use it on this version.</summary>
    Unavailable,
    /// <summary>The plugin is loaded but not connected to its server.</summary>
    Offline,
    NotPaired,
    Synced,
    Paused,
    /// <summary>Paused, but this plugin's unpause call couldn't be resolved.</summary>
    PausedCantUnpause,
}

public readonly record struct SyncPluginState(string Plugin, SyncState State);

public enum MarkerKind
{
    Pause,
    Unpause,
    NotPaired,
}

public sealed record ReleaseAction(IReadOnlyList<string> Pause, IReadOnlyList<string> ConfirmUnpause);

/// <summary>What the pause marker shows and what releasing the hotkey does, from each sync plugin's state.</summary>
public static class SyncPauseRules
{
    private const string Separator = "   ";

    public static MarkerKind Marker(IReadOnlyList<SyncPluginState> states)
    {
        if (states.Any(s => s.State == SyncState.Synced))
            return MarkerKind.Pause;
        if (states.Any(s => s.State == SyncState.Paused))
            return MarkerKind.Unpause;
        return MarkerKind.NotPaired;
    }

    public static string Label(IReadOnlyList<SyncPluginState> states)
    {
        var parts = new List<string>();
        foreach (var (plugin, state) in states)
        {
            var part = state switch
            {
                SyncState.Synced => $"Pause · {plugin}",
                SyncState.Paused => $"Unpause… · {plugin}",
                SyncState.Offline => $"{plugin} offline",
                SyncState.PausedCantUnpause => $"Paused (can't unpause here) · {plugin}",
                _ => null,
            };
            if (part != null)
                parts.Add(part);
        }

        return parts.Count == 0 ? "Not paired" : string.Join(Separator, parts);
    }

    public static ReleaseAction OnRelease(IReadOnlyList<SyncPluginState> states) => new(
        states.Where(s => s.State == SyncState.Synced).Select(s => s.Plugin).ToList(),
        states.Where(s => s.State == SyncState.Paused).Select(s => s.Plugin).ToList());

    /// <summary>The requested plugins whose fresh state is still Paused, in requested order.</summary>
    public static IReadOnlyList<string> StillPaused(IReadOnlyList<SyncPluginState> fresh, IReadOnlyList<string> requested) =>
        requested.Where(plugin => fresh.Any(s => s.Plugin == plugin && s.State == SyncState.Paused)).ToList();

    /// <summary>A notice for each requested plugin that <see cref="StillPaused"/> leaves out, saying why.</summary>
    public static IReadOnlyList<string> SkippedUnpauses(IReadOnlyList<SyncPluginState> fresh, IReadOnlyList<string> requested, string name) =>
        requested
            .Select(plugin => fresh.FirstOrDefault(s => s.Plugin == plugin, new(plugin, SyncState.Unavailable)))
            .Where(s => s.State != SyncState.Paused)
            .Select(s => s.State switch
            {
                SyncState.Synced => $"{name} is no longer paused in {s.Plugin}",
                SyncState.NotPaired => $"{name} is no longer paired in {s.Plugin}",
                SyncState.Offline => $"{s.Plugin} is offline, so {name} wasn't unpaused",
                SyncState.PausedCantUnpause => $"{s.Plugin} can't unpause {name} here",
                _ => $"{s.Plugin} isn't available, so {name} wasn't unpaused",
            })
            .ToList();
}
