using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using SwitchPlease.Core.Indicator;
using SwitchPlease.Win32;

namespace SwitchPlease.App;

/// <summary>
/// The small layout tag shown beside the text cursor: the tray icon's badge, lifted out of
/// the tray and put where the user is looking -- in grey rather than the tray's blue, dark
/// or light as the background asks, because next to the text it should be noticed and no
/// more. See <see cref="BadgeShades"/>.
///
/// A layered window with per-pixel alpha, so the rounded corners are smooth against whatever
/// is behind them and the whole thing can fade without being redrawn. It is never activated,
/// never takes a click -- every click goes straight through to the window underneath -- and
/// has no taskbar button. Something that appears next to the text being typed must not be
/// able to take the keyboard away from it, not even for a moment.
/// </summary>
internal sealed class CaretBadge : IDisposable
{
    /// <summary>
    /// The window's title. Never shown anywhere; it is how the probe finds the badge to
    /// check that it appeared, and where.
    /// </summary>
    public const string Title = "Switch Please layout indicator";

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint ULW_ALPHA = 0x00000002;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    private static readonly nint TopMost = -1;

    private static readonly Color Charcoal = Color.FromArgb(0x20, 0x20, 0x20);
    private static readonly Color Paper = Color.FromArgb(0xF3, 0xF3, 0xF3);

    private readonly BadgeWindow _window = new();
    private byte _alpha;
    private bool _shown;

    // Where it went last and what it looked at to choose its shade. A badge put back in the
    // same place while still up would otherwise be judging its own reflection: whether a
    // screen capture includes a layered window is not something to rely on.
    private Point _lastPosition;
    private BadgeShade _lastShade;

    /// <summary>Draws <paramref name="tag"/> beside the caret and shows it.</summary>
    /// <param name="opacity">How solid it is, from 0 to 1.</param>
    /// <returns>False when Windows would not take the picture, and nothing is shown.</returns>
    public bool Show(string tag, CaretSpot spot, double opacity)
    {
        float scale = spot.Dpi / 96f;

        using var font = new Font("Segoe UI", 11f * scale, FontStyle.Bold, GraphicsUnit.Pixel);

        var size = Measure(tag, font, scale);
        var position = IndicatorPlacement.Place(
            spot.Caret, size, spot.WorkArea, gap: (int)Math.Round(3 * scale));

        var shade = _shown && position == _lastPosition
            ? _lastShade
            : ShadeFor(new Rectangle(position, size));

        _lastPosition = position;
        _lastShade = shade;

        using var bitmap = Draw(tag, font, size, scale, shade);

        byte alpha = ToAlpha(opacity);

        if (!Paint(bitmap, position, alpha))
        {
            Hide();
            return false;
        }

        _alpha = alpha;

        // Shown, then put on top, both without activation. Topmost is set again on every
        // show rather than once: another topmost window shown since would otherwise cover it.
        ShowWindow(_window.Handle, SW_SHOWNOACTIVATE);
        SetWindowPos(
            _window.Handle, TopMost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

        _shown = true;
        return true;
    }

    /// <summary>Fades the badge without redrawing it.</summary>
    public unsafe void SetOpacity(double opacity)
    {
        byte alpha = ToAlpha(opacity);

        if (!_shown || alpha == _alpha)
        {
            return;
        }

        _alpha = alpha;

        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            SourceConstantAlpha = alpha,
            AlphaFormat = AC_SRC_ALPHA,
        };

        // With no source surface, only the blend changes: the picture stays as drawn.
        UpdateLayeredWindow(_window.Handle, 0, null, null, 0, null, 0, &blend, ULW_ALPHA);
    }

    public void Hide()
    {
        if (!_shown)
        {
            return;
        }

        ShowWindow(_window.Handle, SW_HIDE);
        _shown = false;
    }

    public void Dispose() => _window.DestroyHandle();

    private static byte ToAlpha(double opacity) => (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);

