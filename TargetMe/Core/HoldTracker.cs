namespace TargetMe.Core;

/// <summary>
/// Turns a held key into a press/release pair. A hold only counts if it started with a fresh press
/// while active, and stayed active until the release; anything else is dropped.
/// </summary>
public sealed class HoldTracker
{
    private bool wasHeld;

    public bool Holding { get; private set; }

    /// <summary>Feed one frame; returns true on the frame a valid hold is released.</summary>
    public bool Update(bool held, bool active)
    {
        var freshPress = held && !wasHeld;
        wasHeld = held;

        if (!active)
        {
            Holding = false;
            return false;
        }

        if (Holding && !held)
        {
            Holding = false;
            return true;
        }

        if (freshPress)
            Holding = true;
        return false;
    }

    public void Reset() => Holding = false;
}
