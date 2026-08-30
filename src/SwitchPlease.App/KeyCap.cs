using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SwitchPlease.App;

/// <summary>
/// A hotkey drawn as a key: a rounded cap with the combination on it.
///
/// The hotkey windows used to show bindings as ordinary button text, which reads as "some
/// words on a button" rather than as "this is what you press". Drawing it as a key is the
/// whole difference, and it costs one <see cref="OnPaint"/>.
///
/// Sizes itself to its text, so a binding that spells out to `Ctrl+Alt+Shift+Pause/Break`
/// widens the cap instead of being cut in half.
/// </summary>
internal sealed class KeyCap : Control
{
    private bool _armed;
    private int _minimumCapWidth = 96;

    public KeyCap()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = Theme.Body;
        AutoSize = true;
        Margin = new Padding(0, 0, Theme.Gap + Theme.Tight, 0);
        Padding = new Padding(Theme.Gap + Theme.Tight, Theme.Tight + 2, Theme.Gap + Theme.Tight, Theme.Tight + 2);
    }

    /// <summary>Draws the cap as waiting for a keypress rather than showing one.</summary>
    [DefaultValue(false)]
    public bool Armed
    {
        get => _armed;
        set
        {
            if (_armed == value)
            {
                return;
            }

            _armed = value;
            Invalidate();
        }
    }

    /// <summary>Smallest cap width, so a row of them lines up even for a short binding.</summary>
    [DefaultValue(96)]
    public int MinimumCapWidth
    {
        get => _minimumCapWidth;
        set
        {
            _minimumCapWidth = value;
            Size = GetPreferredSize(Size.Empty);
        }
    }

    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;

            // AutoSize on a custom control only does anything if the control says what size
            // it wants; nothing measures the text for us.
            Size = GetPreferredSize(Size.Empty);
            Invalidate();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text.Length == 0 ? "M" : Text, Font);

        return new Size(
            Math.Max(text.Width + Padding.Horizontal, MinimumCapWidth),
            text.Height + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e) =>
        Theme.PaintKeyCap(e.Graphics, new Rectangle(Point.Empty, Size), Text, Font, _armed);

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Size = GetPreferredSize(Size.Empty);
    }
}
