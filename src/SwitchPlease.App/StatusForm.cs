using SwitchPlease.App.Localization;

namespace SwitchPlease.App;

/// <summary>
/// The diagnostics report.
///
/// This used to be a message box, which meant the one thing anyone actually wants to do with
/// it -- paste it into a bug report -- took selecting a wall of text inside a dialog that
/// does not scroll. A read-only text box and a Copy button is the whole difference.
///
/// Unlike the other windows this one sizes itself rather than following its contents: the
/// report is as long as the machine's layout list makes it, and a window that grew to fit
/// would be taller than the screen on the machines that most need reading.
/// </summary>
public sealed class StatusForm : Form
{
    private readonly TextBox _body;

    public StatusForm(string title, string report)
    {
        var strings = Localizer.Text;

        Theme.Apply(this);

        Text = title;
        ClientSize = new Size(600, 540);
        MinimumSize = new Size(440, 340);
        SizeGripStyle = SizeGripStyle.Show;

        // The only window here that is worth keeping open beside something else while you
        // read it, so unlike the dialogs it keeps its minimise button.
        MinimizeBox = true;

        _body = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,

            // The report lines layouts and latency figures up in columns, which only reads
            // as columns in a fixed-width face.
            Font = Theme.Mono,
            Text = report.ReplaceLineEndings(),
        };

        // The text box cannot be given padding of its own, so it sits inside a panel that
        // has some. Without this the report starts hard against the window frame.
        var inset = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Theme.Pad, Theme.Gap, Theme.Gap, Theme.Gap),
            BackColor = Theme.Surface,
        };

        inset.Controls.Add(_body);

        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Theme.Pad),
        };

        frame.Controls.Add(inset);

        var close = Theme.PrimaryButton(strings.ButtonClose, DialogResult.OK);
        var copy = Theme.Button(strings.StatusCopy);

        copy.Click += (_, _) => CopyReport();

        var bar = Theme.ButtonBar(close, copy);
        bar.Dock = DockStyle.Bottom;

        Controls.Add(frame);
        Controls.Add(bar);

        AcceptButton = close;
        CancelButton = close;

        // Otherwise the whole report opens selected and highlighted in blue.
        Shown += (_, _) => _body.Select(0, 0);
    }

    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(_body.Text);
        }
        catch (Exception)
        {
            // Another process is holding the clipboard. Nothing worth interrupting the
            // user over: the text is on screen and selectable either way.
        }
    }
}
