using System.Drawing.Drawing2D;

namespace SwitchPlease.App;

/// <summary>
/// One place that decides what every window looks like.
///
/// The windows used to be built out of absolute pixel coordinates, which has two costs that
/// only show up on someone else's machine: at any scaling other than 100% the controls drift
/// out of their labels, and a translation longer than the English original runs off the edge
/// of the button it belongs to. Everything here is laid out by panels that measure their own
/// contents instead.
///
/// Colours come from <see cref="SystemColors"/> wherever one fits, so the whole interface
/// follows the Windows light or dark setting rather than being painted light and hoping.
/// The few that have no system equivalent -- the accent, the inset surface, the border --
/// are chosen per mode here.
/// </summary>
internal static class Theme
{
    /// <summary>The blue on the tray icon. The interface has exactly one accent colour.</summary>
    private static readonly Color LightAccent = Color.FromArgb(0x2D, 0x7D, 0xD2);
    private static readonly Color DarkAccent = Color.FromArgb(0x4C, 0x9A, 0xE8);

    // Spacing, in the units the rest of the interface is built from. Four steps is enough
    // for every window here, and a fixed set is what keeps the rhythm from drifting.
    public const int Tight = 4;
    public const int Gap = 8;
    public const int Pad = 16;
    public const int Wide = 24;

    public static bool IsDark => Application.IsDarkModeEnabled;

    public static Color Accent => IsDark ? DarkAccent : LightAccent;

    /// <summary>Background of an inset area: the demo panel, a grouped list.</summary>
    public static Color Surface => IsDark
        ? Color.FromArgb(0x2A, 0x2C, 0x30)
        : Color.FromArgb(0xF6, 0xF7, 0xF9);

    public static Color Border => IsDark
        ? Color.FromArgb(0x3C, 0x3F, 0x45)
        : Color.FromArgb(0xDE, 0xE1, 0xE6);

    /// <summary>Background of the strip a window's buttons sit on.</summary>
    public static Color Footer => IsDark
        ? Color.FromArgb(0x24, 0x26, 0x2A)
        : Color.FromArgb(0xFA, 0xFA, 0xFB);

    public static Color Text => SystemColors.ControlText;

    public static Color Hint => SystemColors.GrayText;

    /// <summary>The typewriter stage on the first-run window, which is dark in both modes.</summary>
    public static Color StageBackground => Color.FromArgb(0x18, 0x1A, 0x1E);

    public static Color StageText => Color.FromArgb(0xD7, 0xDA, 0xE0);

    public static Color StageCorrected => Color.FromArgb(0x7E, 0xE7, 0x87);

    public static Color StageCaret => Color.FromArgb(0xFF, 0xD5, 0x4F);

    public static Font Body { get; } = new("Segoe UI", 9.75f);

    public static Font Title { get; } = new("Segoe UI Semibold", 16f);

    public static Font Heading { get; } = new("Segoe UI Semibold", 10f);

    /// <summary>For hints under a control: the same size, so the page keeps one rhythm.</summary>
    public static Font Small { get; } = new("Segoe UI", 9f);

    public static Font Mono { get; } = new("Consolas", 10f);

    /// <summary>Between <see cref="Title"/> and <see cref="Heading"/>, for the tray panel.</summary>
    public static Font Subtitle { get; } = new("Segoe UI Semibold", 11.5f);

    public static Font Stage { get; } = new("Consolas", 13f);

    /// <summary>The shared window setup: font, background, and how it behaves when scaled.</summary>
    public static void Apply(Form form)
    {
        form.Font = Body;
        form.BackColor = SystemColors.Control;
        form.ForeColor = Text;

        // Font-based scaling is what makes a window laid out at 100% correct at 150%.
        form.AutoScaleMode = AutoScaleMode.Font;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MinimizeBox = false;
        form.ShowIcon = false;
    }

    public static Label TitleLabel(string text) => new()
    {
        Text = text,
        Font = Title,
        ForeColor = Text,
        AutoSize = true,
        Margin = Padding.Empty,
    };

    /// <summary>The heading above a group of settings.</summary>
    public static Label SectionLabel(string text) => new()
    {
        Text = text,
        Font = Heading,
        ForeColor = Accent,
        AutoSize = true,
        Margin = new Padding(0, Pad, 0, Tight),
    };

