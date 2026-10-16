using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;
using TargetMe.Core;

namespace TargetMe.Integrations;

public enum MoodleType
{
    Positive,
    Negative,
    Special,
}

public enum MoodleChainTrigger
{
    Dispel = 0,
    HitMaxStacks = 1,
    TimerExpired = 2,
}

/// <summary>Moodles' status tuple, in the order Moodles' IPC returns it.</summary>
public record struct MoodleStatus(
    int Version,
    Guid Guid,
    int IconId,
    string Title,
    string Description,
    string CustomVfxPath,
    long ExpireTicks,
    MoodleType Type,
    int Stacks,
    int StackSteps,
    uint Modifiers,
    Guid ChainedStatus,
    MoodleChainTrigger ChainTrigger,
    string Applier,
    string Dispeller,
    bool Permanent);

/// <summary>Optional: reads someone's Moodles statuses when the Moodles plugin is loaded.</summary>
public sealed class MoodlesIpc
{
    private readonly ICallGateSubscriber<IPlayerCharacter, List<(int, Guid, int, string, string, string, long, MoodleType, int, int, uint, Guid, MoodleChainTrigger, string, string, bool)>> getStatuses =
        Services.PluginInterface.GetIpcSubscriber<IPlayerCharacter, List<(int, Guid, int, string, string, string, long, MoodleType, int, int, uint, Guid, MoodleChainTrigger, string, string, bool)>>("Moodles.GetStatusManagerInfoByPlayerV2");

    // The menu and the popup ask every frame; the statuses don't change that fast.
    private readonly TimedCache<string, IReadOnlyList<MoodleStatus>> cache = new(TimeSpan.FromMilliseconds(250));

    public bool Available => getStatuses.HasFunction;

    /// <summary>The viewer's statuses, reused for up to 250 ms per record.</summary>
    public IReadOnlyList<MoodleStatus> Get(ViewerRecord record, IPlayerCharacter pc) =>
        Available ? cache.Get(record.Key, DateTime.Now, () => Query(pc)) : [];

    private IReadOnlyList<MoodleStatus> Query(IPlayerCharacter pc)
    {
        try
        {
            var result = new List<MoodleStatus>();
            foreach (var s in getStatuses.InvokeFunc(pc) ?? [])
            {
                result.Add(new MoodleStatus(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7,
                    s.Item8, s.Item9, s.Item10, s.Item11, s.Item12, s.Item13, s.Item14, s.Item15, s.Item16));
            }

            return result;
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Moodles status query failed");
            return [];
        }
    }
}
