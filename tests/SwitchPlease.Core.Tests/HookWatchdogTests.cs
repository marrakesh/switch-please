using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The rule that decides whether the keyboard hook has been dropped.
///
/// Both mistakes are expensive and neither announces itself. Deciding the hook is dead while
/// it is alive tears it down and rebuilds it while somebody is typing; deciding it is alive
/// while it is dead leaves the application sitting in the tray doing nothing, which is the
/// failure this whole class exists to end.
///
/// And every number involved is a tick count that wraps to zero every forty-nine days, so a
/// comparison that looks obviously right is wrong twice a year on a machine nobody reboots.
/// </summary>
public class HookWatchdogTests
{
    private const uint Grace = 5000;

    private static readonly HookWatchdog Watchdog = new(Grace);

    [Fact]
    public void AHookThatKeepsRunningIsLeftAlone()
    {
        // The ordinary case: someone is typing and every keystroke reaches the callback.
        Assert.False(Watchdog.LooksDropped(now: 100_000, lastCallback: 99_950, lastInput: 99_950));
    }

    [Fact]
    public void AnIdleMachineIsNotEvidenceOfAnything()
    {
        // Nobody has touched the keyboard for an hour, so of course the callback has not run.
        // Reinstalling here would mean rebuilding the hook all night, every night.
        Assert.False(Watchdog.LooksDropped(now: 3_700_000, lastCallback: 100_000, lastInput: 100_000));
    }

    [Fact]
    public void InputThatNeverReachedTheCallbackMeansTheHookIsGone()
    {
        // The signature of a dropped hook: Windows saw input a moment ago, and our callback
        // has not run in far longer.
        Assert.True(Watchdog.LooksDropped(now: 100_000, lastCallback: 80_000, lastInput: 99_500));
    }

    [Fact]
    public void ShortPausesInTypingAreNotSuspicious()
    {
        // Stopping to think for four seconds must not count, or the hook is rebuilt whenever
        // the user reads something.
        Assert.False(Watchdog.LooksDropped(now: 100_000, lastCallback: 96_000, lastInput: 96_000));
    }

    [Fact]
    public void TheGraceIsExclusiveAtBothEnds()
    {
        // Exactly at the boundary counts as healthy: the test is "longer than", so a hook
        // that ran precisely one grace period ago is given the benefit of the doubt.
        Assert.False(Watchdog.LooksDropped(now: 100_000, lastCallback: 100_000 - Grace, lastInput: 99_999));
        Assert.True(Watchdog.LooksDropped(now: 100_000, lastCallback: 100_000 - Grace - 1, lastInput: 99_999));

        // And input exactly a grace period old no longer counts as recent activity.
        Assert.False(Watchdog.LooksDropped(now: 100_000, lastCallback: 50_000, lastInput: 100_000 - Grace));
    }

    [Fact]
    public void TheAnswerSurvivesTheTickCountWrappingToZero()
    {
        // TickCount wraps every 49 days. Around the wrap the naive subtraction gives numbers
        // in the billions, and a watchdog built on signed arithmetic would either reinstall
        // the hook forever or never again.
        const uint JustBefore = uint.MaxValue - 1000;
        const uint JustAfter = 2000;

        // Healthy: the callback ran just before the wrap, it is just after, nothing is stale.
        Assert.False(Watchdog.LooksDropped(now: JustAfter, lastCallback: JustAfter - 500, lastInput: JustAfter - 500));

        // Dropped: input crossed the wrap, the callback did not.
        Assert.True(Watchdog.LooksDropped(now: JustAfter, lastCallback: JustBefore - 20_000, lastInput: JustAfter - 100));

        // Idle across the wrap is still idle.
        Assert.False(Watchdog.LooksDropped(now: JustAfter, lastCallback: JustBefore, lastInput: JustBefore));
    }

    [Fact]
    public void AFreshlyStartedHookIsNotImmediatelySuspected()
    {
        // At startup the callback has never run. Seeding it with the current time is what the
        // hook does; this pins down that the rule agrees, so a user typing as the application
        // launches does not trigger a reinstall on the first tick.
        Assert.False(Watchdog.LooksDropped(now: 5_000, lastCallback: 5_000, lastInput: 4_999));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(499u)]
    public void AGraceTooShortToBeSafeIsRefused(uint grace) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HookWatchdog(grace));
}
