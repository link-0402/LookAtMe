using System;
using System.Collections.Generic;
using System.Linq;

namespace LookAtMe.Core;

public static class ViewerList
{
    /// <summary>Rows to show: whoever is targeting now first, then most recently seen, capped.</summary>
    public static IReadOnlyList<ViewerRecord> Visible(IEnumerable<ViewerRecord> all, int cap) =>
        all.OrderByDescending(v => v.IsTargeting)
           .ThenByDescending(v => v.LastSeen)
           .Take(Math.Max(cap, 1))
           .ToList();

    /// <summary>Another row has the same name (a player from a different world), so the world must be shown.</summary>
    public static bool HasNamesake(IReadOnlyList<ViewerRecord> rows, ViewerRecord record)
    {
        foreach (var other in rows)
        {
            if (!ReferenceEquals(other, record) && string.Equals(other.Name, record.Name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
