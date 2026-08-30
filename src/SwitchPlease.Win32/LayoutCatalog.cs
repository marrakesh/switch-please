using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Layouts;

namespace SwitchPlease.Win32;

/// <summary>
/// Reduces what Windows reports to the set of layouts that are actually distinguishable.
///
/// GetKeyboardLayoutList is generous: a typical machine reports the same input language
/// several times, once per text-service profile, and the list mixes those with genuinely
/// different layouts. On the machine this was developed against it returned five entries
/// for what a user would describe as "English, Russian and Ukrainian".
///
/// Two layouts matter to us only insofar as they type different characters, so entries are
/// grouped by the characters they produce and one representative is kept from each group.
/// Without this, cycling to "the next layout" lands on an identical one and appears to do
/// nothing.
/// </summary>
internal static class LayoutCatalog
{
    /// <summary>
    /// Builds the string a layout would produce across the printable keys. Layouts with the
    /// same signature are interchangeable for every purpose this application has.
    /// </summary>
    internal static string Signature(nint handle, ushort[] scanCodes)
    {
        var characters = new char[scanCodes.Length];

        for (int i = 0; i < scanCodes.Length; i++)
        {
            characters[i] = KeyboardLayoutService.ResolveCharacter(scanCodes[i], ModifierKeys.None, handle);
        }

        return new string(characters);
    }

    /// <summary>Keeps the first layout of each distinct signature, preserving Windows' order.</summary>
    internal static List<LayoutInfo> Deduplicate(IEnumerable<LayoutInfo> layouts, ushort[] scanCodes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var signatures = new Dictionary<nint, string>();
        var result = new List<LayoutInfo>();

        foreach (var layout in layouts)
        {
            string signature = Signature(layout.Handle, scanCodes);

            // A layout that types nothing is a text-service placeholder, not a keyboard.
            if (signature.Trim('\0').Length == 0)
            {
                continue;
            }

            if (seen.Add(signature))
            {
                result.Add(layout);
                signatures[layout.Handle] = signature;
            }
        }

        return Disambiguate(result, signatures);
    }

    /// <summary>
    /// Windows labels a layout by its input language, not by the keys it types, so two
    /// genuinely different layouts can arrive under the same name -- a Czech QWERTY layout
    /// registered under English, for instance. Where that happens the name is extended with
    /// the start of the layout's digit row, which is usually exactly where such layouts
    /// differ.
    /// </summary>
    private static List<LayoutInfo> Disambiguate(List<LayoutInfo> layouts, Dictionary<nint, string> signatures)
    {
        var duplicated = layouts
            .GroupBy(layout => layout.DisplayName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (duplicated.Count == 0)
        {
            return layouts;
        }

        for (int i = 0; i < layouts.Count; i++)
        {
            var layout = layouts[i];

            if (!duplicated.Contains(layout.DisplayName))
            {
                continue;
            }

            string signature = signatures[layout.Handle];
            string hint = signature.Length >= 4 ? signature[..4] : signature;

            layouts[i] = layout with { DisplayName = $"{layout.DisplayName} [{hint}]" };
        }

        return layouts;
    }
}
