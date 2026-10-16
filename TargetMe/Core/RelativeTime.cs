using System;

namespace TargetMe.Core;

public static class RelativeTime
{
    /// <summary>"now", "45s", "3m" or "2h".</summary>
    public static string Format(TimeSpan ago)
    {
        if (ago.TotalSeconds < 5)
            return "now";
        if (ago.TotalSeconds < 60)
            return $"{(int)ago.TotalSeconds}s";
        if (ago.TotalMinutes < 60)
            return $"{(int)ago.TotalMinutes}m";
        return $"{(int)ago.TotalHours}h";
    }
}
