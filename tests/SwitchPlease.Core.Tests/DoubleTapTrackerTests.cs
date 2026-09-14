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

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1060, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1200, ModifierKeys.None));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1260, ModifierKeys.None));
    }

    [Fact]
    public void TapsTooFarApartDoNotFire()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift, windowMilliseconds: 500);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 2000, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 2050, ModifierKeys.None));
    }

    [Fact]
    public void HoldingTheKeyIsNotATap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift, maximumHoldMilliseconds: 400);

        // Held for a second: that is someone using Shift, not tapping it.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 2000, ModifierKeys.None));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 2100, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 2150, ModifierKeys.None));
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
            Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, time += 10, ModifierKeys.None));
            Assert.False(tracker.Feed(letter, isKeyDown: true, time += 10, ModifierKeys.None));
            Assert.False(tracker.Feed(letter, isKeyDown: false, time += 10, ModifierKeys.None));
            Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, time += 10, ModifierKeys.None));
        }
    }

    [Fact]
    public void AKeyBetweenTapsCancelsThem()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(A, isKeyDown: true, 1050, ModifierKeys.None));
        Assert.False(tracker.Feed(A, isKeyDown: false, 1060, ModifierKeys.None));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1150, ModifierKeys.None));
    }

    [Fact]
    public void EitherSideOfTheModifierCounts()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(VirtualKeys.RShift, isKeyDown: true, 1100, ModifierKeys.None));
        Assert.True(tracker.Feed(VirtualKeys.RShift, isKeyDown: false, 1150, ModifierKeys.None));
    }

    [Fact]
    public void ADifferentModifierIsIgnored()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1000, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1050, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1100, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1150, ModifierKeys.None));
    }

    [Fact]
    public void ThirdTapDoesNotFireAgainOnItsOwn()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100, ModifierKeys.None));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1150, ModifierKeys.None));

        // Both taps were consumed, so the next one starts a fresh pair.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1200, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1250, ModifierKeys.None));
    }

    [Fact]
    public void AutoRepeatWhileHeldDoesNotCountAsSeparateTaps()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000, ModifierKeys.None));

        for (uint i = 1; i <= 5; i++)
        {
            Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1000 + (i * 30), ModifierKeys.None));
        }

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1200, ModifierKeys.None));
    }

    [Fact]
    public void TickCountWrapAroundStillFires()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        // The millisecond counter wraps roughly every 49 days; unsigned subtraction has to
        // keep the gap correct across that boundary.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, uint.MaxValue - 100, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, uint.MaxValue - 50, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 20, ModifierKeys.None));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 60, ModifierKeys.None));
    }

    [Fact]
    public void ResetForgetsAHalfFinishedTap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        Tap(tracker, VirtualKeys.LShift, 1000);
        tracker.Reset();

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1150, ModifierKeys.None));
    }

    [Fact]
    public void LayoutSwitchWithCtrlAndShiftNeverReportsATap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        // Windows switches keyboard layouts by sending Ctrl down, Shift down, Shift up,
        // Ctrl up. The Shift press must not read as a clean tap just because it went down
        // and up quickly.
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1000, ModifierKeys.Control));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1010, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1050, ModifierKeys.Control));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1060, ModifierKeys.None));
    }

    [Fact]
    public void HoldingCtrlWhileTappingShiftTwiceNeverReportsATap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        // This is the reported bug: cycling through three or more keyboard layouts holds
        // Ctrl and taps Shift repeatedly, landing two Shift taps inside the double-tap
        // window. That must not fire the double-tap-Shift hotkey.
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1000, ModifierKeys.Control));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1010, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1050, ModifierKeys.Control));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1140, ModifierKeys.Control));

        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1150, ModifierKeys.None));
    }

    [Fact]
    public void HoldingAltWhileTappingShiftTwiceNeverReportsATap()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        // Same layout-switch cycling, but with Alt+Shift instead of Ctrl+Shift.
        Assert.False(tracker.Feed(VirtualKeys.LMenu, isKeyDown: true, 1000, ModifierKeys.Alt));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1010, ModifierKeys.Alt | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1050, ModifierKeys.Alt));

        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1100, ModifierKeys.Alt | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1140, ModifierKeys.Alt));

        Assert.False(tracker.Feed(VirtualKeys.LMenu, isKeyDown: false, 1150, ModifierKeys.None));
    }

    [Fact]
    public void ABareDoubleTapStillFiresAfterALayoutSwitch()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Shift);

        // Poisoned by a Ctrl+Shift layout switch first, exactly as above.
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1000, ModifierKeys.Control));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1010, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1050, ModifierKeys.Control));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1060, ModifierKeys.None));

        // A genuine bare double tap right afterwards must still be recognised: the tracker
        // must not be left poisoned by the layout switch.
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1200, ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1240, ModifierKeys.None));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1300, ModifierKeys.Shift));
        Assert.True(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1340, ModifierKeys.None));
    }

    [Fact]
    public void ACtrlTrackerIsNotTrippedByTheCtrlShiftLayoutSwitch()
    {
        var tracker = new DoubleTapTracker(VirtualKeys.Control);

        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: true, 1000, ModifierKeys.Control));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: true, 1010, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.False(tracker.Feed(VirtualKeys.LShift, isKeyDown: false, 1050, ModifierKeys.Control));
        Assert.False(tracker.Feed(VirtualKeys.LControl, isKeyDown: false, 1060, ModifierKeys.None));
    }

    private static void Tap(DoubleTapTracker tracker, ushort key, uint time)
    {
        tracker.Feed(key, isKeyDown: true, time, ModifierKeys.None);
        tracker.Feed(key, isKeyDown: false, time + 50, ModifierKeys.None);
    }
}
