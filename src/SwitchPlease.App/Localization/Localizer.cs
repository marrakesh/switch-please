using System.Runtime.InteropServices;
using SwitchPlease.Core.Localization;

namespace SwitchPlease.App.Localization;

/// <summary>
/// Holds the interface language and resolves it.
///
/// Which languages exist is not decided here: the list lives with the translations, so
/// adding one is a change to a single file. This only answers "which of them is in force",
/// and the one question that needs Windows.
///
/// The display language is read through GetUserDefaultUILanguage rather than CultureInfo,
/// because this application builds with InvariantGlobalization: no ICU data is shipped, so
/// every culture would come back as the invariant one and the interface would always be
/// English.
/// </summary>
public static class Localizer
{
    /// <summary>The setting value meaning "follow the Windows display language".</summary>
    public const string Auto = "auto";

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    /// <summary>Strings for the language currently in force.</summary>
    public static UiStrings Text { get; private set; } = Translations.Default.Strings;

    /// <summary>What the user chose: a tag, or <see cref="Auto"/>.</summary>
    public static string Selected { get; private set; } = Auto;

    /// <summary>The language actually being displayed, with Auto already resolved.</summary>
    public static UiLanguage Effective { get; private set; } = Translations.Default;

    /// <summary>Languages offered in the menu.</summary>
    public static IReadOnlyList<UiLanguage> Available => Translations.All;

    /// <summary>
    /// Switches the interface. Anything unrecognised is treated as Auto, so a hand-edited
    /// settings file cannot leave the application with no strings at all.
    /// </summary>
    public static void Use(string? tagOrAuto)
    {
        var chosen = Translations.Find(tagOrAuto);

        Selected = chosen?.Tag ?? Auto;
        Effective = chosen ?? DetectFromWindows();
        Text = Effective.Strings;
    }

    private static UiLanguage DetectFromWindows()
    {
        try
        {
            int primary = GetUserDefaultUILanguage() & 0x3FF;

            // Anything the application does not ship falls back to English rather than to
            // nothing.
            return Translations.ForWindowsLanguage(primary) ?? Translations.Default;
        }
        catch (EntryPointNotFoundException)
        {
            return Translations.Default;
        }
    }
}
