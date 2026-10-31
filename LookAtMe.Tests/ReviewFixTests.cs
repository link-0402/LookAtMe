using LookAtMe.Core;

namespace LookAtMe.Tests;

public class ViewerRecordKeyTests
{
    [Fact]
    public void KeyIsBuiltOnce()
    {
        var record = new ViewerRecord { Name = "Alice", WorldId = 73 };

        Assert.Equal(ViewerRecord.MakeKey("Alice", 73), record.Key);
        Assert.Same(record.Key, record.Key);
    }
}

public class NamesakeTests
{
    private static ViewerRecord Rec(string name, uint world) => new() { Name = name, WorldId = world };

    [Fact]
    public void SameNameOnAnotherWorldIsANamesake()
    {
        var a = Rec("Alice", 73);
        var b = Rec("Alice", 74);
        var c = Rec("Bob", 73);
        var rows = new[] { a, b, c };

        Assert.True(ViewerList.HasNamesake(rows, a));
        Assert.True(ViewerList.HasNamesake(rows, b));
        Assert.False(ViewerList.HasNamesake(rows, c));
    }

    [Fact]
    public void ARowIsNotItsOwnNamesake()
    {
        var a = Rec("Alice", 73);

        Assert.False(ViewerList.HasNamesake([a], a));
    }
}

public class TellCommandTests
{
    [Fact]
    public void IncludesWorldWhenKnown() =>
        Assert.Equal("/tell Alice Smith@Gilgamesh", TellCommand.For("Alice Smith", "Gilgamesh"));

    [Fact]
    public void LeavesOutWorldWhenUnknown() =>
        Assert.Equal("/tell Alice Smith", TellCommand.For("Alice Smith", string.Empty));
}

public class TimedCacheTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 10, 0, 0);
    private static readonly TimeSpan Ttl = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void ReusesValueWithinTtlThenReloads()
    {
        var cache = new TimedCache<string, int>(Ttl);
        var loads = 0;
        int Load() => ++loads;

        Assert.Equal(1, cache.Get("k", T0, Load));
        Assert.Equal(1, cache.Get("k", T0.AddMilliseconds(249), Load));
        Assert.Equal(2, cache.Get("k", T0.AddMilliseconds(250), Load));
    }

    [Fact]
    public void KeysAreCachedSeparately()
    {
        var cache = new TimedCache<string, string>(Ttl);

        Assert.Equal("a", cache.Get("a", T0, () => "a"));
        Assert.Equal("b", cache.Get("b", T0, () => "b"));
        Assert.Equal("a", cache.Get("a", T0, () => "changed"));
    }

    [Fact]
    public void ExpiredEntriesArePruned()
    {
        var cache = new TimedCache<string, int>(Ttl);
        cache.Get("old", T0, () => 1);

        cache.Get("new", T0.AddSeconds(1), () => 2);

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void ClearDropsEntries()
    {
        var cache = new TimedCache<string, int>(Ttl);
        cache.Get("k", T0, () => 1);

        cache.Clear();

        Assert.Equal(2, cache.Get("k", T0, () => 2));
        Assert.Equal(1, cache.Count);
    }
}