    /// <summary>
    /// About the height of a line of small text, so it reads as a note beside the line rather
    /// than a window over it.
    /// </summary>
    private static Size Measure(string tag, Font font, float scale)
    {
        Size text = TextRenderer.MeasureText(tag, font, Size.Empty, TextFormatFlags.NoPadding);

        int height = (int)Math.Round(18 * scale);

        return new Size(Math.Max(height, text.Width + (int)Math.Round(10 * scale)), height);
    }

    /// <summary>
    /// Looks at what is on screen where the badge is about to go. Dark, the usual case, when
    /// the screen cannot be read -- a secure desktop, a capture that fails.
    /// </summary>
    private static unsafe BadgeShade ShadeFor(Rectangle area)
    {
        try
        {
            using var sample = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(sample))
            {
                graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
            }

            var bits = sample.LockBits(
                new Rectangle(Point.Empty, area.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                // Thirty-two bits a pixel leaves no padding at the end of a row, so the rows
                // run on into one another and can be read as one span.
                var pixels = new ReadOnlySpan<uint>((void*)bits.Scan0, area.Width * area.Height);
                return BadgeShades.For(pixels);
            }
            finally
            {
                sample.UnlockBits(bits);
            }
        }
        catch (Exception)
        {
            return BadgeShade.Dark;
        }
    }

    /// <summary>
    /// The badge itself: the tag on a small rounded tile, sized for the monitor the caret is
    /// on. Drawn with antialiasing rather than ClearType, which needs an opaque background to
    /// work against and has none here.
    /// </summary>
    private static Bitmap Draw(string tag, Font font, Size size, float scale, BadgeShade shade)
    {
        (Color tileColour, Color inkColour) = shade == BadgeShade.Dark
            ? (Charcoal, Color.White)
            : (Paper, Charcoal);

        int width = size.Width;
        int height = size.Height;

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        float radius = 4 * scale;
        var tile = new RectangleF(0.5f, 0.5f, width - 1f, height - 1f);

        using (var path = RoundedRectangle(tile, radius))
        using (var fill = new SolidBrush(tileColour))
        {
            graphics.FillPath(fill, path);
        }

        using var ink = new SolidBrush(inkColour);
        using var centred = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        graphics.DrawString(tag, font, ink, new RectangleF(0, 0, width, height), centred);

        return bitmap;
    }

    /// <summary>Puts <paramref name="bitmap"/> on screen as the window's whole content.</summary>
    private unsafe bool Paint(Bitmap bitmap, Point position, byte alpha)
    {
        nint screen = GetDC(0);
        nint memory = CreateCompatibleDC(screen);

        // GDI+ hands this over premultiplied, which is what a layered window expects.
        nint picture = bitmap.GetHbitmap(Color.FromArgb(0));
        nint previous = SelectObject(memory, picture);

        try
        {
            var size = new SIZE { Width = bitmap.Width, Height = bitmap.Height };
            var source = new POINT();
            var destination = new POINT { X = position.X, Y = position.Y };
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                SourceConstantAlpha = alpha,
                AlphaFormat = AC_SRC_ALPHA,
            };

            return UpdateLayeredWindow(
                _window.Handle, screen, &destination, &size, memory, &source, 0, &blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(picture);
            DeleteDC(memory);
            _ = ReleaseDC(0, screen);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    /// <summary>
    /// A bare window rather than a Form: there is nothing in it to lay out or paint, and a
    /// Form would bring its own handling of opacity, activation and the taskbar, all of which
    /// would have to be talked out of what is wanted here.
    /// </summary>
    private sealed class BadgeWindow : NativeWindow
    {
        public BadgeWindow() => CreateHandle(new CreateParams
        {
            Caption = Title,
            Style = WS_POPUP,
            ExStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool UpdateLayeredWindow(
        nint hwnd,
        nint hdcDst,
        POINT* pptDst,
        SIZE* psize,
        nint hdcSrc,
        POINT* pptSrc,
        uint crKey,
        BLENDFUNCTION* pblend,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint ho);
}
