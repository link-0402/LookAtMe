using TargetMe.Core;

namespace TargetMe.Tests;

public class SyncPauseRulesTests
{
    private static SyncPluginState L(SyncState s) => new("Lightless", s);
    private static SyncPluginState P(SyncState s) => new("PlayerSync", s);

    [Fact]
    public void MarkerPauseWhenAnySynced()
    {
        Assert.Equal(MarkerKind.Pause, SyncPauseRules.Marker([L(SyncState.Synced), P(SyncState.Paused)]));
    }

    [Fact]
    public void MarkerUnpauseWhenOnlyPaused()
    {
        Assert.Equal(MarkerKind.Unpause, SyncPauseRules.Marker([L(SyncState.NotPaired), P(SyncState.Paused)]));
    }

    [Fact]
    public void MarkerNotPairedOtherwise()
    {
        Assert.Equal(MarkerKind.NotPaired, SyncPauseRules.Marker([L(SyncState.Offline), P(SyncState.PausedCantUnpause)]));
        Assert.Equal(MarkerKind.NotPaired, SyncPauseRules.Marker([L(SyncState.Unavailable), P(SyncState.Unavailable)]));
    }

    [Fact]
    public void LabelListsEachKnownPlugin()
    {
        Assert.Equal("Pause · Lightless   Unpause… · PlayerSync",
            SyncPauseRules.Label([L(SyncState.Synced), P(SyncState.Paused)]));
    }

    [Fact]
    public void LabelNotPairedWhenNothingKnown()
    {
        Assert.Equal("Not paired", SyncPauseRules.Label([L(SyncState.NotPaired), P(SyncState.Unavailable)]));
        Assert.Equal("Not paired", SyncPauseRules.Label([]));
    }

    [Fact]
    public void LabelOffline()
    {
        Assert.Equal("Lightless offline", SyncPauseRules.Label([L(SyncState.Offline), P(SyncState.NotPaired)]));
    }

    [Fact]
    public void LabelCantUnpause()
    {
        Assert.Equal("Paused (can't unpause here) · PlayerSync", SyncPauseRules.Label([P(SyncState.PausedCantUnpause)]));
    }

    public static TheoryData<SyncState, SyncState> AllPairs()
    {
        var data = new TheoryData<SyncState, SyncState>();
        foreach (var l in Enum.GetValues<SyncState>())
            foreach (var p in Enum.GetValues<SyncState>())
                data.Add(l, p);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void ReleaseActsPerPlugin(SyncState l, SyncState p)
    {
        var action = SyncPauseRules.OnRelease([L(l), P(p)]);

        var expectedPause = new List<string>();
        var expectedConfirm = new List<string>();
        if (l == SyncState.Synced) expectedPause.Add("Lightless");
        if (p == SyncState.Synced) expectedPause.Add("PlayerSync");
        if (l == SyncState.Paused) expectedConfirm.Add("Lightless");
        if (p == SyncState.Paused) expectedConfirm.Add("PlayerSync");

        Assert.Equal(expectedPause, action.Pause);
        Assert.Equal(expectedConfirm, action.ConfirmUnpause);
    }

    [Fact]
    public void StillPausedSkipsChanged()
    {
        Assert.Equal(["PlayerSync"],
            SyncPauseRules.StillPaused([L(SyncState.Synced), P(SyncState.Paused)], ["Lightless", "PlayerSync"]));
        Assert.Equal(["Lightless"],
            SyncPauseRules.StillPaused([L(SyncState.Paused)], ["PlayerSync", "Lightless"]));
    }

    [Fact]
    public void SkippedUnpausesSayWhy()
    {
        Assert.Equal(
            [
                "Alice is no longer paused in Lightless",
                "PlayerSync is offline, so Alice wasn't unpaused",
            ],
            SyncPauseRules.SkippedUnpauses([L(SyncState.Synced), P(SyncState.Offline)], ["Lightless", "PlayerSync"], "Alice"));
        Assert.Equal(
            [
                "Alice is no longer paired in Lightless",
                "PlayerSync can't unpause Alice here",
            ],
            SyncPauseRules.SkippedUnpauses([L(SyncState.NotPaired), P(SyncState.PausedCantUnpause)], ["Lightless", "PlayerSync"], "Alice"));
    }

    [Fact]
    public void SkippedUnpausesCoverMissingPlugins()
    {
        Assert.Equal(["PlayerSync isn't available, so Alice wasn't unpaused"],
            SyncPauseRules.SkippedUnpauses([L(SyncState.Paused)], ["Lightless", "PlayerSync"], "Alice"));
        Assert.Equal(["Lightless isn't available, so Alice wasn't unpaused"],
            SyncPauseRules.SkippedUnpauses([L(SyncState.Unavailable)], ["Lightless"], "Alice"));
    }

    [Fact]
    public void NothingSkippedWhenStillPaused()
    {
        Assert.Empty(SyncPauseRules.SkippedUnpauses([L(SyncState.Paused), P(SyncState.Paused)], ["Lightless", "PlayerSync"], "Alice"));
    }
}
