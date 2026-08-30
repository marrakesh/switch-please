using System.Runtime.InteropServices;
using SwitchPlease.App.Localization;
using SwitchPlease.Core.Config;

namespace SwitchPlease.App;

/// <summary>
/// Shown once, the first time the application runs.
///
/// A tray utility with no window has a real problem: it starts, puts a small icon among
/// twenty others, and gives the user no way of knowing what it does or how to ask it to do
/// it. This panel answers both, and it does so by demonstrating rather than explaining --
/// it types a phrase in the wrong layout, waits, and puts it right.
///
/// It rises from the corner above the notification area rather than appearing in the middle
/// of the screen, because the one thing it has to communicate is where the application now
/// lives. A dialog in the centre says "attend to me"; a panel over the tray points at the
/// icon it is talking about. Windows hides new tray icons behind the overflow arrow by
/// default, so it says that too.
///
/// Built as a notice rather than a window: no title bar, no button to dismiss it with, and a
/// bar along the bottom draining towards the moment it closes itself. Nothing here is worth
/// making someone click a button to acknowledge, and a notice that will not leave on its own
/// is a window in disguise. Moving the pointer over it stops the clock, so it cannot vanish
/// mid-sentence.
///
/// It appears exactly once, on the run where no settings file exists yet, and never again.
/// </summary>
public sealed class WelcomeForm : Form
{
    private const int TypeIntervalMilliseconds = 55;
    private const int EraseIntervalMilliseconds = 22;
    private const int PauseMilliseconds = 700;

    /// <summary>
    /// Deliberately narrow. This is a panel that appears over the notification area, not a
    /// dialog, and something the size of a dialog parked in the corner reads as a window
    /// that opened in the wrong place rather than as a notice about the icon beneath it.
    /// </summary>
    private const int ContentWidth = 292;

    /// <summary>Distance from the working area's corner, so the panel does not touch the edge.</summary>
    private const int CornerMargin = 12;

    private const int FadeIntervalMilliseconds = 15;
    private const double FadeStep = 0.09;

    /// <summary>
    /// How long the panel stays. Long enough to watch the demonstration, which takes about
    /// five seconds of it, and then read three short lines.
    /// </summary>
    private const int LifetimeMilliseconds = 18_000;

    private const int LifeTickMilliseconds = 40;

    private const int TimerBarHeight = 3;

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly System.Windows.Forms.Timer _fade = new();
    private readonly System.Windows.Forms.Timer _life = new();
    private readonly string _typed;
    private readonly string _fixed;

    // Built by local functions in the constructor, which a readonly field cannot be
    // assigned from.
    private Label _demo = null!;
    private Label _caret = null!;
    private Panel _timeLeft = null!;
    private Label _close = null!;
    private ToolTip _tip = null!;

    private Stage _stage = Stage.Typing;
    private int _position;
    private int _pauseTicksLeft;
    private Stage _afterPause = Stage.Erasing;
    private int _afterPauseInterval = EraseIntervalMilliseconds;
    private int _remaining = LifetimeMilliseconds;

    public WelcomeForm(AppSettings settings)
    {
        var strings = Localizer.Text;

        _typed = strings.WelcomeDemoTyped;
        _fixed = strings.WelcomeDemoFixed;

        Theme.Apply(this);

        Text = strings.WelcomeTitle;

        // No frame at all: the title bar is a third of the height of something this small,
        // and it says nothing the panel does not already say.
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;

        // A panel over the tray, not a window in its own right: it is not something to
        // alt-tab to or to find again later, and it stays in front until it goes.
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Opacity = 0;

        var body = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(Theme.Pad, Theme.Gap + Theme.Tight, Theme.Pad, Theme.Pad),
        };

        var heading = Theme.TitleLabel(strings.WelcomeHeading);
        heading.Font = Theme.Subtitle;

        // Leaves room for the close cross, which floats over this corner.
        heading.MaximumSize = new Size(ContentWidth - Theme.Wide, 0);
        heading.Margin = new Padding(0, 0, 0, Theme.Gap + Theme.Tight);
        body.Controls.Add(heading);

        body.Controls.Add(BuildStage());
        body.Controls.Add(HotkeyRow(settings.ConvertWordHotkey.ToString(), strings.WelcomeActionWord, Theme.Gap + Theme.Tight));
        body.Controls.Add(HotkeyRow(settings.ConvertSelectionHotkey.ToString(), strings.WelcomeActionSelection, Theme.Tight));

