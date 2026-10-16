using TargetMe.Core;

namespace TargetMe.Tests;

public class EmoteChipsTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 10, 0, 0);

    [Fact]
    public void RepeatIncrementsCount()
    {
        var chips = new EmoteChips();
        chips.Add(1, T0);
        chips.Add(1, T0.AddSeconds(1));

        var live = chips.Live(T0.AddSeconds(1), 15);

        var chip = Assert.Single(live);
        Assert.Equal(2, chip.Count);
        Assert.Equal(T0.AddSeconds(1), chip.LastAt);
    }

    [Fact]
    public void RepeatMovesChipToNewest()
    {
        var chips = new EmoteChips();
        chips.Add(1, T0);
        chips.Add(2, T0.AddSeconds(1));
        chips.Add(1, T0.AddSeconds(2));

        Assert.Equal([(ushort)2, (ushort)1], chips.Live(T0.AddSeconds(2), 15).Select(c => c.EmoteId));
    }

    [Fact]
    public void KeepsFiveNewest()
    {
        var chips = new EmoteChips();
        for (ushort id = 1; id <= 6; id++)
            chips.Add(id, T0.AddSeconds(id));

        Assert.Equal([(ushort)2, 3, 4, 5, 6], chips.Live(T0.AddSeconds(6), 15).Select(c => c.EmoteId));
    }

    [Fact]
    public void ExpiresAfterLifetime()
    {
        var chips = new EmoteChips();
        chips.Add(1, T0);

        Assert.Single(chips.Live(T0.AddSeconds(14), 15));
        Assert.Empty(chips.Live(T0.AddSeconds(16), 15));
    }

    [Fact]
    public void FadesInLastTwoSeconds()
    {
        var chip = new EmoteChip(1, 1, T0);

        Assert.Equal(1f, EmoteChips.Alpha(chip, T0.AddSeconds(13), 15), 0.01f);
        Assert.Equal(0.5f, EmoteChips.Alpha(chip, T0.AddSeconds(14), 15), 0.01f);
        Assert.Equal(0f, EmoteChips.Alpha(chip, T0.AddSeconds(15), 15), 0.01f);
    }

    [Fact]
    public void ZeroLifetimeNeverExpires()
    {
        var chips = new EmoteChips();
        chips.Add(1, T0);

        var chip = Assert.Single(chips.Live(T0.AddHours(1), 0));
        Assert.Equal(1f, EmoteChips.Alpha(chip, T0.AddHours(1), 0));
    }
}
