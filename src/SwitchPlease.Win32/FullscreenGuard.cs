using System.Runtime.InteropServices;

namespace SwitchPlease.Win32;

/// <summary>
/// Answers "is a game or a presentation on screen right now?".
///
/// This is not a nicety. The default hotkey is a double tap of Shift, and in almost every
/// game Shift is sprint or walk — tapping it twice in half a second is not an unusual thing
/// to do, it is what running feels like. Left alone, the switcher fires repeatedly in games
/// and rewrites whatever the chat box happened to be holding.
///
/// Two questions, because neither covers the field on its own:
///
/// 1. <c>SHQueryUserNotificationState</c>, which Windows itself uses to decide whether to
///    suppress notifications. It knows about exclusive-fullscreen Direct3D and presentation
///    mode, neither of which can be worked out by measuring anything.
///
/// 2. Whether the focused window covers its whole monitor with no border. This is what
///    borderless-windowed games look like, and Windows does not report those as fullscreen
///    at all.
///
/// Both answers are cached briefly: this is asked on the path that runs per keystroke, and
/// the shell call is not free.
/// </summary>
public static class FullscreenGuard
{
    private const int CacheLifetimeMilliseconds = 500;

    /// <summary>
    /// The desktop's own window classes. Progman and WorkerW both cover the entire screen by
    /// definition, and treating the desktop as a game would switch the correction off
    /// whenever nothing else had focus.
    /// </summary>
    private static readonly string[] DesktopClasses = ["Progman", "WorkerW", "Shell_TrayWnd"];

    [ThreadStatic]
    private static nint _cachedWindow;

    [ThreadStatic]
    private static int _cachedAt;

    [ThreadStatic]
    private static bool _cachedVerdict;

    /// <summary>
    /// Whether the switcher should hold off entirely. False on any failure: a probe that
    /// cannot answer must not be able to switch the application off.
    /// </summary>
    public static bool ShouldStandAside(nint foregroundWindow)
    {
        if (foregroundWindow == 0)
        {
            return false;
        }

        int now = Environment.TickCount;

        if (foregroundWindow == _cachedWindow && (uint)(now - _cachedAt) < CacheLifetimeMilliseconds)
        {
            return _cachedVerdict;
        }

        bool verdict = IsPresentingOrPlaying() || CoversItsMonitor(foregroundWindow);

        _cachedWindow = foregroundWindow;
        _cachedAt = now;
        _cachedVerdict = verdict;

        return verdict;
    }

    private static bool IsPresentingOrPlaying()
    {
        try
        {
            if (NativeMethods.SHQueryUserNotificationState(out int state) != 0)
            {
                return false;
            }

            // QUNS_BUSY is included deliberately: it is what a full-screen application that
            // is not Direct3D reports, and it is also what Windows uses to stop notifications
            // covering someone's screen share.
            return state is NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN
                or NativeMethods.QUNS_PRESENTATION_MODE
                or NativeMethods.QUNS_BUSY;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the window fills its monitor exactly, which is what a borderless-windowed
    /// game looks like and what Windows will not tell you about any other way.
    /// </summary>
    private static bool CoversItsMonitor(nint window)
    {
        if (window == NativeMethods.GetShellWindow() || window == NativeMethods.GetDesktopWindow())
        {
            return false;
        }

        if (IsDesktopClass(window))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(window, out var bounds))
        {
            return false;
        }

        nint monitor = NativeMethods.MonitorFromWindow(window, NativeMethods.MONITOR_DEFAULTTONEAREST);

        if (monitor == 0)
        {
            return false;
        }

        var info = new NativeMethods.MONITORINFO { Size = Marshal.SizeOf<NativeMethods.MONITORINFO>() };

        if (!NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            return false;
        }

        // Exactly, not approximately. A maximised ordinary window stops short of the taskbar,
        // and someone writing in a maximised editor must not have the switcher go quiet.
        return bounds.Left <= info.Monitor.Left
            && bounds.Top <= info.Monitor.Top
            && bounds.Right >= info.Monitor.Right
            && bounds.Bottom >= info.Monitor.Bottom;
    }

    private static unsafe bool IsDesktopClass(nint window)
    {
        const int Capacity = 64;

        char* buffer = stackalloc char[Capacity];
        int written = NativeMethods.GetClassNameW(window, buffer, Capacity);

        if (written <= 0)
        {
            return false;
        }

        var name = new ReadOnlySpan<char>(buffer, written);

        foreach (string desktop in DesktopClasses)
        {
            if (name.Equals(desktop, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
