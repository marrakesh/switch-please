using System.Text.Json.Serialization;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Keys;

namespace SwitchPlease.Core.Config;

/// <summary>How a hotkey is recognised in the key stream.</summary>
public enum HotkeyKind
{
    /// <summary>A key pressed with modifiers held, e.g. Ctrl+Shift+L.</summary>
    Chord = 0,

    /// <summary>
    /// A modifier key tapped twice in quick succession. The only kind that is guaranteed to
    /// be available: compact and laptop keyboards frequently have no Pause/Break key, and
    /// every free F-key or letter chord is already taken by some application.
    /// </summary>
    DoubleTap = 1,
}

/// <summary>A global hotkey, matched against the low-level hook stream.</summary>
/// <param name="VirtualKey">
/// Win32 virtual-key code. For <see cref="HotkeyKind.DoubleTap"/> this is the modifier
/// itself: Shift, Control or Alt.
/// </param>
/// <param name="Modifiers">Modifiers that must be held. Ignored for a double tap.</param>
public sealed record Hotkey(
    ushort VirtualKey,
    ModifierKeys Modifiers = ModifierKeys.None,
    HotkeyKind Kind = HotkeyKind.Chord)
{
    /// <summary>Double-tap Shift: works on every keyboard and collides with nothing.</summary>
    public static Hotkey ConvertWord { get; } = new(VirtualKeys.Shift, Kind: HotkeyKind.DoubleTap);

    /// <summary>Double-tap Ctrl, for the current selection or the whole buffered line.</summary>
    public static Hotkey ConvertSelection { get; } = new(VirtualKeys.Control, Kind: HotkeyKind.DoubleTap);

    /// <summary>
    /// No key at all. Virtual-key zero is not something a keyboard can produce, so a binding
    /// holding it never matches -- which is how an optional command stays unbound without a
    /// separate flag to check everywhere.
    /// </summary>
    public static Hotkey None { get; } = new(0);

    /// <summary>Whether this binding can ever match a keystroke.</summary>
    public bool IsSet => VirtualKey != 0;

    public override string ToString()
    {
        if (!IsSet)
        {
            return "-";
        }

        if (Kind == HotkeyKind.DoubleTap)
        {
            return $"{VirtualKeys.NameOf(VirtualKeys.NormalizeModifier(VirtualKey))} ×2";
        }

        var parts = new List<string>(4);

        if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & ModifierKeys.Win) != 0) parts.Add("Win");

        parts.Add(VirtualKeys.NameOf(VirtualKey));
        return string.Join("+", parts);
    }
}

/// <summary>User-visible configuration, persisted as JSON next to the executable's data folder.</summary>
public sealed class AppSettings
{
    /// <summary>Master switch. When false the hook stays installed but does nothing.</summary>
    public bool Enabled { get; set; } = true;

    public Hotkey ConvertWordHotkey { get; set; } = Hotkey.ConvertWord;

    public Hotkey ConvertSelectionHotkey { get; set; } = Hotkey.ConvertSelection;

    /// <summary>
    /// Puts back what the last correction changed.
    ///
    /// Unbound by default. Pressing the word hotkey again already reverses a word, because
    /// the record keeps the original scan codes; this is for the two cases where that does
    /// not help -- a correction made automatically, and a converted selection, which leaves
    /// nothing behind to convert back.
    /// </summary>
    public Hotkey UndoHotkey { get; set; } = Hotkey.None;

    /// <summary>
    /// Off by default on purpose: the manual hotkey is predictable, whereas automatic
    /// rewriting has to earn trust before it is allowed to touch what you type.
    /// </summary>
    public bool AutoDetectEnabled { get; set; }

    /// <summary>
    /// How much better the alternative reading must be before auto-conversion fires, 0..1.
    /// Higher means more conservative.
    /// </summary>
    public double AutoDetectSensitivity { get; set; } = 0.25;

    /// <summary>
    /// How long to wait before rewriting a word during auto-correction. The keystroke that
    /// ended the word is already on its way to the application; sending backspaces before it
    /// lands would delete the wrong characters.
    /// </summary>
    public int CorrectionDelayMilliseconds { get; set; } = 15;

    public bool PlaySoundOnConvert { get; set; } = true;