        var footer = Theme.HintLabel(strings.WelcomeBody, ContentWidth);
        footer.Margin = new Padding(0, Theme.Gap + Theme.Tight, 0, 0);
        body.Controls.Add(footer);

        // Bottom first: a docked control claims its edge in the order it is added, and the
        // bar has to own the bottom before the body fills what is left.
        Controls.Add(BuildTimerBar());
        Controls.Add(Theme.Page(body));
        Controls.Add(BuildCloseButton(strings.ButtonClose));

        _timer.Interval = TypeIntervalMilliseconds;
        _timer.Tick += (_, _) => Advance();

        _fade.Interval = FadeIntervalMilliseconds;
        _fade.Tick += (_, _) => FadeIn();

        _life.Interval = LifeTickMilliseconds;
        _life.Tick += (_, _) => Expire();

        Panel BuildStage()
        {
            var panel = new RoundedPanel
            {
                Width = ContentWidth,
                Height = 50,
                BackColor = SystemColors.Control,
                Fill = Theme.StageBackground,

                // Outlined as well as filled: in dark mode the stage and the window are
                // both near black, and without an edge the demonstration stops reading as a
                // screen inside the window and starts reading as a hole in it.
                Outline = Theme.Border,
                Radius = 10,
                Margin = Padding.Empty,
            };

            _demo = new Label
            {
                Font = Theme.Stage,
                ForeColor = Theme.StageText,
                BackColor = Theme.StageBackground,
                Location = new Point(Theme.Gap + Theme.Tight, 13),
                AutoSize = true,
                Text = string.Empty,
            };

            // A block that sits after the text and blinks with it, which is what makes the
            // difference between "a label being updated" and "something being typed".
            _caret = new Label
            {
                Font = Theme.Stage,
                ForeColor = Theme.StageCaret,
                BackColor = Theme.StageBackground,
                Location = new Point(Theme.Gap + Theme.Tight, 13),
                AutoSize = true,
                Text = "_",
            };

            panel.Controls.Add(_demo);
            panel.Controls.Add(_caret);

            return panel;
        }

        Panel BuildTimerBar()
        {
            var track = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = TimerBarHeight,
                BackColor = Theme.Border,
                Margin = Padding.Empty,
            };

            _timeLeft = new Panel
            {
                Dock = DockStyle.Left,
                BackColor = Theme.Accent,
            };

