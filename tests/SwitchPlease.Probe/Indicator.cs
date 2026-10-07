using System.Runtime.InteropServices;

namespace SwitchPlease.Probe;

/// <summary>
/// The layout indicator, looked at from outside: whether it is on screen, and where.
///
/// Found by its window title, which the application gives it for exactly this purpose and
/// nothing else. Rectangles are read here, in this program's own idea of a pixel, as is the
/// caret they are compared with, so the two agree whatever the display scaling.
/// </summary>
internal static class Indicator
{
    private const string Title = "Switch Please layout indicator";

    /// <summary>Where the indicator is, or null while it is not showing.</summary>
    public static Rectangle? Bounds
    {
        get
        {
            nint window = FindWindowW(null, Title);

            if (window == 0 || !IsWindowVisible(window) || !GetWindowRect(window, out var r))
            {
                return null;
            }

            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }

    /// <summary>Waits up to <paramref name="milliseconds"/> for the indicator to appear.</summary>
    public static Rectangle? WaitForIt(int milliseconds)
    {
        for (int waited = 0; waited < milliseconds; waited += 20)
        {
            if (Bounds is { } found)
            {
                return found;
            }

            Wait.For(20);
        }

        return Bounds;
    }

    /// <summary>The caret of this program's own text box, in screen coordinates.</summary>
    public static Rectangle? Caret
    {
        get
        {
            var info = new GUITHREADINFO { Size = Marshal.SizeOf<GUITHREADINFO>() };

            if (!GetGUIThreadInfo(GetCurrentThreadId(), ref info) || info.Caret == 0)
            {
                return null;
            }

            var topLeft = new POINT { X = info.CaretRect.Left, Y = info.CaretRect.Top };
            var bottomRight = new POINT { X = info.CaretRect.Right, Y = info.CaretRect.Bottom };

            if (!ClientToScreen(info.Caret, ref topLeft) || !ClientToScreen(info.Caret, ref bottomRight))
            {
                return null;
            }

            return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }
    }

    /// <summary>
    /// Whether the indicator sits against the caret: touching it, give or take its own height
    /// above or below. Anywhere further is pointing at something else.
    /// </summary>
    public static bool IsBeside(Rectangle indicator, Rectangle caret)
    {
        var reach = caret;
        reach.Inflate(indicator.Width, indicator.Height * 2);

        return reach.IntersectsWith(indicator);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int Size;
        public uint Flags;
        public nint Active;
        public nint Focus;
        public nint Capture;
        public nint MenuOwner;
        public nint MoveSize;
        public nint Caret;
        public RECT CaretRect;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref POINT point);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