    /// <summary>
    /// Show the new layout beside the text cursor for a moment whenever it changes, whether
    /// the user switched it or a correction did.
    ///
    /// Off by default, like everything else that puts something on screen the user did not
    /// ask for: a window appearing next to what you are typing is welcome only to someone
    /// who went looking for it.
    /// </summary>
    public bool ShowLayoutAtCaret { get; set; }

    /// <summary>
    /// How long the layout indicator stays before it fades, in milliseconds. It goes sooner
    /// whenever the user presses a key or clicks, so this is the most it can be in the way.
    /// </summary>
    public int LayoutIndicatorMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Type the correction one character at a time, this many milliseconds apart.
    ///
    /// Zero, the default, sends it in a single call. The effect is deliberately opt-in: it
    /// is charming, and it is also latency added to the one operation whose whole design
    /// goal is to finish before the user can type over it.
    /// </summary>
    public int TypewriterMillisecondsPerCharacter { get; set; }

    /// <summary>
    /// Longest gap between the two presses of a double-tap hotkey, in milliseconds. Worth
    /// raising for anyone who finds the default hard to hit, and lowering for anyone who
    /// triggers it by accident.
    /// </summary>
    public int DoubleTapWindowMilliseconds { get; set; } = 500;

    /// <summary>
    /// Longest either press of a double tap may last. Holding a modifier is how it is
    /// normally used, so a long press must not count towards the pair.
    /// </summary>
    public int DoubleTapHoldMilliseconds { get; set; } = 400;

    /// <summary>
    /// Refuse to record or rewrite anything while the caret is in a password field.
    ///
    /// Excluding whole applications covers the dedicated password managers; this covers the
    /// login form on a web page, which is inside an application the user certainly does want
    /// the switcher working in. Detection is best-effort, so this is a safeguard rather than
    /// a guarantee.
    /// </summary>
    public bool RespectPasswordFields { get; set; } = true;

    /// <summary>
    /// Hold off entirely while a game or a presentation is on screen.
    ///
    /// The reason is the default hotkey. Double-tapping Shift is deliberately something
    /// ordinary typing never does, but in a game Shift is sprint, and tapping it twice in
    /// half a second is what running feels like. Without this the switcher fires again and
    /// again in games and rewrites whatever the chat box was holding.
    /// </summary>
    public bool PauseInFullscreenApps { get; set; } = true;

    /// <summary>
    /// Shortest word automatic correction will touch. Below four characters there is barely
    /// enough evidence to judge, and the words that short are the most common ones.
    /// </summary>
    public int MinimumAutoWordLength { get; set; } = 3;

    /// <summary>
    /// Whether the diagnostic log may contain the text that was typed.
    ///
    /// Off by default, and deliberately separate from the diagnostics switch: a log of
    /// everything a keyboard hook saw is exactly the file nobody wants left behind on their
    /// disk. With this off the log records what was decided and why, with the text itself
    /// reduced to its length.
    /// </summary>
    public bool LogTextContent { get; set; }

    /// <summary>Size at which the diagnostic log is rolled over, in bytes.</summary>
    public long LogMaximumBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Check GitHub for a newer release when the application starts.
    ///
    /// Off by default. It is a network request to a third party that the user did not ask
    /// for, and a tray utility that phones home without being told to is not something to
    /// switch on for them.
    /// </summary>
    public bool CheckForUpdates { get; set; }

    // Deliberately no RunAtStartup here. Autostart is recorded in the Run key under HKCU,
    // which Windows persists and the installer writes; the tray menu reads it back rather
    // than keeping a copy. A copy in this file could only drift away from the registry,
    // which is what the installer and Windows itself act on.

