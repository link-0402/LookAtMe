using TargetMe.Core;
using static TargetMe.Core.KeyPhase;

namespace TargetMe.Tests;

public class HoldTrackerTests
{
    [Fact]
    public void PressAndReleaseFires()
    {
        var hold = new HoldTracker();

        Assert.False(hold.Update(Chord, active: true));
        Assert.True(hold.Holding);
        Assert.True(hold.Update(Up, active: true));
        Assert.False(hold.Holding);
    }

    [Fact]
    public void InactiveMidHoldCancels()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);
        hold.Update(Chord, false);

        Assert.False(hold.Holding);
        Assert.False(hold.Update(Up, true));
    }

    [Fact]
    public void StillHeldAfterReactivationNeedsFreshPress()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);
        hold.Update(Chord, false);
        hold.Update(Chord, true);
        Assert.False(hold.Update(Up, true));

        hold.Update(Chord, true);
        Assert.True(hold.Update(Up, true));
    }

    [Fact]
    public void PressWhileInactiveDoesNotArm()
    {
        var hold = new HoldTracker();

        Assert.False(hold.Update(Chord, false));
        Assert.False(hold.Update(Chord, true));
        Assert.False(hold.Update(Up, true));
    }

    [Fact]
    public void ReleaseWhileInactiveDoesNotFire()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);

        Assert.False(hold.Update(Up, false));
    }

    [Fact]
    public void ResetDisarms()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);
        hold.Reset();

        Assert.False(hold.Update(Chord, true));
        Assert.False(hold.Update(Up, true));
    }

    // Alt+Tab, Ctrl+Shift added mid-hold: cancel, never a release.
    [Fact]
    public void ExtraModifierCancels()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);

        Assert.False(hold.Update(Spoiled, true));
        Assert.False(hold.Holding);
        Assert.False(hold.Update(Up, true));
    }

    [Fact]
    public void ChordReturningAfterSpoilNeedsFreshPress()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);
        hold.Update(Spoiled, true);

        Assert.False(hold.Update(Chord, true));
        Assert.False(hold.Update(Up, true));
    }

    // Letting go of Shift before D on Ctrl+Shift+D: still holding until D goes up.
    [Fact]
    public void BoundModifierReleasedKeepsHoldingUntilMainKeyUp()
    {
        var hold = new HoldTracker();

        hold.Update(Chord, true);

        Assert.False(hold.Update(MainOnly, true));
        Assert.True(hold.Holding);
        Assert.True(hold.Update(Up, true));
    }
}
