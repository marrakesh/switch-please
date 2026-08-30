using SwitchPlease.App.Localization;
using SwitchPlease.Core.Config;

namespace SwitchPlease.App;

/// <summary>
/// The settings that used to be reachable only by hand-editing settings.json.
///
/// Everything here was already configurable; what was missing was any way to discover that.
/// The tray menu carries the switches people flip often, and this carries the numbers they
/// set once -- how cautious automatic correction should be, how fast a double tap has to be,
/// and which applications to keep out of.
///
/// Grouped rather than listed. Eleven settings in one column is a wall; four headings turn
/// it into four short answers to four questions, and puts the ones about privacy together
/// where someone looking for them will find them.
/// </summary>
public sealed class SettingsForm : Form
{
    /// <summary>
    /// The sensitivity is a fraction, and a slider is the honest control for it: the exact
    /// number means nothing to anyone, whereas "more cautious in that direction" does.
    /// Stored as hundredths because TrackBar counts in whole numbers.
    /// </summary>
    private const int SensitivityScale = 100;

    /// <summary>Milliseconds per character when the typewriter effect is switched on here.</summary>
    private const int DefaultTypewriterSpeed = 12;

    private const int ContentWidth = 470;

    /// <summary>Tallest the scrolling area gets before it scrolls instead of growing.</summary>
    private const int MaximumBodyHeight = 560;

    private readonly TrackBar _sensitivity;
    private readonly NumericUpDown _delay;
    private readonly NumericUpDown _tapWindow;
    private readonly NumericUpDown _tapHold;
    private readonly NumericUpDown _minimumWord;
    private readonly CheckBox _passwordFields;
    private readonly CheckBox _fullscreen;
    private readonly CheckBox _typewriter;
    private readonly CheckBox _logText;
    private readonly CheckBox _updates;
    private readonly TextBox _excluded;
    private readonly Label _sensitivityValue;

    // The checkbox only chooses between off and on; the speed itself has no control, so it
    // is remembered from the settings this window was opened with. Reading it back off the
    // object being written to would work only as long as that is the same object, which is
    // not what ApplyTo promises.
    private readonly int _typewriterSpeed;

    public SettingsForm(AppSettings settings)
    {
        var strings = Localizer.Text;

        Theme.Apply(this);

        Text = strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var stack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(Theme.Wide, Theme.Pad, Theme.Wide, Theme.Pad),
        };

        stack.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // ---- Automatic correction -------------------------------------------------------
        Section(stack, strings.SettingsGroupCorrection, first: true);

        Caption(stack, strings.SettingsSensitivity);

