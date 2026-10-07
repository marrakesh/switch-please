namespace SwitchPlease.Core.Indicator;

/// <summary>What the layout indicator should do after one look at the foreground window.</summary>
public enum IndicatorStep
{
    None,
    Show,
    Hide,
}

/// <summary>
/// Decides when the layout indicator at the caret appears and when it goes.
///
/// It appears when the layout of the window being typed in changes -- by the user's own
/// shortcut, by a click on the language bar, or by a correction -- and goes as soon as the
/// user does anything: a key that is not a modifier, a mouse click, or a second's patience.
/// It is a reminder of what the next keystroke will type, not a status display, so it is
/// gone by the time that keystroke has been typed.
///
/// Windows announces none of this. The caller looks at the foreground window a few times a
/// second and reports what it saw; everything about what that means is decided here, where
/// it can be tested without a window in sight.
/// </summary>
/// <param name="holdMilliseconds">How long it stays fully visible.</param>
/// <param name="fadeMilliseconds">How long it then takes to fade out.</param>
public sealed class LayoutIndicatorState(int holdMilliseconds = 1000, int fadeMilliseconds = 250)
{
    /// <summary>
    /// How long focus may be away from a window for a change found on the way back to count.
    ///
    /// Switching the layout can move focus by itself: Windows shows its own language flyout,
    /// which takes the foreground while it is up, and the window the user was typing in is
    /// often first seen on the new layout only once it has focus again. Treating every focus
    /// change as the end of the story missed exactly those switches. Long enough to cover
    /// someone holding Win while they read the flyout; a window left for longer than this was
    /// left on purpose.
    /// </summary>
    public const int ReturnWithinMilliseconds = 5000;

    private nint _window;
    private nint _layout;

    // The window focus last moved away from, and the layout it had then.
    private nint _previousWindow;
    private nint _previousLayout;
    private long _leftAt;

    private bool _visible;
    private long _shownAt;
    private long _activityAtShow;

    public bool IsVisible => _visible;

    /// <summary>
    /// How long it stays fully visible. Settable, because it is a setting: a change applies
    /// from the next time it appears.
    /// </summary>
    public int HoldMilliseconds { get; set; } = holdMilliseconds;

    /// <summary>
    /// Takes in one observation of the foreground window.
    /// </summary>
    /// <param name="window">The foreground window, zero when there is none.</param>
    /// <param name="layout">Its keyboard layout, zero when it could not be read.</param>
    /// <param name="activity">
    /// A count of non-modifier keys pressed and mouse buttons clicked, from anywhere. Only
    /// whether it has moved since the indicator appeared matters.
    /// </param>
    /// <param name="now">Milliseconds on any steadily increasing clock.</param>
    public IndicatorStep Observe(nint window, nint layout, long activity, long now)
    {
        if (LayoutChanged(window, layout, now))
        {
            _visible = true;
            _shownAt = now;
            _activityAtShow = activity;
            return IndicatorStep.Show;
        }

        bool expired = now - _shownAt >= HoldMilliseconds + fadeMilliseconds;

        if (_visible && (activity != _activityAtShow || expired))
        {
            _visible = false;
            return IndicatorStep.Hide;
        }

        return IndicatorStep.None;
    }

    /// <summary>
    /// Takes back a <see cref="IndicatorStep.Show"/> the caller could not carry out: there
    /// was no caret to put the indicator by, or the window is one the switcher keeps out of.
    /// </summary>
    public void Withdraw() => _visible = false;

    /// <summary>
    /// Forgets everything seen so far, so that switching the indicator on does not report a
    /// change that happened while it was off.
    /// </summary>
    public void Reset()
    {
        _window = 0;
        _layout = 0;
        _previousWindow = 0;
        _previousLayout = 0;
        _visible = false;
    }

    /// <summary>How opaque the indicator should be now, from 1 down to 0 as it fades.</summary>
    public double Opacity(long now)
    {
        if (!_visible)
        {
            return 0;
        }

        long fading = now - _shownAt - HoldMilliseconds;

        if (fading <= 0)
        {
            return 1;
        }

        return fadeMilliseconds <= 0 ? 0 : Math.Clamp(1 - ((double)fading / fadeMilliseconds), 0, 1);
    }

    private bool LayoutChanged(nint window, nint layout, long now)
    {
        if (window == _window)
        {
            bool changed = layout != _layout && layout != 0 && _layout != 0;
            _layout = layout;
            return changed;
        }

        // Focus moved. Coming back to a window is not news in itself, so the only thing that
        // counts is the window having a different layout from the one it was left with --
        // and only when it was left a moment ago; see ReturnWithinMilliseconds.
        bool changedWhileAway = window == _previousWindow
            && window != 0
            && now - _leftAt <= ReturnWithinMilliseconds
            && layout != _previousLayout
            && layout != 0
            && _previousLayout != 0;

        _previousWindow = _window;
        _previousLayout = _layout;
        _leftAt = now;
        _window = window;
        _layout = layout;

        return changedWhileAway;
    }
}
