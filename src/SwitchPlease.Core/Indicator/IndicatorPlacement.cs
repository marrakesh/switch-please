using System.Drawing;

namespace SwitchPlease.Core.Indicator;

/// <summary>
/// Where the layout indicator goes, given where the caret is.
///
/// Under the caret and centred on it, so it points at the place the next character will
/// appear without covering the line being written. Above it instead when there is no room
/// below -- a chat box at the bottom of the screen is the common case -- and always kept
/// inside the work area, so it never ends up under the taskbar or half off a monitor.
/// </summary>
public static class IndicatorPlacement
{
    /// <param name="caret">The caret, in screen pixels.</param>
    /// <param name="badge">The indicator's size, in the same pixels.</param>
    /// <param name="workArea">The work area of the monitor the caret is on.</param>
    /// <param name="gap">Space to leave between the caret and the indicator.</param>
    /// <returns>The indicator's top-left corner.</returns>
    public static Point Place(Rectangle caret, Size badge, Rectangle workArea, int gap)
    {
        int x = caret.Left + (caret.Width / 2) - (badge.Width / 2);
        int y = caret.Bottom + gap;

        if (y + badge.Height > workArea.Bottom)
        {
            y = caret.Top - gap - badge.Height;
        }

        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - badge.Width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - badge.Height));

        return new Point(x, y);
    }
}