        var slider = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
        };

        _sensitivity = new TrackBar
        {
            Width = ContentWidth - 60,
            Height = 40,
            Minimum = 5,
            Maximum = 95,
            TickFrequency = 10,
            SmallChange = 1,
            LargeChange = 5,
            Margin = new Padding(0, 0, Theme.Gap, 0),
            Value = Math.Clamp((int)Math.Round(settings.AutoDetectSensitivity * SensitivityScale), 5, 95),
        };

        _sensitivityValue = new Label
        {
            AutoSize = true,
            Font = Theme.Heading,
            ForeColor = Theme.Accent,
            Margin = new Padding(0, 12, 0, 0),
        };

        _sensitivity.ValueChanged += (_, _) => ShowSensitivity();
        ShowSensitivity();

        slider.Controls.Add(_sensitivity);
        slider.Controls.Add(_sensitivityValue);
        stack.Controls.Add(slider);

        Hint(stack, strings.SettingsSensitivityHint);

        _delay = Number(stack, strings.SettingsDelay, settings.CorrectionDelayMilliseconds, 0, 200);
        Hint(stack, strings.SettingsDelayHint);

        _minimumWord = Number(stack, strings.SettingsMinimumWord, settings.MinimumAutoWordLength, 2, 10);

        // ---- Double tap -----------------------------------------------------------------
        Section(stack, strings.SettingsGroupHotkeys);

        _tapWindow = Number(stack, strings.SettingsTapWindow, settings.DoubleTapWindowMilliseconds, 120, 2000);
        _tapHold = Number(stack, strings.SettingsTapHold, settings.DoubleTapHoldMilliseconds, 80, 2000);

        // ---- Privacy and safety ---------------------------------------------------------
        Section(stack, strings.SettingsGroupPrivacy);

        _passwordFields = Check(stack, strings.SettingsPasswordFields, settings.RespectPasswordFields);
        _fullscreen = Check(stack, strings.SettingsFullscreen, settings.PauseInFullscreenApps);
        Hint(stack, strings.SettingsFullscreenHint);

        _logText = Check(stack, strings.SettingsLogText, settings.LogTextContent);
        Hint(stack, strings.SettingsLogTextHint);

        _updates = Check(stack, strings.SettingsUpdates, settings.CheckForUpdates);

        _typewriterSpeed = settings.TypewriterMillisecondsPerCharacter;
        _typewriter = Check(stack, strings.SettingsTypewriter, _typewriterSpeed > 0);
        Hint(stack, strings.SettingsTypewriterHint);

        // ---- Applications ---------------------------------------------------------------
        Section(stack, strings.SettingsGroupApplications);

        Caption(stack, strings.SettingsExcluded);

        _excluded = new TextBox
        {
            Width = ContentWidth,
            Height = 108,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, Theme.Tight),
            Text = string.Join(Environment.NewLine, settings.ExcludedProcesses),
        };

        stack.Controls.Add(_excluded);
        Hint(stack, strings.SettingsExcludedHint);

        // The window would otherwise be as tall as its contents, which on a laptop screen is
        // taller than the screen. Past a point it scrolls instead.
        var body = new Panel
        {
            AutoScroll = true,
            Width = stack.PreferredSize.Width + SystemInformation.VerticalScrollBarWidth,
            Height = Math.Min(stack.PreferredSize.Height, MaximumBodyHeight),
            Margin = Padding.Empty,
        };

        body.Controls.Add(stack);

        var ok = Theme.PrimaryButton(strings.ButtonOk, DialogResult.OK);
        var cancel = Theme.Button(strings.ButtonCancel, DialogResult.Cancel);

        Controls.Add(Theme.Page(body, cancel, ok));

        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>
    /// Copies what was chosen back onto <paramref name="settings"/>. Only called once the
    /// dialog has been accepted, so cancelling leaves the live settings untouched.
    /// </summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.AutoDetectSensitivity = (double)_sensitivity.Value / SensitivityScale;
        settings.CorrectionDelayMilliseconds = (int)_delay.Value;
        settings.DoubleTapWindowMilliseconds = (int)_tapWindow.Value;
        settings.DoubleTapHoldMilliseconds = (int)_tapHold.Value;
        settings.MinimumAutoWordLength = (int)_minimumWord.Value;
        settings.RespectPasswordFields = _passwordFields.Checked;
        settings.PauseInFullscreenApps = _fullscreen.Checked;

        // A speed rather than a flag in the file, so anyone who wants it slower or faster
        // can say so by hand; switching it on here only restores whatever they had, or the
        // default if they never chose one.
        settings.TypewriterMillisecondsPerCharacter = _typewriter.Checked
            ? Math.Max(_typewriterSpeed, DefaultTypewriterSpeed)
            : 0;
        settings.LogTextContent = _logText.Checked;
        settings.CheckForUpdates = _updates.Checked;

        settings.ExcludedProcesses =
        [
            .. _excluded.Lines
                .Select(line => line.Trim().ToLowerInvariant())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.Ordinal),
        ];
    }

    private void ShowSensitivity() =>
        _sensitivityValue.Text = ((double)_sensitivity.Value / SensitivityScale).ToString("F2");

    /// <summary>A heading with a rule under it, opening a group of related settings.</summary>
    private static void Section(TableLayoutPanel stack, string text, bool first = false)
    {
        var label = Theme.SectionLabel(text);
        label.Margin = new Padding(0, first ? 0 : Theme.Wide, 0, Theme.Tight);

        stack.Controls.Add(label);
        stack.Controls.Add(Theme.Separator(ContentWidth));
    }

    private static void Caption(TableLayoutPanel stack, string text)
    {
        var label = Theme.BodyLabel(text);
        label.MaximumSize = new Size(ContentWidth, 0);
        label.Margin = new Padding(0, 0, 0, Theme.Tight);

        stack.Controls.Add(label);
    }

    private static void Hint(TableLayoutPanel stack, string text) =>
        stack.Controls.Add(Theme.HintLabel(text, ContentWidth));

    /// <summary>Label on the left, spinner on the right, both on one row.</summary>
    private static NumericUpDown Number(
        TableLayoutPanel stack,
        string caption,
        int value,
        int low,
        int high)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,

            // A minimum rather than a width: AutoSize wins over Width, and without a floor
            // every row would end just after its own label, leaving the spinners in a
            // ragged column.
            MinimumSize = new Size(ContentWidth, 0),
            Margin = new Padding(0, 0, 0, Theme.Gap),
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = Theme.BodyLabel(caption);
        label.Anchor = AnchorStyles.Left;
        label.MaximumSize = new Size(ContentWidth - 150, 0);
        label.Margin = new Padding(0, Theme.Tight + 1, Theme.Gap, 0);

        var box = new NumericUpDown
        {
            Width = 120,
            Anchor = AnchorStyles.Right,
            Minimum = low,
            Maximum = high,
            Increment = 1,
            TextAlign = HorizontalAlignment.Right,
            Margin = Padding.Empty,

            // A value outside the range is not the user's mistake to fix: it comes from a
            // settings file written by an older build or edited by hand.
            Value = Math.Clamp(value, low, high),
        };

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(box, 1, 0);
        stack.Controls.Add(row);

        return box;
    }

    private static CheckBox Check(TableLayoutPanel stack, string caption, bool value)
    {
        var box = new CheckBox
        {
            Text = caption,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Checked = value,
            Margin = new Padding(0, 0, 0, Theme.Gap),
        };

        stack.Controls.Add(box);
        return box;
    }
}
