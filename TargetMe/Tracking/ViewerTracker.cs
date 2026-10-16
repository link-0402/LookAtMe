using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using TargetMe.Core;

namespace TargetMe.Tracking;

/// <summary>Keeps the session's list of players who have targeted the local player.</summary>
public sealed class ViewerTracker : IDisposable
{
    private static readonly TimeSpan LoadedRefreshInterval = TimeSpan.FromMilliseconds(100);
    // Finding someone who came back with a new object id needs their name, so that scan runs less often.
    private static readonly TimeSpan NameScanInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, ViewerRecord> records = new();
    private readonly Dictionary<ulong, ViewerRecord> byObjectId = new();
    private readonly HashSet<ViewerRecord> targetingNow = new();
    private readonly HashSet<ViewerRecord> loadedNow = new();
    private DateTime lastLoadedRefresh = DateTime.MinValue;
    private DateTime lastNameScan = DateTime.MinValue;

    /// <summary>Raised when a player goes from not targeting the local player to targeting them.</summary>
    public event Action<ViewerRecord>? ViewerStarted;

    public IReadOnlyCollection<ViewerRecord> All => records.Values;

    public ViewerTracker()
    {
        Services.Framework.Update += OnUpdate;
        Services.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        Services.Framework.Update -= OnUpdate;
        Services.ClientState.Logout -= OnLogout;
    }

    private void OnUpdate(IFramework framework) => Update();

    private void OnLogout(int type, int code) => Clear();

    public void Clear()
    {
        records.Clear();
        byObjectId.Clear();
    }

    public void Update()
    {
        var local = Services.ObjectTable.LocalPlayer;
        if (local == null)
            return;

        var now = DateTime.Now;
        var refreshLoaded = now - lastLoadedRefresh >= LoadedRefreshInterval;
        var scanByName = false;
        if (refreshLoaded)
        {
            lastLoadedRefresh = now;
            loadedNow.Clear();
            if (now - lastNameScan >= NameScanInterval && AnyAway())
            {
                lastNameScan = now;
                scanByName = true;
            }
        }

        targetingNow.Clear();
        var localId = local.GameObjectId;

        foreach (var obj in Services.ObjectTable.PlayerObjects)
        {
            if (obj is not IPlayerCharacter pc || pc.GameObjectId == localId)
                continue;

            var isTargetingMe = pc.TargetObjectId == localId;
            if (!isTargetingMe && !refreshLoaded)
                continue;

            var record = MatchById(pc);
            if (isTargetingMe)
            {
                record ??= GetOrCreate(pc, now);
                targetingNow.Add(record);
                record.LastSeen = now;
                if (!record.IsTargeting)
                {
                    record.IsTargeting = true;
                    ViewerStarted?.Invoke(record);
                }
            }
            else if (record == null)
            {
                // Building the key allocates, so only do it on the slower scan.
                if (!scanByName || !records.TryGetValue(ViewerRecord.MakeKey(pc.Name.TextValue, pc.HomeWorld.RowId), out record))
                    continue;
            }

            loadedNow.Add(record);
            Attach(record, pc);
        }

        foreach (var record in records.Values)
        {
            if (!targetingNow.Contains(record))
                record.IsTargeting = false;
            if (refreshLoaded && !loadedNow.Contains(record))
                record.IsLoaded = false;
        }
    }

    /// <summary>Records an emote the player aimed at the local player, adding them to the list if needed.</summary>
    public void AddEmote(IPlayerCharacter instigator, ushort emoteId)
    {
        var now = DateTime.Now;
        var record = MatchById(instigator) ?? GetOrCreate(instigator, now);
        record.LastSeen = now;
        Attach(record, instigator);
        record.Emotes.Add(emoteId, now);
    }

    /// <summary>The live character for a record, or null if they aren't loaded (or the id was reused).</summary>
    public IPlayerCharacter? Resolve(ViewerRecord record)
    {
        if (!record.IsLoaded)
            return null;

        return Services.ObjectTable.SearchById(record.GameObjectId) is IPlayerCharacter pc
               && pc.Name.TextValue == record.Name
               && pc.HomeWorld.RowId == record.WorldId
            ? pc
            : null;
    }

    /// <summary>The record last seen with this player's object id, if it's still the same player.</summary>
    private ViewerRecord? MatchById(IPlayerCharacter pc) =>
        byObjectId.TryGetValue(pc.GameObjectId, out var record)
        && record.WorldId == pc.HomeWorld.RowId
        && record.Name == pc.Name.TextValue
            ? record
            : null;

    private void Attach(ViewerRecord record, IPlayerCharacter pc)
    {
        if (record.GameObjectId != pc.GameObjectId
            && byObjectId.TryGetValue(record.GameObjectId, out var previous) && previous == record)
            byObjectId.Remove(record.GameObjectId);

        record.GameObjectId = pc.GameObjectId;
        record.IsLoaded = true;
        byObjectId[pc.GameObjectId] = record;
    }

    private bool AnyAway()
    {
        foreach (var record in records.Values)
        {
            if (!record.IsLoaded)
                return true;
        }

        return false;
    }

    private ViewerRecord GetOrCreate(IPlayerCharacter pc, DateTime now)
    {
        var key = ViewerRecord.MakeKey(pc.Name.TextValue, pc.HomeWorld.RowId);
        if (records.TryGetValue(key, out var existing))
            return existing;

        var record = new ViewerRecord
        {
            Name = pc.Name.TextValue,
            WorldId = pc.HomeWorld.RowId,
            WorldName = pc.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty,
            ContentId = ReadContentId(pc),
            FirstSeen = now,
            LastSeen = now,
            IsLoaded = true,
            GameObjectId = pc.GameObjectId,
        };
        records[key] = record;
        return record;
    }

    private static unsafe ulong ReadContentId(IPlayerCharacter pc) => ((Character*)pc.Address)->ContentId;
}
