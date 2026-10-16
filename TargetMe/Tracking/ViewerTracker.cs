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

    private readonly Dictionary<string, ViewerRecord> records = new();
    private readonly HashSet<string> targetingNow = new();
    private readonly HashSet<string> loadedNow = new();
    private DateTime lastLoadedRefresh = DateTime.MinValue;

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

    public void Clear() => records.Clear();

    public void Update()
    {
        var local = Services.ObjectTable.LocalPlayer;
        if (local == null)
            return;

        var now = DateTime.Now;
        var refreshLoaded = now - lastLoadedRefresh >= LoadedRefreshInterval;
        if (refreshLoaded)
        {
            lastLoadedRefresh = now;
            loadedNow.Clear();
        }

        targetingNow.Clear();
        var localId = local.GameObjectId;

        foreach (var obj in Services.ObjectTable.PlayerObjects)
        {
            if (obj is not IPlayerCharacter pc || pc.GameObjectId == localId)
                continue;

            var isTargetingMe = pc.TargetObjectId == localId;
            // Building the key allocates, so only do it when this player matters this frame.
            if (!isTargetingMe && !(refreshLoaded && records.Count > 0))
                continue;

            var key = ViewerRecord.MakeKey(pc.Name.TextValue, pc.HomeWorld.RowId);
            ViewerRecord? record;
            if (isTargetingMe)
            {
                record = GetOrCreate(key, pc, now);
                targetingNow.Add(key);
                record.LastSeen = now;
                if (!record.IsTargeting)
                {
                    record.IsTargeting = true;
                    ViewerStarted?.Invoke(record);
                }
            }
            else if (!records.TryGetValue(key, out record))
            {
                continue;
            }

            loadedNow.Add(key);
            record.IsLoaded = true;
            record.GameObjectId = pc.GameObjectId;
        }

        foreach (var record in records.Values)
        {
            if (!targetingNow.Contains(record.Key))
                record.IsTargeting = false;
            if (refreshLoaded && !loadedNow.Contains(record.Key))
                record.IsLoaded = false;
        }
    }

    /// <summary>Records an emote the player aimed at the local player, adding them to the list if needed.</summary>
    public void AddEmote(IPlayerCharacter instigator, ushort emoteId)
    {
        var now = DateTime.Now;
        var record = GetOrCreate(ViewerRecord.MakeKey(instigator.Name.TextValue, instigator.HomeWorld.RowId), instigator, now);
        record.LastSeen = now;
        record.IsLoaded = true;
        record.GameObjectId = instigator.GameObjectId;
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

    private ViewerRecord GetOrCreate(string key, IPlayerCharacter pc, DateTime now)
    {
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
