using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SwitchPlease.Win32;

/// <summary>Where the caret is, and what the monitor it is on looks like.</summary>
/// <param name="Caret">The caret, in screen pixels.</param>
/// <param name="WorkArea">The work area of its monitor, in screen pixels.</param>
/// <param name="Dpi">That monitor's scale, 96 being 100%.</param>
public readonly record struct CaretSpot(Rectangle Caret, Rectangle WorkArea, int Dpi);

/// <summary>
/// Finds the text caret of the foreground window on screen.
///
/// Two places to look, because no single one covers the applications people type into:
///
/// 1. The system caret, from <c>GetGUIThreadInfo</c>. Every native edit control has one, and
///    so do Office, WinForms and WPF. Free to ask, and exact.
///
/// 2. The caret Microsoft Active Accessibility exposes, for applications that draw their own
///    and have no system caret to report. Chromium-based browsers and Firefox answer here,
///    for the sake of screen magnifiers.
///
/// An application that answers neither -- some Electron editors, most terminals -- gets no
/// answer, and the indicator is not shown there. Guessing from the mouse pointer instead
/// would put it somewhere unrelated to the text, which is worse than not showing it.
/// </summary>
public static class CaretLocator
{
    private static readonly Guid AccessibleInterfaceId = new("618736e0-3c3d-11cf-810c-00aa00389b71");

    public static CaretSpot? Find(nint foregroundWindow)
    {
        if (foregroundWindow == 0)
        {
            return null;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);

        if (threadId == 0)
        {
            return null;
        }

        var info = new NativeMethods.GUITHREADINFO
        {
            Size = Marshal.SizeOf<NativeMethods.GUITHREADINFO>(),
        };

        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info))
        {
            return null;
        }

        var caret = info.Caret != 0 ? FromSystemCaret(info.Caret, info.CaretRect) : null;

        caret ??= FromAccessibility(info.Focus != 0 ? info.Focus : foregroundWindow);

        return caret is { } found && IsInside(found, foregroundWindow) ? Describe(found) : null;
    }

    /// <summary>
    /// The system caret, which is kept in the client coordinates of the window it belongs to
    /// -- and in that window's own idea of a pixel. An application that does not handle DPI
    /// itself works in coordinates Windows scales up behind its back, so the corners are
    /// mapped to the screen the way that application sees it, then scaled to real pixels.
    /// For an application that handles DPI, as most do now, the second step changes nothing.
    /// </summary>
    private static Rectangle? FromSystemCaret(nint window, NativeMethods.RECT rect)
    {
        // A caret with no height is one that was created and never placed.
        if (rect.Bottom <= rect.Top)
        {
            return null;
        }

        var topLeft = new NativeMethods.POINT { X = rect.Left, Y = rect.Top };
        var bottomRight = new NativeMethods.POINT { X = rect.Right, Y = rect.Bottom };

        nint previous = NativeMethods.SetThreadDpiAwarenessContext(
            NativeMethods.GetWindowDpiAwarenessContext(window));

        try
        {
            if (!NativeMethods.ClientToScreen(window, ref topLeft)
                || !NativeMethods.ClientToScreen(window, ref bottomRight))
            {
                return null;
            }
        }
        finally
        {
            if (previous != 0)
            {
                NativeMethods.SetThreadDpiAwarenessContext(previous);
            }
        }

        NativeMethods.LogicalToPhysicalPointForPerMonitorDPI(window, ref topLeft);
        NativeMethods.LogicalToPhysicalPointForPerMonitorDPI(window, ref bottomRight);

        return Rectangle.FromLTRB(topLeft.X, topLeft.Y, Math.Max(bottomRight.X, topLeft.X + 1), bottomRight.Y);
    }

    /// <summary>
    /// Asks the accessibility layer for the caret, which reports it in screen coordinates.
    ///
    /// Late-bound through IDispatch, as the password-field probe is, and for the same
    /// reason: one method is needed, and a hand-written interface that got the layout wrong
    /// would crash the process rather than return a wrong answer.
    /// </summary>
    private static Rectangle? FromAccessibility(nint window)
    {
        // The request below is a message the window's own thread has to answer.
        if (NativeMethods.IsHungAppWindow(window))
        {
            return null;
        }

        object? accessible = null;

        try
        {
            if (NativeMethods.AccessibleObjectFromWindow(
                    window, NativeMethods.OBJID_CARET, in AccessibleInterfaceId, out accessible) != 0
                || accessible is null)
            {
                return null;
            }

            // accLocation(out left, out top, out width, out height, child)
            object[] arguments = [0, 0, 0, 0, NativeMethods.CHILDID_SELF];
            var byReference = new ParameterModifier(arguments.Length);

            for (int i = 0; i < 4; i++)
            {
                byReference[i] = true;
            }

            accessible.GetType().InvokeMember(
                "accLocation",
                BindingFlags.InvokeMethod,
                binder: null,
                accessible,
                arguments,
                [byReference],
                culture: null,
                namedParameters: null);

            if (arguments[0] is not int left || arguments[1] is not int top
                || arguments[2] is not int width || arguments[3] is not int height)
            {
                return null;
            }

            // An application with nothing focused reports an empty caret at the origin.
            return height > 0 ? new Rectangle(left, top, Math.Max(width, 1), height) : null;
        }
        catch (Exception)
        {
            // No accessibility support, or a broken one: the normal case, not an error.
            return null;
        }
        finally
        {
            if (accessible is not null && Marshal.IsComObject(accessible))
            {
                Marshal.ReleaseComObject(accessible);
            }
        }
    }

    /// <summary>
    /// Whether the caret is inside the window it is supposed to belong to. Some applications
    /// leave a caret behind at a stale position when the field it was in goes away, and an
    /// indicator drawn there would point at nothing.
    /// </summary>
    private static bool IsInside(Rectangle caret, nint window)
    {
        if (!NativeMethods.GetWindowRect(window, out var bounds))
        {
            return false;
        }

        var middle = new Point(caret.Left + (caret.Width / 2), caret.Top + (caret.Height / 2));

        return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom).Contains(middle);
    }

    private static CaretSpot? Describe(Rectangle caret)
    {
        var middle = new NativeMethods.POINT
        {
            X = caret.Left + (caret.Width / 2),
            Y = caret.Top + (caret.Height / 2),
        };

        nint monitor = NativeMethods.MonitorFromPoint(middle, NativeMethods.MONITOR_DEFAULTTONULL);

        if (monitor == 0)
        {
            return null;
        }

        var info = new NativeMethods.MONITORINFO { Size = Marshal.SizeOf<NativeMethods.MONITORINFO>() };

        if (!NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            return null;
        }

        int dpi = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint x, out _) == 0
            ? (int)x
            : 96;

        var work = Rectangle.FromLTRB(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);

        return new CaretSpot(caret, work, dpi);
    }
}
