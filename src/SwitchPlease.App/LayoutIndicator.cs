using SwitchPlease.Core.Config;
using SwitchPlease.Core.Indicator;
using SwitchPlease.Win32;

namespace SwitchPlease.App;

/// <summary>
/// Shows the new layout beside the text cursor for a moment after it changes.
///
/// Windows tells a background process nothing when the layout of another application's
/// window changes, so this looks: ten times a second, at the foreground window's layout.
/// That is three cheap calls that never leave this process, and the timer only runs while
/// the indicator is switched on. Everything about what an observation means -- whether it
/// is a change, when the badge goes -- is decided by <see cref="LayoutIndicatorState"/>.
///
/// Runs on the interface thread, which owns the badge window. Finding the caret does not:
/// in a browser it is a question put to the browser, which answers when it gets round to
/// it, and the tray must not stop responding while it does.
/// </summary>
internal sealed class LayoutIndicator : IDisposable
{
    private const int LookingInterval = 100;

    // Faster while the badge is up, so it fades smoothly and goes the moment a key is
    // pressed rather than up to a tenth of a second later.
    private const int ShowingInterval = 30;

    // How solid the badge is at its strongest. Part see-through: it sits on top of text the
    // user may be in the middle of reading, and needs only to be noticed, not to cover it.
    private const double Strength = 0.7;

    // The range the settings window offers. A hand-edited file can hold anything; zero would
    // mean a badge that never appears, and a minute one that never leaves.
    private const int ShortestHold = 200;
    private const int LongestHold = 5000;

    // How long to keep asking for a caret that is not there yet. A window that has just got
    // focus back -- from the language flyout, say -- is in the foreground a moment before
    // its caret is, and asking once would miss exactly the switches the flyout makes.
    private const int KeepLookingMilliseconds = 500;

    private readonly SwitcherService _service;
    private readonly Action<string> _log;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly LayoutIndicatorState _state = new();
    private readonly CaretBadge _badge = new();
    private AppSettings _settings;

    // A badge waiting for its caret: which window, what it says, and until when to try.
    private Task<CaretSpot?>? _lookup;
    private nint _pendingWindow;
    private string _pendingTag = string.Empty;
    private long _giveUpAt;

    private bool _reportedFailure;

    public LayoutIndicator(SwitcherService service, AppSettings settings, Action<string> log)
    {
        _service = service;
        _settings = settings;
        _log = log;

        _timer = new System.Windows.Forms.Timer { Interval = LookingInterval };
        _timer.Tick += (_, _) => Look();

        Apply(settings);
    }

    /// <summary>Starts or stops looking, to match the settings.</summary>
    public void Apply(AppSettings settings)
    {
        _settings = settings;
        _state.HoldMilliseconds = Math.Clamp(settings.LayoutIndicatorMilliseconds, ShortestHold, LongestHold);

        bool wanted = settings.ShowLayoutAtCaret && settings.Enabled;

        if (wanted == _timer.Enabled)
        {
            return;
        }

        if (wanted)
        {
            _state.Reset();
            _timer.Interval = LookingInterval;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            Abandon();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _badge.Dispose();
    }

    /// <summary>
    /// One look. Guarded as a whole, because a timer that throws on the interface thread
    /// raises the application's crash report -- and goes on ticking behind it, raising
    /// another ten times a second.
    /// </summary>
    private void Look()
    {
        try
        {
            LookOnce();
        }
        catch (Exception ex)
        {
            Abandon();

            if (!_reportedFailure)
            {
                _reportedFailure = true;
                _log($"layout indicator: {ex.Message}");
            }
        }
    }

    private void LookOnce()
    {
        nint window = ForegroundWindowInfo.Handle;
        long now = Environment.TickCount64;

        var step = _state.Observe(
            window, KeyboardLayoutService.LayoutHandleOf(window), _service.Interactions, now);

        switch (step)
        {
            case IndicatorStep.Show:
                Begin(window, now);
                break;

            case IndicatorStep.Hide:
                Abandon();
                break;
        }

        if (_lookup is not null)
        {
            Continue(now);
        }

        if (_state.IsVisible && _lookup is null)
        {
            _badge.SetOpacity(Strength * _state.Opacity(now));
        }

        int interval = _state.IsVisible ? ShowingInterval : LookingInterval;

        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
    }

    /// <summary>
    /// Starts looking for the caret of <paramref name="window"/>, unless it is somewhere the
    /// switcher keeps out of.
    /// </summary>
    private void Begin(nint window, long now)
    {
        if (!MayShowIn(window) || _service.Layouts.GetLayoutOf(window) is not { } layout)
        {
            Abandon();
            return;
        }

        _pendingWindow = window;
        _pendingTag = layout.ShortTag;
        _giveUpAt = now + KeepLookingMilliseconds;

        // A lookup still running for an earlier change is left to finish on its own; its
        // answer is about a moment that has passed.
        _lookup = FindCaret(window);
    }

    /// <summary>Shows the badge once the caret is found, asks again if it was not, or gives up.</summary>
    private void Continue(long now)
    {
        if (_lookup is not { IsCompleted: true } lookup)
        {
            if (now >= _giveUpAt)
            {
                Abandon();
            }

            return;
        }

        if (lookup.Result is { } found)
        {
            _lookup = null;

            if (!_badge.Show(_pendingTag, found, Strength * _state.Opacity(now)))
            {
                Abandon();
            }

            return;
        }

        if (now >= _giveUpAt)
        {
            Abandon();
            return;
        }

        _lookup = FindCaret(_pendingWindow);
    }

    /// <summary>Takes the badge down, along with any search for where to put it.</summary>
    private void Abandon()
    {
        _lookup = null;
        _state.Withdraw();
        _badge.Hide();
    }

    private bool MayShowIn(nint window)
    {
        if (_settings.IsExcluded(ForegroundWindowInfo.GetProcessName(window)))
        {
            return false;
        }

        // A game has the screen. Anything drawn over it is in the way, and in a game that
        // takes keyboard focus it could be worse than that.
        return !(_settings.PauseInFullscreenApps && FullscreenGuard.ShouldStandAside(window));
    }

    /// <summary>
    /// Asks on the thread pool. A failure there is an answer of "no caret", not an exception
    /// left for nobody to observe.
    /// </summary>
    private static Task<CaretSpot?> FindCaret(nint window) => Task.Run(() =>
    {
        try
        {
            return CaretLocator.Find(window);
        }
        catch (Exception)
        {
            return null;
        }
    });
}
