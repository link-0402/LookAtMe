using System;
using System.Collections.Generic;

namespace TargetMe.Core;

/// <summary>Lets something happen at most once per cooldown for each key.</summary>
public sealed class Cooldowns
{
    private readonly Dictionary<string, DateTime> lastFired = new();

    public bool TryFire(string key, DateTime now, TimeSpan cooldown)
    {
        if (lastFired.TryGetValue(key, out var last) && now - last < cooldown)
            return false;

        lastFired[key] = now;
        return true;
    }
}
