using System;
using System.Collections.Generic;

namespace TargetMe.Core;

/// <summary>Lets something happen at most once per cooldown for each key.</summary>
public sealed class Cooldowns
{
    private readonly Dictionary<string, DateTime> lastFired = new();
    private DateTime lastAny = DateTime.MinValue;

    public bool TryFire(string key, DateTime now, TimeSpan cooldown) => TryFire(key, now, cooldown, TimeSpan.Zero);

    /// <summary>Also keeps <paramref name="minGap"/> between any two firings, whatever their key.</summary>
    public bool TryFire(string key, DateTime now, TimeSpan cooldown, TimeSpan minGap)
    {
        if (now - lastAny < minGap)
            return false;
        if (lastFired.TryGetValue(key, out var last) && now - last < cooldown)
            return false;

        lastFired[key] = now;
        lastAny = now;
        return true;
    }
}
