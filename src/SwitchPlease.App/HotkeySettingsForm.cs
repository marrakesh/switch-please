using SwitchPlease.App.Localization;
using SwitchPlease.Core.Config;
using SwitchPlease.Core.Keys;

namespace SwitchPlease.App;

/// <summary>
/// Lets the user reassign the hotkeys by pressing the combination they want.
///
/// Key events are taken through an application message filter rather than the usual
/// KeyDown handlers, because the buttons on this form would otherwise swallow Space,
/// Enter, Tab and the arrow keys before they could be captured -- and because recognising
/// a double tap needs key releases, which ProcessCmdKey never sees.
/// </summary>
public sealed class HotkeySettingsForm : Form, IMessageFilter
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int ContentWidth = 400;

    private readonly Row[] _rows;
    private readonly DoubleTapTracker[] _trackers;

    private Row? _armed;

    public HotkeySettingsForm(Hotkey word, Hotkey selection, Hotkey undo)
    {
        var strings = Localizer.Text;

        // Shift and Control, and deliberately not Alt.
        //
        // Alt on its own is how Windows opens a window's menu bar, and it does that on the
        // release -- which is exactly the event a double tap is recognised on, and one that
        // cannot be swallowed without leaving the application believing Alt is still held.
        // Bound here it does not merely look untidy: the first test that tried it left a
        // window sitting in menu mode with its message loop going nowhere. Alt is still
        // perfectly good as part of a chord, where the key pressed with it cancels the menu.
        _trackers =
        [
            new DoubleTapTracker(VirtualKeys.Shift),
            new DoubleTapTracker(VirtualKeys.Control),
        ];

        // One row per bindable command. Adding another means adding an entry here and
        // nothing else: the capture, the clearing and the duplicate check all walk this.
        _rows =
        [
            new Row(strings.HotkeysWord, word, Hotkey.ConvertWord),
            new Row(strings.HotkeysSelection, selection, Hotkey.ConvertSelection),
            new Row(strings.HotkeysUndo, undo, Hotkey.None),
        ];

        Theme.Apply(this);

        Text = strings.HotkeysTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var body = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(Theme.Wide, Theme.Wide, Theme.Wide, Theme.Pad),
        };

        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        foreach (var row in _rows)
        {
            var label = Theme.BodyLabel(row.Caption);
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, Theme.Gap, Theme.Pad, Theme.Gap);
            label.MinimumSize = new Size(140, 0);

            row.Cap.MinimumCapWidth = 220;
            row.Cap.Text = row.Value.ToString();
            row.Cap.Margin = new Padding(0, 0, 0, Theme.Gap);
            row.Cap.Cursor = Cursors.Hand;

            var captured = row;
            row.Cap.Click += (_, _) => Arm(captured);

            body.Controls.Add(label);
            body.Controls.Add(row.Cap);
        }

        var hint = Theme.HintLabel(strings.HotkeysHint, ContentWidth);
        hint.Margin = new Padding(0, Theme.Gap, 0, 0);
        body.Controls.Add(hint);
        body.SetColumnSpan(hint, 2);

        var reset = Theme.Button(strings.HotkeysDefaults);
        var ok = Theme.PrimaryButton(strings.ButtonOk, DialogResult.OK);
        var cancel = Theme.Button(strings.ButtonCancel, DialogResult.Cancel);

        reset.Click += (_, _) =>
        {
            Cancel();

            foreach (var row in _rows)
            {
                row.Value = row.Default;
                row.Cap.Text = row.Value.ToString();
            }
        };

        ok.Click += OnConfirm;

        // A gap between the pair that answers the window and the one that only changes what
        // is on it, so "Defaults" cannot be hit while reaching for "Cancel".
        var spacer = new Panel { Width = Theme.Wide, Height = 1, Margin = Padding.Empty };

        Controls.Add(Theme.Page(body, cancel, ok, spacer, reset));

        AcceptButton = ok;
        CancelButton = cancel;
    }

    public Hotkey WordHotkey => _rows[0].Value;

    public Hotkey SelectionHotkey => _rows[1].Value;

    public Hotkey UndoHotkey => _rows[2].Value;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Application.AddMessageFilter(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(this);
        base.OnFormClosed(e);
    }

    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        if (_armed is null || m.Msg is not (WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP))
        {
            return false;
        }

        bool isKeyDown = m.Msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        Record((ushort)(int)m.WParam, isKeyDown);

        // Swallow everything while capturing, so a captured Space or Enter does not also
        // press the button underneath it.
        return true;
    }

    private void Record(ushort virtualKey, bool isKeyDown)
    {
        uint now = unchecked((uint)Environment.TickCount);
        bool tapped = false;
        var modifiers = CurrentModifiers();

        // Every tracker must see every event, so none of these calls may be skipped.
        foreach (var tracker in _trackers)
        {
            if (tracker.Feed(virtualKey, isKeyDown, now, modifiers))
            {
                Assign(new Hotkey(tracker.TrackedKey, Kind: HotkeyKind.DoubleTap));
                tapped = true;
            }
        }

        if (tapped || !isKeyDown)
        {
            return;
        }

        if (virtualKey == VirtualKeys.Escape)
        {
            Cancel();
            return;
        }

        // Backspace or Delete leaves the command unbound. Undo ships that way, so there has
        // to be a way back to it -- and a way to switch off a hotkey that turns out to
        // collide with something.
        if (virtualKey is VirtualKeys.Back or VirtualKeys.Delete)
        {
            Assign(Hotkey.None);
            return;
        }

        // A modifier on its own is not a chord; keep waiting to see if it becomes a tap.
        if (VirtualKeys.IsModifier(virtualKey))
        {
            return;
        }

        Assign(new Hotkey(virtualKey, CurrentModifiers()));
    }

    private static ModifierKeys CurrentModifiers()
    {
        var pressed = Control.ModifierKeys;
        var modifiers = Core.Keys.ModifierKeys.None;

        if ((pressed & Keys.Shift) != 0) modifiers |= Core.Keys.ModifierKeys.Shift;
        if ((pressed & Keys.Control) != 0) modifiers |= Core.Keys.ModifierKeys.Control;
        if ((pressed & Keys.Alt) != 0) modifiers |= Core.Keys.ModifierKeys.Alt;

        return modifiers;
    }

    private void Arm(Row row)
    {
        Cancel();

        _armed = row;
        row.Cap.Armed = true;
        row.Cap.Text = Localizer.Text.HotkeysPress;

        foreach (var tracker in _trackers)
        {
            tracker.Reset();
        }
    }

    private void Cancel()
    {
        if (_armed is null)
        {
            return;
        }

        _armed.Cap.Armed = false;
        _armed.Cap.Text = _armed.Value.ToString();
        _armed = null;
    }

    private void Assign(Hotkey hotkey)
    {
        if (_armed is null)
        {
            return;
        }

        _armed.Value = hotkey;
        _armed.Cap.Armed = false;
        _armed.Cap.Text = hotkey.ToString();
        _armed = null;
    }

    private void OnConfirm(object? sender, EventArgs e)
    {
        // Unbound rows are all equal to each other and none of them is a collision.
        var bound = _rows.Where(r => r.Value.IsSet).Select(r => r.Value).ToList();

        if (bound.Count == bound.Distinct().Count())
        {
            return;
        }

        MessageBox.Show(
            this,
            Localizer.Text.HotkeysDuplicate,
            "Switch Please",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);

        DialogResult = DialogResult.None;
    }

    /// <summary>One bindable command and the key cap standing in for it.</summary>
    private sealed class Row(string caption, Hotkey value, Hotkey fallback)
    {
        public string Caption { get; } = caption;

        public KeyCap Cap { get; } = new();

        public Hotkey Value { get; set; } = value;

        /// <summary>What the "defaults" button restores this row to.</summary>
        public Hotkey Default { get; } = fallback;
    }
}
