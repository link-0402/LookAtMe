using System;
using System.Collections.Generic;
using System.Linq;

namespace TargetMe.Core;

public static class ViewerList
{
    /// <summary>Rows to show: whoever is targeting now first, then most recently seen, capped.</summary>
    public static IReadOnlyList<ViewerRecord> Visible(IEnumerable<ViewerRecord> all, int cap) =>
        all.OrderByDescending(v => v.IsTargeting)
           .ThenByDescending(v => v.LastSeen)
           .Take(Math.Max(cap, 1))
           .ToList();
}
