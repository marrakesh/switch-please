namespace SwitchPlease.Core.Layouts;

/// <summary>
/// An installed keyboard layout. <paramref name="Handle"/> is the Win32 HKL, kept as a
/// plain integer so this assembly stays free of platform references.
/// </summary>
public sealed record LayoutInfo(nint Handle, int LanguageId, string CultureName, string DisplayName)
{
    /// <summary>Two-letter tag shown in the tray icon, e.g. "RU" or "EN".</summary>
    public string ShortTag =>
        CultureName.Length >= 2 ? CultureName[..2].ToUpperInvariant() : "??";

    public override string ToString() => $"{DisplayName} ({ShortTag})";
}
