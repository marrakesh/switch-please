using System.Drawing;
using SwitchPlease.Core.Indicator;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// When the layout indicator appears, when it goes, and where it is put.
///
/// The window it draws and the polling that feeds it are Windows; what each observation
/// means is not, and that is the part with cases worth pinning down -- above all the one
/// where switching the layout moves focus by itself.
/// </summary>
public class LayoutIndicatorTests
{
    private const nint Editor = 0x1000;
    private const nint Browser = 0x2000;
    private const nint Flyout = 0x3000;

    private const nint English = 0x0409_0409;
    private const nint Russian = 0x0419_0419;

    [Fact]
    public void AChangeOfLayoutInTheSameWindowShowsIt()
    {
        var state = new LayoutIndicatorState();

        Assert.Equal(IndicatorStep.None, state.Observe(Editor, English, activity: 0, now: 0));
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, English, activity: 0, now: 100));
        Assert.Equal(IndicatorStep.Show, state.Observe(Editor, Russian, activity: 0, now: 200));
        Assert.True(state.IsVisible);
    }

    [Fact]
    public void TheFirstLookIsNotAChange()
    {
        var state = new LayoutIndicatorState();

        // Switching the indicator on must not report the layout the window already had.
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 0, now: 0));
        Assert.False(state.IsVisible);
    }

    [Fact]
    public void MovingToAWindowWithAnotherLayoutIsNotAChange()
    {
        var state = new LayoutIndicatorState();

        state.Observe(Editor, English, activity: 0, now: 0);

        // With a layout per window, as Windows can be set up, every window switch would
        // otherwise flash the badge.
        Assert.Equal(IndicatorStep.None, state.Observe(Browser, Russian, activity: 1, now: 100));
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, English, activity: 2, now: 200));
    }

    [Fact]
    public void AChangeFirstSeenAfterTheLanguageFlyoutHadFocusStillShows()
    {
        var state = new LayoutIndicatorState();

        state.Observe(Editor, English, activity: 0, now: 0);

        // Win+Space: Windows' own flyout takes the foreground, and the editor is next seen
        // on the new layout once it has focus back.
        Assert.Equal(IndicatorStep.None, state.Observe(Flyout, English, activity: 1, now: 100));
        Assert.Equal(IndicatorStep.None, state.Observe(Flyout, English, activity: 1, now: 900));
        Assert.Equal(IndicatorStep.Show, state.Observe(Editor, Russian, activity: 1, now: 1000));
    }

    [Fact]
    public void AWindowLeftForLongIsNotComparedWithHowItWasLeft()
    {
        var state = new LayoutIndicatorState();

        state.Observe(Editor, English, activity: 0, now: 0);
        state.Observe(Browser, English, activity: 1, now: 100);

        long later = 100 + LayoutIndicatorState.ReturnWithinMilliseconds + 1;

        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 2, now: later));
    }

    [Fact]
    public void ReturningToAWindowOnTheLayoutItWasLeftWithShowsNothing()
    {
        var state = new LayoutIndicatorState();

        state.Observe(Editor, English, activity: 0, now: 0);
        state.Observe(Flyout, Russian, activity: 0, now: 100);

        Assert.Equal(IndicatorStep.None, state.Observe(Editor, English, activity: 0, now: 200));
    }

    [Fact]
    public void AnUnreadableLayoutIsNeitherSideOfAChange()
    {
        var state = new LayoutIndicatorState();

        state.Observe(Editor, English, activity: 0, now: 0);

        Assert.Equal(IndicatorStep.None, state.Observe(Editor, 0, activity: 0, now: 100));
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 0, now: 200));
    }

    [Fact]
    public void TypingHidesIt()
    {
        var state = ShownAt(now: 1000, activity: 5);

        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 5, now: 1100));
        Assert.Equal(IndicatorStep.Hide, state.Observe(Editor, Russian, activity: 6, now: 1130));
        Assert.False(state.IsVisible);
    }

    [Fact]
    public void ItGoesByItselfOnceItHasFaded()
    {
        var state = ShownAt(now: 1000, activity: 0);

        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 0, now: 2249));
        Assert.Equal(IndicatorStep.Hide, state.Observe(Editor, Russian, activity: 0, now: 2250));
    }

    [Fact]
    public void ItStaysWhileSomethingElseBrieflyHasFocus()
    {
        var state = ShownAt(now: 1000, activity: 0);

        // The flyout appearing after the change was seen is not the user moving on.
        Assert.Equal(IndicatorStep.None, state.Observe(Flyout, Russian, activity: 0, now: 1100));
        Assert.True(state.IsVisible);
    }

    [Fact]
    public void ItFadesAfterHoldingAtFullStrength()
    {
        var state = ShownAt(now: 1000, activity: 0);

        Assert.Equal(1.0, state.Opacity(1000));
        Assert.Equal(1.0, state.Opacity(2000));
        Assert.Equal(0.5, state.Opacity(2125), precision: 3);
        Assert.Equal(0.0, state.Opacity(2250));
    }

    [Fact]
    public void HowLongItStaysIsASettingThatCanChange()
    {
        var state = new LayoutIndicatorState(holdMilliseconds: 1000, fadeMilliseconds: 250)
        {
            HoldMilliseconds = 3000,
        };

        state.Observe(Editor, English, activity: 0, now: 0);
        state.Observe(Editor, Russian, activity: 0, now: 100);

        Assert.Equal(1.0, state.Opacity(3000));
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 0, now: 3349));
        Assert.Equal(IndicatorStep.Hide, state.Observe(Editor, Russian, activity: 0, now: 3350));
    }

    [Fact]
    public void AnotherChangeWhileShownShowsAgain()
    {
        var state = ShownAt(now: 1000, activity: 0);

        Assert.Equal(IndicatorStep.Show, state.Observe(Editor, English, activity: 0, now: 1500));

        // And the clock starts again from the second change.
        Assert.Equal(1.0, state.Opacity(2400));
    }

    [Fact]
    public void AWithdrawnShowIsNotHiddenLater()
    {
        var state = ShownAt(now: 1000, activity: 0);

        // No caret to put it by: the caller never showed it, so there is nothing to hide.
        state.Withdraw();

        Assert.False(state.IsVisible);
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 1, now: 1100));
    }

    [Fact]
    public void ResettingForgetsWhatWasSeen()
    {
        var state = new LayoutIndicatorState();

        state.Observe(Editor, English, activity: 0, now: 0);
        state.Reset();

        // Switched off on English, switched on again on Russian: not a change it saw happen.
        Assert.Equal(IndicatorStep.None, state.Observe(Editor, Russian, activity: 0, now: 100));
    }

    [Fact]
    public void ItGoesUnderTheCaretCentredOnIt()
    {
        var caret = new Rectangle(500, 300, 2, 20);

        var at = IndicatorPlacement.Place(caret, new Size(30, 20), WorkArea, gap: 3);

        Assert.Equal(new Point(486, 323), at);
    }

    [Fact]
    public void WithNoRoomBelowItGoesAbove()
    {
        // A chat box at the very bottom of the screen.
        var caret = new Rectangle(500, 1020, 2, 20);

        var at = IndicatorPlacement.Place(caret, new Size(30, 20), WorkArea, gap: 3);

        Assert.Equal(new Point(486, 997), at);
    }

    [Fact]
    public void ItStaysOnTheMonitorAtTheEdges()
    {
        var left = IndicatorPlacement.Place(new Rectangle(2, 300, 2, 20), new Size(30, 20), WorkArea, gap: 3);
        var right = IndicatorPlacement.Place(new Rectangle(1918, 300, 2, 20), new Size(30, 20), WorkArea, gap: 3);

        Assert.Equal(0, left.X);
        Assert.Equal(1890, right.X);
    }

    [Fact]
    public void ItKeepsToTheWorkAreaOfASecondMonitor()
    {
        var secondMonitor = new Rectangle(-1280, 0, 1280, 984);
        var caret = new Rectangle(-1279, 960, 2, 20);

        var at = IndicatorPlacement.Place(caret, new Size(30, 20), secondMonitor, gap: 3);

        Assert.Equal(new Point(-1280, 937), at);
    }

    [Fact]
    public void OnAWhitePageItIsDark() =>
        Assert.Equal(BadgeShade.Dark, BadgeShades.For([0xFFFFFFFF, 0xFFF0F0F0, 0xFF000000]));

    [Fact]
    public void InADarkEditorItIsLight() =>
        Assert.Equal(BadgeShade.Light, BadgeShades.For([0xFF1E1E1E, 0xFF252526, 0xFFD4D4D4]));

    [Fact]
    public void GreenCountsForMoreThanBlue()
    {
        // Pure green looks bright and pure blue looks dark, whatever their numbers say.
        Assert.Equal(BadgeShade.Dark, BadgeShades.For([0xFF00FF00]));
        Assert.Equal(BadgeShade.Light, BadgeShades.For([0xFF0000FF]));
    }

    [Fact]
    public void NothingToLookAtMeansDark() =>
        Assert.Equal(BadgeShade.Dark, BadgeShades.For([]));

    /// <summary>A 1920x1080 monitor with a 40-pixel taskbar along the bottom.</summary>
    private static Rectangle WorkArea => new(0, 0, 1920, 1040);

    private static LayoutIndicatorState ShownAt(long now, long activity)
    {
        var state = new LayoutIndicatorState(holdMilliseconds: 1000, fadeMilliseconds: 250);

        state.Observe(Editor, English, activity, now - 100);
        Assert.Equal(IndicatorStep.Show, state.Observe(Editor, Russian, activity, now));

        return state;
    }
}
