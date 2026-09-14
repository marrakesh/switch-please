namespace SwitchPlease.Core.Keys;

/// <summary>
/// Recognises a modifier key being tapped twice in quick succession, the standard way to
/// get a hotkey on a keyboard that has no Pause/Break key.
///
/// A tap only counts when the key went down and up with no other key in between and was
/// not held. That is what keeps ordinary typing from triggering it: writing "АБ" presses
/// Shift twice, but a letter falls between the two presses, so neither press is a tap.
///
/// A tap also must not count while a different modifier is already held. Windows switches
/// keyboard layouts on Ctrl+Shift or Alt+Shift by sending the second key's down and up
/// while the first stays held -- Ctrl down, Shift down, Shift up, Ctrl up -- and a user
/// with three or more layouts cycles through them by tapping Shift like that repeatedly.
/// Left unchecked, two such presses landing inside the window would read as a clean
/// double tap of Shift.
///
/// Times come from the keyboard message timestamp, which is a millisecond tick count.
/// Unsigned subtraction is used throughout so the arithmetic stays correct when that
/// counter wraps.
/// </summary>
public sealed class DoubleTapTracker
{
    /// <summary>Sentinel meaning "no tap recorded", since a tick count of zero is possible.</summary>
    private const uint NoTap = 0;

    /// <summary>Modifiers a chord can hold. CapsLock is a toggle, not one of these.</summary>
    private const ModifierKeys RelevantModifiers =
        ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Win;

    private readonly uint _windowMilliseconds;
    private readonly uint _maximumHoldMilliseconds;
    private readonly ModifierKeys _ownModifier;

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

        _ownModifier = TrackedKey switch
        {
            VirtualKeys.Shift => ModifierKeys.Shift,
            VirtualKeys.Control => ModifierKeys.Control,
            VirtualKeys.Menu => ModifierKeys.Alt,
            VirtualKeys.LWin or VirtualKeys.RWin => ModifierKeys.Win,
            _ => ModifierKeys.None,
        };
    }

    public ushort TrackedKey { get; }

    /// <summary>
    /// Feeds one key event. Returns true on the release that completes a double tap.
    /// Must be given every key event, not just the tracked one, so that a key pressed
    /// between taps can cancel them.
    /// </summary>
    public bool Feed(ushort virtualKey, bool isKeyDown, uint timeMilliseconds, ModifierKeys held)
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

                // A tap that begins with e.g. Ctrl already down -- as when Windows switches
                // keyboard layouts on Ctrl+Shift -- starts out already interrupted.
                _interrupted = IsForeignModifierHeld(held);
            }

            return false;
        }

        _isDown = false;

        bool wasTap = !_interrupted
            && (timeMilliseconds - _downTime) <= _maximumHoldMilliseconds
            && !IsForeignModifierHeld(held);
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

    /// <summary>Whether some modifier other than this tracker's own is currently held.</summary>
    private bool IsForeignModifierHeld(ModifierKeys held) =>
        (held & RelevantModifiers & ~_ownModifier) != 0;

    /// <summary>Forgets any half-finished tap, e.g. after the switcher is re-enabled.</summary>
    public void Reset()
    {
        _lastTapTime = NoTap;
        _isDown = false;
        _interrupted = false;
    }
}
