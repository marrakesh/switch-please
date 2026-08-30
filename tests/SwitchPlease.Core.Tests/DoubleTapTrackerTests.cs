using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

public class DoubleTapTrackerTests
{
    private const ushort A = 0x41;
    private const ushort B = 0x42;

    [Fact]
    public void TwoQuickTapsFire()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1060));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1200));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1260));
    }

    [Fact]
    public void TapsTooFarApartDoNotFire()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift, windowMilliseconds: 500);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 2000));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 2050));
    }

    [Fact]
    public void HoldingTheKeyIsNotATap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift, maximumHoldMilliseconds: 400);

        // Held for a second: that is someone using Shift, not tapping it.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 2000));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 2100));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 2150));
    }

    [Fact]
    public void TypingCapitalLettersDoesNotFire()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);
        uint time = 1000;

        // "АБ": Shift+A then Shift+B. Shift is pressed twice, but each press has a letter
        // inside it, so neither is a tap.
        foreach (ushort letter in (ushort[])[A, B])
        {
            Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, time += 10));
            Assert.False(tracker.Feed(letter, isKeyDown: true, time += 10));
            Assert.False(tracker.Feed(letter, isKeyDown: false, time += 10));
            Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, time += 10));
        }
    }

    [Fact]
    public void AKeyBetweenTapsCancelsThem()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(A, isKeyDown: true, 1050));
        Assert.False(tracker.Feed(A, isKeyDown: false, 1060));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1150));
    }

    [Fact]
    public void EitherSideOfTheModifierCounts()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(VirtualKeys.RShift, isKeyDown: true, 1100));
        Assert.True(tracker.Feed(VirtualKeys.RShift, isKeyDown: false, 1150));
    }

    [Fact]
    public void ADifferentModifierIsIgnored()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1000));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1050));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1100));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1150));
    }

    [Fact]
    public void ThirdTapDoesNotFireAgainOnItsOwn()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1150));

        // Both taps were consumed, so the next one starts a fresh pair.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1200));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1250));
    }

    [Fact]
    public void AutoRepeatWhileHeldDoesNotCountAsSeparateTaps()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000));

        for (uint i = 1; i <= 5; i++)
        {
            Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000 + (i * 30)));
        }

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1200));
    }

    [Fact]
    public void TickCountWrapAroundStillFires()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        // The millisecond counter wraps roughly every 49 days; unsigned subtraction has to
        // keep the gap correct across that boundary.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, uint.MaxValue - 100));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, uint.MaxValue - 50));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 20));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 60));
    }

    [Fact]
    public void ResetForgetsAHalfFinishedTap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);
        tracker.Reset();

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1150));
    }

    private static void Tap(DoubleTapTracker tracker, ushort key, uint time)
    {
        tracker.Feed(key, isKeyDown: true, time);
        tracker.Feed(key, isKeyDown: false, time + 50);
    }
}
