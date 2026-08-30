namespace SwitchPlease.Core.Keys;

/// <summary>
/// Decides whether Windows has quietly dropped the keyboard hook.
///
/// It does that. A low-level hook whose callback overruns <c>LowLevelHooksTimeout</c> is
/// removed without a word: no error, no notification, and the handle the application is
/// holding stays exactly as valid-looking as before. The switcher then sits in the tray
/// looking perfectly healthy and does nothing at all, which is the worst failure it has,
/// because there is no way for the user to tell it apart from "it decided not to correct
/// that one".
///
/// There is no call that asks "is my hook still installed?". What there is: the time our
/// callback last ran, and the time Windows last saw any input at all. If the machine has had
/// input recently and our callback has not run in a long time, the hook is gone.
///
/// Kept apart from the hook itself because it is arithmetic on three tick counts, all of
/// which wrap every 49 days, and getting the comparison wrong in either direction is bad in
/// its own way: too eager and the hook is torn down and rebuilt while the user is typing,
/// too lax and the switcher stays dead.
/// </summary>
public sealed class HookWatchdog
{
    /// <summary>
    /// How long the callback must have been silent before silence means anything.
    ///
    /// Generously long. Reinstalling costs a moment of deafness, and the cost of being wrong
    /// is paid every time the user pauses, whereas the cost of waiting is paid once per
    /// dropped hook.
    /// </summary>
    public const uint DefaultGraceMilliseconds = 5000;

    private readonly uint _grace;

    public HookWatchdog(uint graceMilliseconds = DefaultGraceMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(graceMilliseconds, 500u);
        _grace = graceMilliseconds;
    }

    /// <summary>
    /// Whether the hook looks dropped.
    ///
    /// All three arguments are millisecond tick counts from the same clock, which is what
    /// <c>GetLastInputInfo</c> reports and what <c>Environment.TickCount</c> shares. Every
    /// comparison is done on unsigned differences so that it stays correct across the wrap.
    /// </summary>
    /// <param name="now">The current tick count.</param>
    /// <param name="lastCallback">When the hook callback last ran.</param>
    /// <param name="lastInput">When Windows last saw input from the user, from any source.</param>
    public bool LooksDropped(uint now, uint lastCallback, uint lastInput)
    {
        uint sinceCallback = now - lastCallback;
        uint sinceInput = now - lastInput;

        // Two conditions, and both matter. The callback has been quiet for longer than a
        // pause in typing explains -- and the machine has not been idle, because a callback
        // that has not run while nobody has touched the keyboard is not evidence of
        // anything.
        return sinceCallback > _grace && sinceInput < _grace;
    }
}
