using System;

namespace TargetMe.Core;

/// <summary>
/// Keeps one looked-up value, null included, until it's invalidated, so a per-frame caller doesn't
/// repeat an expensive lookup. A lookup that throws isn't kept; the next read tries again.
/// </summary>
public sealed class StaleCache<T> where T : class
{
    private T? value;
    private bool stale = true;

    public bool HasValue => !stale;

    /// <summary>Drops the value; the next read looks it up again.</summary>
    public void Invalidate()
    {
        value = null;
        stale = true;
    }

    public T? Get(Func<T?> lookup, bool rescan = false)
    {
        if (stale || rescan)
        {
            Invalidate();
            value = lookup();
            stale = false;
        }

        return value;
    }
}
