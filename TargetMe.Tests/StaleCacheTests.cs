using TargetMe.Core;

namespace TargetMe.Tests;

public class StaleCacheTests
{
    [Fact]
    public void LooksUpOnceUntilInvalidated()
    {
        var cache = new StaleCache<object>();
        var lookups = 0;
        var first = new object();
        object? Find() { lookups++; return lookups == 1 ? first : new object(); }

        Assert.Same(first, cache.Get(Find));
        Assert.Same(first, cache.Get(Find));
        Assert.Equal(1, lookups);

        cache.Invalidate();
        Assert.NotSame(first, cache.Get(Find));
        Assert.Equal(2, lookups);
    }

    [Fact]
    public void KeepsANullResult()
    {
        var cache = new StaleCache<object>();
        var lookups = 0;
        object? Find() { lookups++; return null; }

        Assert.Null(cache.Get(Find));
        Assert.Null(cache.Get(Find));
        Assert.Equal(1, lookups);
    }

    [Fact]
    public void RescanLooksUpAgain()
    {
        var cache = new StaleCache<object>();
        var lookups = 0;
        object? Find() { lookups++; return new object(); }

        var first = cache.Get(Find);
        var second = cache.Get(Find, rescan: true);

        Assert.NotSame(first, second);
        Assert.Same(second, cache.Get(Find));
        Assert.Equal(2, lookups);
    }

    [Fact]
    public void FailedLookupIsRetried()
    {
        var cache = new StaleCache<object>();
        var lookups = 0;
        var found = new object();
        object? Find() => ++lookups == 1 ? throw new InvalidOperationException() : found;

        Assert.Throws<InvalidOperationException>(() => cache.Get(Find));
        Assert.Same(found, cache.Get(Find));
        Assert.Equal(2, lookups);
    }

    [Fact]
    public void InvalidateDropsTheValue()
    {
        var cache = new StaleCache<object>();
        cache.Get(() => new object());

        cache.Invalidate();

        Assert.False(cache.HasValue);
    }
}
