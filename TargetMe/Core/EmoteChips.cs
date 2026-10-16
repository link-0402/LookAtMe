using System;
using System.Collections.Generic;
using System.Linq;

namespace TargetMe.Core;

public sealed record EmoteChip(ushort EmoteId, int Count, DateTime LastAt);

/// <summary>Recent emotes one viewer aimed at the local player, oldest first.</summary>
public sealed class EmoteChips
{
    public const int MaxChips = 5;
    private const double FadeSeconds = 2;

    private readonly List<EmoteChip> chips = new();

    public void Add(ushort emoteId, DateTime now)
    {
        var index = chips.FindIndex(c => c.EmoteId == emoteId);
        var count = 1;
        if (index >= 0)
        {
            count = chips[index].Count + 1;
            chips.RemoveAt(index);
        }

        chips.Add(new EmoteChip(emoteId, count, now));
        if (chips.Count > MaxChips)
            chips.RemoveAt(0);
    }

    /// <summary>Chips that haven't expired. A lifetime of 0 keeps them for the session.</summary>
    public IReadOnlyList<EmoteChip> Live(DateTime now, int lifetimeSeconds)
    {
        chips.RemoveAll(c => Alpha(c, now, lifetimeSeconds) <= 0f);
        return chips.ToList();
    }

    /// <summary>1 until the last two seconds of the chip's life, then fades linearly to 0.</summary>
    public static float Alpha(EmoteChip chip, DateTime now, int lifetimeSeconds)
    {
        if (lifetimeSeconds <= 0)
            return 1f;

        var remaining = lifetimeSeconds - (now - chip.LastAt).TotalSeconds;
        return (float)Math.Clamp(remaining / FadeSeconds, 0, 1);
    }
}
