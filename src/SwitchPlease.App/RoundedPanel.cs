using System.ComponentModel;

namespace SwitchPlease.App;

/// <summary>
/// A panel with rounded corners, painted rather than clipped.
///
/// The obvious way is to set <see cref="Control.Region"/> to a rounded path, and it does not
/// survive: a region is reset by handle recreation and by scaling, so the corners come back
/// square on exactly the machines nobody tested. Painting the shape instead is one method and
/// always right.
/// </summary>
internal sealed class RoundedPanel : Panel
{
    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
    }

    // Nothing here is ever placed by the designer, so there is nothing to serialise. Saying
    // so is what the WinForms analyser asks for; a [DefaultValue] would have to be a compile
    // time constant, which a colour taken from the theme is not.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; init; } = 8;

    /// <summary>What the rounded shape is filled with. The control's own background is the corners.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; init; } = Theme.Surface;

    /// <summary>Outline colour, or transparent for none.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Outline { get; init; } = Color.Transparent;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundedRectangle(bounds, Radius);

        using (var brush = new SolidBrush(Fill))
        {
            e.Graphics.FillPath(brush, path);
        }

        if (Outline != Color.Transparent)
        {
            using var pen = new Pen(Outline);
            e.Graphics.DrawPath(pen, path);
        }

        base.OnPaint(e);
    }
}