    /// <summary>
    /// Interface language: "auto" to follow Windows, otherwise one of the two-letter tags
    /// in Translations.All. Kept as a string, and deliberately not enumerated here, so that
    /// adding a language stays a change to one file.
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Executable names where the switcher stays out of the way entirely. Terminals and
    /// password managers are the usual suspects.
    /// </summary>
    /// <remarks>
    /// Terminals are here for a reason of their own: the selection hotkey has to ask the
    /// focused window to copy, and in a terminal the usual way of asking is also the way of
    /// interrupting whatever is running. Ctrl+Insert avoids that, but a terminal is still
    /// somewhere text gets typed for a machine rather than for a reader, so there is nothing
    /// for the switcher to fix there.
    /// </remarks>
    public List<string> ExcludedProcesses { get; set; } =
    [
        "keepass.exe",
        "keepassxc.exe",
        "1password.exe",
        "bitwarden.exe",
        "dashlane.exe",
        "lastpass.exe",
        "enpass.exe",
        "windowsterminal.exe",
        "wt.exe",
        "conhost.exe",
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "mintty.exe",
        "putty.exe",
        "alacritty.exe",
        "wezterm-gui.exe",

        // Development environments, for a different reason again: what gets typed into them
        // is mostly identifiers, which the guards would refuse anyway, and single letters
        // are chords. There is very little here for the switcher to fix and a great deal for
        // it to get wrong.
        "devenv.exe",
        "rider64.exe",
        "idea64.exe",
        "pycharm64.exe",
        "webstorm64.exe",
        "clion64.exe",
        "goland64.exe",
        "studio64.exe",
        "code.exe",
        "code - insiders.exe",
        "cursor.exe",
        "sublime_text.exe",
        "gvim.exe",
        "emacs.exe",
    ];

    /// <summary>
    /// Words automatic correction never rewrites. The hotkeys still work on them: pressing
    /// one is a request, and a request is not what this list is about.
    ///
    /// Filled by undoing an automatic correction, and editable in the settings window. Kept
    /// in lower case without surrounding punctuation; see <see cref="WordExceptions"/>.
    /// </summary>
    /// <remarks>
    /// The one place typed text is written to disk without the user switching anything on,
    /// and only text they have pointed at: a word whose correction they undid. The privacy
    /// section of the README says so.
    /// </remarks>
    public List<string> NeverCorrectWords { get; set; } = [];

    /// <summary>
    /// Adds <paramref name="word"/> to <see cref="NeverCorrectWords"/>.
    /// </summary>
    /// <returns>False when it was already there, or had no letters to keep.</returns>
    public bool RememberNeverCorrect(string word)
    {
        string key = WordExceptions.Normalize(word);

        if (key.Length == 0 || NeverCorrectWords.Contains(key, StringComparer.Ordinal))
        {
            return false;
        }

        NeverCorrectWords.Add(key);
        return true;
    }

    /// <summary>
    /// Applications where automatic correction differs from the global setting, by
    /// executable name.
    ///
    /// The exclusion list is all or nothing, and that is too blunt for the commonest
    /// arrangement there is: automatic correction is wanted in the browser and the chat
    /// window, and unwanted in the editor -- where the switcher should still be one hotkey
    /// away, which excluding it entirely would take away too.
    ///
    /// An entry that is not here follows <see cref="AutoDetectEnabled"/>.
    /// </summary>
    /// <remarks>
    /// Read-only, and filled rather than replaced, for a reason that is invisible until the
    /// settings come back from disk: given a setter, System.Text.Json throws this dictionary
    /// away and builds its own, which uses the default comparer -- so matching without regard
    /// to case works in memory and stops working after a restart. Without a setter it is
    /// ignored on the way in instead, which is worse again, so the handling has to be said
    /// out loud.
    ///
    /// <see cref="ExcludedProcesses"/> must keep its setter for the opposite reason: it has
    /// defaults, and filling rather than replacing would resurrect every entry the user
    /// deleted.
    /// </remarks>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, bool> AutoDetectPerApplication { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether automatic correction applies in <paramref name="process"/>.
    ///
    /// The exclusion list wins over everything: an application the switcher is told to keep
    /// out of is one it keeps out of, whatever else is configured for it.
    /// </summary>
    public bool AutoDetectIn(string? process)
    {
        if (string.IsNullOrEmpty(process))
        {
            return AutoDetectEnabled;
        }

        if (IsExcluded(process))
        {
            return false;
        }

        return AutoDetectPerApplication.TryGetValue(process, out bool wanted)
            ? wanted
            : AutoDetectEnabled;
    }

    /// <summary>Whether the switcher stays out of <paramref name="process"/> entirely.</summary>
    public bool IsExcluded(string? process) =>
        !string.IsNullOrEmpty(process)
        && ExcludedProcesses.Contains(process, StringComparer.OrdinalIgnoreCase);

    /// <summary>Writes latency samples for the hook callback to the log.</summary>
    public bool DiagnosticsEnabled { get; set; }
}