    public static Label BodyLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Text,
        Margin = Padding.Empty,
    };

    /// <summary>
    /// Explanatory text under a control. Wrapped rather than truncated, and measured, so a
    /// translation twice the length of the English still pushes the rest of the window down
    /// instead of overlapping it.
    /// </summary>
    public static Label HintLabel(string text, int width) => new()
    {
        Text = text,
        Font = Small,
        ForeColor = Hint,
        AutoSize = true,
        MaximumSize = new Size(width, 0),
        Margin = new Padding(0, Tight, 0, Gap),
    };

    /// <summary>A hairline between sections, drawn rather than a control with a border.</summary>
    public static Control Separator(int width) => new Panel
    {
        Height = 1,
        Width = width,
        BackColor = Border,
        Margin = new Padding(0, Gap, 0, Gap),
    };

    /// <summary>
    /// A window: its content, and the strip of buttons under it.
    ///
    /// Docking the content to the top instead looks like it ought to work and does not: a
    /// docked child takes its width from its parent, so the form has nothing to size itself
    /// from and collapses to the width of its own buttons. Stacked rows in a table let the
    /// content state a width and the form follow it, which is also what makes the window
    /// grow rather than clip when a translation runs long.
    /// </summary>
    public static TableLayoutPanel Page(Control body, params Control[] buttons)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        body.Margin = Padding.Empty;
        root.Controls.Add(body, 0, 0);

        if (buttons.Length > 0)
        {
            var bar = ButtonBar(buttons);
            bar.Dock = DockStyle.Fill;
            bar.Margin = Padding.Empty;

            root.Controls.Add(bar, 0, 1);
        }

        return root;
    }

    /// <summary>
    /// The strip at the bottom of a window that its buttons sit on. Right to left, so the
    /// primary action is nearest the corner the eye goes to.
    /// </summary>
    public static FlowLayoutPanel ButtonBar(params Control[] buttons)
    {
        var bar = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Footer,
            Padding = new Padding(Pad, Gap + Tight, Pad, Gap + Tight),
            WrapContents = false,
        };

        foreach (var button in buttons)
        {
            bar.Controls.Add(button);
        }

        return bar;
    }

    public static Button Button(string text, DialogResult result = DialogResult.None) => new()
    {
        Text = text,
        DialogResult = result,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(96, 30),
        Padding = new Padding(Gap, 0, Gap, 0),
        Margin = new Padding(Gap, 0, 0, 0),
        FlatStyle = FlatStyle.System,
        UseVisualStyleBackColor = true,
    };

    /// <summary>The one button on a window that is the point of opening it.</summary>
    public static Button PrimaryButton(string text, DialogResult result)
    {
        var button = Button(text, result);

        // Accented rather than merely default-focused, because on a window with three
        // buttons the focus rectangle alone is not enough to say which one is the answer.
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(Accent, 0.12f);
        button.FlatAppearance.MouseDownBackColor = Lighten(Accent, -0.12f);

        return button;
    }

    /// <summary>A key drawn as a key, for showing what a hotkey is bound to.</summary>
    public static void PaintKeyCap(Graphics graphics, Rectangle bounds, string text, Font font, bool armed)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var face = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        using var path = RoundedRectangle(face, 6);
        using var fill = new SolidBrush(armed ? Lighten(Accent, IsDark ? -0.55f : 0.82f) : Surface);
        using var edge = new Pen(armed ? Accent : Border);

        graphics.FillPath(fill, path);
        graphics.DrawPath(edge, path);

        TextRenderer.DrawText(
            graphics,
            text,
            font,
            bounds,
            armed ? Accent : Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
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

    /// <param name="amount">Positive moves towards white, negative towards black.</param>
    private static Color Lighten(Color color, float amount)
    {
        static int Mix(int channel, float amount) => amount >= 0
            ? (int)(channel + ((255 - channel) * amount))
            : (int)(channel * (1 + amount));

        return Color.FromArgb(Mix(color.R, amount), Mix(color.G, amount), Mix(color.B, amount));
    }
}