            track.Controls.Add(_timeLeft);
            return track;
        }

        Control BuildCloseButton(string tooltip)
        {
            _close = new Label
            {
                Text = "✕",
                Font = Theme.Small,
                ForeColor = Theme.Hint,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(22, 22),
                Cursor = Cursors.Hand,
            };

            _close.MouseEnter += (_, _) =>
            {
                _close.ForeColor = Theme.Text;
                _close.BackColor = Theme.Surface;
            };

            _close.MouseLeave += (_, _) =>
            {
                _close.ForeColor = Theme.Hint;
                _close.BackColor = Color.Transparent;
            };

            _close.Click += (_, _) => Close();

            _tip = new ToolTip();
            _tip.SetToolTip(_close, tooltip);

            return _close;
        }
    }

    /// <summary>
    /// Whether this is the first time the application has been run, judged by the settings
    /// file not existing yet. That is also the moment the file gets written, so asking later
    /// would always answer no.
    /// </summary>
    public static bool IsFirstRun() => !File.Exists(SettingsStore.FilePath);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        RoundTheCorners();
        PlaceCloseButton();
        MoveToCorner();

        _fade.Start();
        _timer.Start();
        _life.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    /// <summary>
    /// A hairline around the panel. Without a frame of its own it would otherwise have no
    /// edge at all, and against a dark desktop it would end wherever its background happened
    /// to stop.
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _fade.Stop();
            _fade.Dispose();
            _life.Stop();
            _life.Dispose();
            _tip.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Counts down to closing, and stops while the pointer is over the panel.
    ///
    /// Asking where the pointer is on each tick rather than tracking enter and leave events:
    /// every label and panel in here swallows those, so following them would mean wiring up
    /// a dozen controls and still missing the gaps between them.
    /// </summary>
    private void Expire()
    {
        if (ClientRectangle.Contains(PointToClient(Cursor.Position)))
        {
            return;
        }

        _remaining -= LifeTickMilliseconds;

        if (_remaining <= 0)
        {
            _life.Stop();
            Close();
            return;
        }

        int track = _timeLeft.Parent?.ClientSize.Width ?? Width;
        _timeLeft.Width = (int)(track * ((double)_remaining / LifetimeMilliseconds));
    }

    /// <summary>
    /// Asks the window manager for rounded corners, which Windows 11 gives a framed window
    /// for free and a frameless one only when told. Older versions do not know the
    /// attribute and say so, which is why the result is ignored.
    /// </summary>
    private void RoundTheCorners()
    {
        const int DwmWindowCornerPreference = 33;
        const int Round = 2;

        try
        {
            int preference = Round;
            _ = DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Nothing to round on a system without the desktop window manager.
        }
    }

    private void PlaceCloseButton()
    {
        _close.Location = new Point(Width - _close.Width - Theme.Gap, Theme.Gap);
        _close.BringToFront();
    }

    /// <summary>
    /// Bottom right of the working area, which is the screen minus the taskbar wherever the
    /// user keeps it. On the usual setup that puts the panel directly over the tray.
    /// </summary>
    private void MoveToCorner()
    {
        var area = (Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen)?.WorkingArea
            ?? new Rectangle(0, 0, 1024, 768);

        Location = new Point(
            Math.Max(area.Left, area.Right - Width - CornerMargin),
            Math.Max(area.Top, area.Bottom - Height - CornerMargin));
    }

    private void FadeIn()
    {
        Opacity = Math.Min(Opacity + FadeStep, 1.0);

        if (Opacity >= 1.0)
        {
            _fade.Stop();
        }
    }

    /// <summary>The binding on the left, what it does on the right.</summary>
    private static FlowLayoutPanel HotkeyRow(string hotkey, string action, int topMargin)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, topMargin, 0, 0),
        };

        var cap = new KeyCap
        {
            Font = Theme.Small,
            MinimumCapWidth = 74,
            Text = hotkey,
        };

        var caption = Theme.BodyLabel(action);
        caption.Font = Theme.Small;
        caption.Anchor = AnchorStyles.Left;
        caption.MaximumSize = new Size(ContentWidth - cap.Width - Theme.Gap, 0);
        caption.Margin = new Padding(0, (cap.Height / 2) - (Theme.Small.Height / 2), 0, 0);

        row.Controls.Add(cap);
        row.Controls.Add(caption);

        return row;
    }

    private void Advance()
    {
        switch (_stage)
        {
            case Stage.Typing:
                Type();
                break;

            case Stage.Waiting:
                CountDown();
                break;

            case Stage.Erasing:
                Erase();
                break;

            case Stage.Fixing:
                Fix();
                break;

            case Stage.Done:
                // Leave the corrected phrase on screen. Looping would turn the point of the
                // panel into wallpaper.
                _timer.Stop();
                break;
        }

        Redraw();
    }

    private void Type()
    {
        if (_position < _typed.Length)
        {
            _position++;
            return;
        }

        // Hold the wrong version on screen for a moment. Without the pause the correction
        // happens too fast to register as a correction at all.
        PauseThen(Stage.Erasing, EraseIntervalMilliseconds);
    }

    private void Erase()
    {
        if (_position > 0)
        {
            _position--;
            return;
        }

        _stage = Stage.Fixing;
        _timer.Interval = TypeIntervalMilliseconds;
    }

    private void Fix()
    {
        if (_position < _fixed.Length)
        {
            _position++;
            return;
        }

        _stage = Stage.Done;
    }

    private void PauseThen(Stage next, int nextInterval)
    {
        _pauseTicksLeft = Math.Max(PauseMilliseconds / _timer.Interval, 1);
        _afterPause = next;
        _afterPauseInterval = nextInterval;
        _stage = Stage.Waiting;
    }

    private void CountDown()
    {
        if (--_pauseTicksLeft > 0)
        {
            return;
        }

        _stage = _afterPause;
        _timer.Interval = _afterPauseInterval;
    }

    private void Redraw()
    {
        string source = _stage is Stage.Fixing or Stage.Done ? _fixed : _typed;
        int shown = Math.Clamp(_position, 0, source.Length);

        _demo.Text = source[..shown];
        _demo.ForeColor = _stage is Stage.Fixing or Stage.Done ? Theme.StageCorrected : Theme.StageText;

        _caret.Left = _demo.Left + _demo.Width;
        _caret.Visible = _stage != Stage.Done;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    private enum Stage
    {
        Typing,
        Waiting,
        Erasing,
        Fixing,
        Done,
    }
}
