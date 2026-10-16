using TargetMe.Core;

namespace TargetMe.Tests;

public class MenuRulesTests
{
    [Fact]
    public void FocusCameraNeedsLoadedViewer() =>
        Assert.False(MenuRules.CanFocusCamera(loaded: false, focused: false, inCombat: false, stopOnCombat: true));

    [Fact]
    public void FocusCameraDisabledInCombatWhenItWouldStopAtOnce() =>
        Assert.False(MenuRules.CanFocusCamera(loaded: true, focused: false, inCombat: true, stopOnCombat: true));

    [Fact]
    public void FocusCameraAllowedInCombatWhenNotStoppingOnCombat() =>
        Assert.True(MenuRules.CanFocusCamera(loaded: true, focused: false, inCombat: true, stopOnCombat: false));

    [Fact]
    public void StopFocusingAlwaysAllowed() =>
        Assert.True(MenuRules.CanFocusCamera(loaded: false, focused: true, inCombat: true, stopOnCombat: true));
}
