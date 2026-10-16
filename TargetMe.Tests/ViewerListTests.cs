using TargetMe.Core;

namespace TargetMe.Tests;

public class ViewerListTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 10, 0, 0);

    private static ViewerRecord Rec(string name, uint world, bool targeting, DateTime lastSeen) => new()
    {
        Name = name,
        WorldId = world,
        IsTargeting = targeting,
        LastSeen = lastSeen,
    };

    [Fact]
    public void TargetingFirstThenNewest()
    {
        var a = Rec("A", 1, true, T0);
        var b = Rec("B", 1, false, T0.AddMinutes(5));
        var c = Rec("C", 1, false, T0.AddMinutes(1));

        var visible = ViewerList.Visible([c, b, a], 15);

        Assert.Equal([a, b, c], visible);
    }

    [Fact]
    public void CapsToMostRecent()
    {
        var all = Enumerable.Range(0, 20).Select(i => Rec($"P{i}", 1, false, T0.AddSeconds(i))).ToList();

        var visible = ViewerList.Visible(all, 15);

        Assert.Equal(15, visible.Count);
        Assert.Equal(Enumerable.Range(5, 15).Reverse().Select(i => $"P{i}"), visible.Select(v => v.Name));
    }

    [Fact]
    public void CapBelowOneShowsOne()
    {
        var visible = ViewerList.Visible([Rec("A", 1, false, T0), Rec("B", 1, false, T0)], 0);

        Assert.Single(visible);
    }

    [Fact]
    public void SameNameDifferentWorldsAreDistinct()
    {
        Assert.NotEqual(ViewerRecord.MakeKey("Alice", 73), ViewerRecord.MakeKey("Alice", 74));

        var a = Rec("Alice", 73, true, T0);
        var b = Rec("Alice", 74, true, T0);
        Assert.NotEqual(a.Key, b.Key);
        Assert.Equal(2, ViewerList.Visible([a, b], 15).Count);
    }
}
