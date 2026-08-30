namespace SwitchPlease.Core.Keys;

/// <summary>
/// Recognises a modifier key being tapped twice in quick succession, the standard way to
/// get a hotkey on a keyboard that has no Pause/Break key.
///
/// A tap only counts when the key went down and up with no other key in between and was
/// not held. That is what keeps ordinary typing from triggering it: writing "АБ" presses
/// Shift twice, but a letter falls between the two presses, so neither press is a tap.
///
/// Times come from the keyboard message timestamp, which is a millisecond tick count.
/// Unsigned subtraction is used throughout so the arithmetic stays correct when that
/// counter wraps.
/// </summary>
public sealed class DoubleTapTracker
{
    /// <summary>Sentinel meaning "no tap recorded", since a tick count of zero is possible.</summary>
    private const uint NoTap = 0;

    private readonly uint _windowMilliseconds;
    private readonly uint _maximumHoldMilliseconds;

    private uint _lastTapTime = NoTap;
    private uint _downTime;
    private bool _isDown;
    private bool _interrupted;

    /// <param name="trackedKey">A modifier virtual-key: Shift, Control or Alt.</param>
    /// <param name="windowMilliseconds">Longest gap between the two taps.</param>
    /// <param name="maximumHoldMilliseconds">
    /// Longest a tap may last. Holding the key is how the modifier is normally used, so a
    /// long press must not count towards a double tap.
    /// </param>
    public DoubleTapTracker(ushort trackedKey, uint windowMilliseconds = 500, uint maximumHoldMilliseconds = 400)
    {
        TrackedKey = VirtualKeys.NormalizeModifier(trackedKey);
        _windowMilliseconds = windowMilliseconds;
        _maximumHoldMilliseconds = maximumHoldMilliseconds;
    }

    public ushort TrackedKey { get; }

    /// <summary>
    /// Feeds one key event. Returns true on the release that completes a double tap.
    /// Must be given every key event, not just the tracked one, so that a key pressed
    /// between taps can cancel them.
    /// </summary>
    public bool Feed(ushort virtualKey, bool isKeyDown, uint timeMilliseconds)
    {
        ushort normalized = VirtualKeys.NormalizeModifier(virtualKey);

        if (normalized != TrackedKey)
        {
            if (isKeyDown)
            {
                _interrupted = true;
                _lastTapTime = NoTap;
            }

            return false;
        }

        if (isKeyDown)
        {
            // Ignore auto-repeat while the key is already held.
            if (!_isDown)
            {
                _isDown = true;
                _downTime = timeMilliseconds;
                _interrupted = false;
            }

            return false;
        }

        _isDown = false;

        bool wasTap = !_interrupted && (timeMilliseconds - _downTime) <= _maximumHoldMilliseconds;
        _interrupted = false;

        if (!wasTap)
        {
            _lastTapTime = NoTap;
            return false;
        }

        if (_lastTapTime != NoTap && (timeMilliseconds - _lastTapTime) <= _windowMilliseconds)
        {
            // Consume both taps so a third press starts a fresh pair rather than firing again.
            _lastTapTime = NoTap;
            return true;
        }

        _lastTapTime = timeMilliseconds == NoTap ? 1 : timeMilliseconds;
        return false;
    }

    /// <summary>Forgets any half-finished tap, e.g. after the switcher is re-enabled.</summary>
    public void Reset()
    {
        _lastTapTime = NoTap;
        _isDown = false;
        _interrupted = false;
    }
}
