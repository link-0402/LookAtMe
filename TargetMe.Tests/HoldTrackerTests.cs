using TargetMe.Core;

namespace TargetMe.Tests;

public class HoldTrackerTests
{
    [Fact]
    public void PressAndReleaseFires()
    {
        var hold = new HoldTracker();

        Assert.False(hold.Update(held: true, active: true));
        Assert.True(hold.Holding);
        Assert.True(hold.Update(held: false, active: true));
        Assert.False(hold.Holding);
    }

    [Fact]
    public void InactiveMidHoldCancels()
    {
        var hold = new HoldTracker();

        hold.Update(true, true);
        hold.Update(true, false);

        Assert.False(hold.Holding);
        Assert.False(hold.Update(false, true));
    }

    [Fact]
    public void StillHeldAfterReactivationNeedsFreshPress()
    {
        var hold = new HoldTracker();

        hold.Update(true, true);
        hold.Update(true, false);
        hold.Update(true, true);
        Assert.False(hold.Update(false, true));

        hold.Update(true, true);
        Assert.True(hold.Update(false, true));
    }

    [Fact]
    public void PressWhileInactiveDoesNotArm()
    {
        var hold = new HoldTracker();

        Assert.False(hold.Update(true, false));
        Assert.False(hold.Update(true, true));
        Assert.False(hold.Update(false, true));
    }

    [Fact]
    public void ReleaseWhileInactiveDoesNotFire()
    {
        var hold = new HoldTracker();

        hold.Update(true, true);

        Assert.False(hold.Update(false, false));
    }

    [Fact]
    public void ResetDisarms()
    {
        var hold = new HoldTracker();

        hold.Update(true, true);
        hold.Reset();

        Assert.False(hold.Update(false, true));
    }
}
