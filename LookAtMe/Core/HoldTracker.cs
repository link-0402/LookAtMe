namespace LookAtMe.Core;

/// <summary>
/// Turns a held binding into a press/release pair. A hold only starts with a fresh press of the full
/// chord while active, and only counts if the main key goes up while still active. Going inactive
/// (focus loss, chat, cutscene) or adding a modifier the binding doesn't use cancels it, and the main
/// key then has to come up before a new hold can start.
/// </summary>
public sealed class HoldTracker
{
    private bool needsUp;

    public bool Holding { get; private set; }

    /// <summary>Feed one frame; returns true on the frame a valid hold is released.</summary>
    public bool Update(KeyPhase phase, bool active)
    {
        if (phase == KeyPhase.Up)
            needsUp = false;
        else if (phase == KeyPhase.Spoiled || !active)
            needsUp = true;

        if (!active)
        {
            Holding = false;
            return false;
        }

        if (Holding)
        {
            if (phase == KeyPhase.Up)
            {
                Holding = false;
                return true;
            }

            if (phase == KeyPhase.Spoiled)
                Holding = false;
            return false;
        }

        if (phase == KeyPhase.Chord && !needsUp)
            Holding = true;
        return false;
    }

    public void Reset()
    {
        Holding = false;
        needsUp = true;
    }
}
