using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace SwitchPlease.App;

/// <summary>
/// Draws the tray icon at runtime rather than shipping a .ico, so the icon can show the
/// current state: which layout is active and whether the switcher is listening.
/// </summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);

    /// <summary>
    /// How many frames the correction animation has. Each one hides one more character of
    /// the tag behind a carriage that sweeps across it, which at tray-icon size reads as a
    /// typewriter head going over the letters.
    /// </summary>
    public const int AnimationFrames = 6;

    public static Icon Create(string tag, bool active) => Create(tag, active, frame: -1);

    /// <param name="frame">
    /// Position of the typewriter carriage, 0 to <see cref="AnimationFrames"/>, or -1 for
    /// the ordinary resting icon.
    /// </param>
    public static Icon Create(string tag, bool active, int frame)
    {
        const int Size = 32;

        using var bitmap = new Bitmap(Size, Size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            Color background = active ? Color.FromArgb(0x2D, 0x7D, 0xD2) : Color.FromArgb(0x60, 0x60, 0x60);

            using (var brush = new SolidBrush(background))
            using (var path = RoundedRectangle(new Rectangle(1, 1, Size - 2, Size - 2), 7))
            {
                graphics.FillPath(brush, path);
            }

            string text = tag.Length >= 2 ? tag[..2].ToUpperInvariant() : "??";

            using var font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };

            graphics.DrawString(text, font, textBrush, new RectangleF(0, 0, Size, Size), format);

            if (frame >= 0)
            {
                DrawCarriage(graphics, Size, frame);
            }
        }

        return ToIcon(bitmap);
    }

    /// <summary>
    /// A bright bar sweeping left to right with the struck part of the tag underlined behind
    /// it. Thirty-two pixels is not enough for a picture of a typewriter, but it is enough
    /// for the motion, and the motion is what reads.
    /// </summary>
    private static void DrawCarriage(Graphics graphics, int size, int frame)
    {
        float progress = Math.Clamp(frame / (float)AnimationFrames, 0f, 1f);
        float x = 3 + (progress * (size - 9));

        using var struck = new SolidBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
        graphics.FillRectangle(struck, 3, size - 8, x - 3, 2);

        using var carriage = new SolidBrush(Color.FromArgb(0xFF, 0xFF, 0xD5, 0x4F));
        graphics.FillRectangle(carriage, x, size - 11, 4, 8);
    }

    private static Icon ToIcon(Bitmap bitmap)
    {
        nint handle = bitmap.GetHicon();

        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            // Icon.FromHandle does not take ownership; without this every refresh leaks
            // a GDI handle, and the icon eventually stops rendering.
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
