namespace SwitchPlease.Win32;

/// <summary>
/// Identifies the window that currently has focus, so the switcher can stay out of
/// applications the user has excluded.
///
/// The process name is resolved through QueryFullProcessImageName rather than
/// Process.MainModule, because the latter throws for elevated processes -- exactly the
/// ones a password manager is likely to be.
/// </summary>
public static class ForegroundWindowInfo
{
    /// <summary>
    /// The last answer, as one object rather than two fields.
    ///
    /// It has to be one object because two threads ask: the worker on every keystroke, and
    /// the tray on its tick, to name the application for the menu. Two separate fields are
    /// written one after the other, and a reader arriving between the two writes gets one
    /// thread's window paired with the other thread's process name. The consequence is not
    /// theoretical -- that pair is what the exclusion list is checked against, so the answer
    /// "this is not a password manager" can be about a different window entirely.
    ///
    /// A record swapped in one assignment cannot be read half-updated.
    /// </summary>
    private static Lookup _cache = new(0, string.Empty);

    private sealed record Lookup(nint Window, string Process);

    /// <summary>
    /// Window classes that belong to the shell rather than to anything the user is working
    /// in: the taskbar, the desktop, and the wallpaper host behind it.
    /// </summary>
    private static readonly string[] ShellClasses =
        ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland"];

    public static nint Handle => NativeMethods.GetForegroundWindow();

    /// <summary>
    /// Whether the window belongs to the shell.
    ///
    /// Judged by window class rather than by process, because the taskbar and an ordinary
    /// File Explorer window are the same executable, and only one of the two is something a
    /// user would ever want to exclude.
    /// </summary>
    public static unsafe bool IsShellSurface(nint window)
    {
        if (window == 0)
        {
            return true;
        }

        const int Capacity = 64;

        char* buffer = stackalloc char[Capacity];
        int written = NativeMethods.GetClassNameW(window, buffer, Capacity);

        if (written <= 0)
        {
            return false;
        }

        var name = new ReadOnlySpan<char>(buffer, written);

        foreach (string shell in ShellClasses)
        {
            if (name.Equals(shell, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lower-cased executable name of the focused window's process, e.g. "chrome.exe".
    /// Cached per window handle: this runs on every keystroke and must stay cheap.
    /// </summary>
    public static string GetProcessName(nint window)
    {
        if (window == 0)
        {
            return string.Empty;
        }

        var cached = Volatile.Read(ref _cache);

        if (cached.Window == window)
        {
            return cached.Process;
        }

        string name = ResolveProcessName(window);

        // Only ever a hint: a stale entry costs one extra lookup, never a wrong answer,
        // because the window it belongs to is part of what is stored.
        Volatile.Write(ref _cache, new Lookup(window, name));

        return name;
    }

    private static string ResolveProcessName(nint window)
    {
        // The return value is the thread that owns the window, which is not what is wanted
        // here; the process comes back through the out parameter.
        _ = NativeMethods.GetWindowThreadProcessId(window, out uint processId);

        if (processId == 0)
        {
            return string.Empty;
        }

        nint handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            processId);

        if (handle == 0)
        {
            return string.Empty;
        }

        try
        {
            const int Capacity = 260;

            unsafe
            {
                char* buffer = stackalloc char[Capacity];
                uint size = Capacity;

                if (!NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size))
                {
                    return string.Empty;
                }

                var path = new ReadOnlySpan<char>(buffer, (int)size);
                return Path.GetFileName(path).ToString().ToLowerInvariant();
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
