using TargetMe.Core;

namespace TargetMe.Tests;

public class CooldownsTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 10, 0, 0);
    private static readonly TimeSpan Five = TimeSpan.FromSeconds(5);

    [Fact]
    public void FiresThenBlocksUntilElapsed()
    {
        var cd = new Cooldowns();

        Assert.True(cd.TryFire("k", T0, Five));
        Assert.False(cd.TryFire("k", T0.AddSeconds(4), Five));
        Assert.True(cd.TryFire("k", T0.AddSeconds(5), Five));
    }

    [Fact]
    public void KeysAreIndependent()
    {
        var cd = new Cooldowns();

        Assert.True(cd.TryFire("a", T0, Five));
        Assert.True(cd.TryFire("b", T0, Five));
    }

    [Fact]
    public void BlockedAttemptDoesNotExtendCooldown()
    {
        var cd = new Cooldowns();

        cd.TryFire("k", T0, Five);
        cd.TryFire("k", T0.AddSeconds(4), Five);

        Assert.True(cd.TryFire("k", T0.AddSeconds(5), Five));
    }
}
