using System;
using System.Collections.Generic;

namespace TargetMe.Core;

/// <summary>Keeps each loaded value for a short time, so a per-frame caller doesn't reload it every frame.</summary>
public sealed class TimedCache<TKey, TValue> where TKey : notnull
{
    private readonly TimeSpan ttl;
    private readonly Dictionary<TKey, (TValue Value, DateTime LoadedAt)> entries = new();

    public TimedCache(TimeSpan ttl) => this.ttl = ttl;

    public int Count => entries.Count;

    public void Clear() => entries.Clear();

    public TValue Get(TKey key, DateTime now, Func<TValue> load)
    {
        if (entries.TryGetValue(key, out var entry) && now - entry.LoadedAt < ttl)
            return entry.Value;

        PruneExpired(now);
        var value = load();
        entries[key] = (value, now);
        return value;
    }

    private void PruneExpired(DateTime now)
    {
        List<TKey>? expired = null;
        foreach (var (key, entry) in entries)
        {
            if (now - entry.LoadedAt >= ttl)
                (expired ??= []).Add(key);
        }

        if (expired != null)
        {
            foreach (var key in expired)
                entries.Remove(key);
        }
    }
}
