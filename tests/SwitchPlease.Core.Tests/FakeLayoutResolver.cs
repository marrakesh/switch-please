using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Layouts;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Stands in for Windows so the conversion logic can be tested anywhere.
///
/// The real resolver asks the operating system what each physical key types under each
/// installed layout. This does the same from the fixed rows in <see cref="LayoutFixture"/>,
/// using the position of a character in its row as the scan code -- which is exactly the
/// relationship the real thing relies on, and the only property of a scan code any of this
/// code cares about.
/// </summary>
internal sealed class FakeLayoutResolver(params LayoutInfo[] layouts) : ILayoutResolver
{
    /// <summary>All three, in the order a machine with these keyboards would report them.</summary>
    public static FakeLayoutResolver All { get; } =
        new(LayoutFixture.English, LayoutFixture.Russian, LayoutFixture.Ukrainian);

    /// <summary>The ordinary case: one Latin and one Cyrillic layout.</summary>
    public static FakeLayoutResolver EnglishAndRussian { get; } =
        new(LayoutFixture.English, LayoutFixture.Russian);

    public IReadOnlyList<LayoutInfo> InstalledLayouts { get; } = layouts;

    public char ResolveCharacter(in KeyStroke stroke, LayoutInfo layout)
    {
        string row = LayoutFixture.RowOf(layout);
        int index = stroke.ScanCode - 1;

        if (index < 0 || index >= row.Length)
        {
            // A key this table says nothing about, e.g. the space bar. The caller keeps
            // whatever the stroke originally produced, which is what Windows does too.
            return '\0';
        }

        char produced = row[index];
        bool upper = (stroke.Modifiers & ModifierKeys.Shift) != 0;

        // Caps Lock turns letters round and leaves everything else alone, as Windows does.
        if ((stroke.Modifiers & ModifierKeys.CapsLock) != 0 && char.IsLetter(produced))
        {
            upper = !upper;
        }

        return upper ? char.ToUpperInvariant(produced) : produced;
    }

    public LayoutMap GetMap(LayoutInfo source, LayoutInfo target) => LayoutFixture.MapFor(source, target);

    /// <summary>
    /// Builds the record of someone typing <paramref name="text"/> with
    /// <paramref name="layout"/> active -- the characters they actually got, along with the
    /// keys they pressed to get them.
    /// </summary>
    /// <param name="text">What appeared on screen.</param>
    /// <param name="capsLock">
    /// Whether Caps Lock was on, in which case a small letter on screen is one Shift was held
    /// for.
    /// </param>
    public static TypingBuffer Typed(string text, LayoutInfo layout, bool capsLock = false) =>
        Append(new TypingBuffer(), text, layout, capsLock);

    /// <summary>
    /// Adds more typing to <paramref name="buffer"/>, for a line typed partly one way and
    /// partly another.
    /// </summary>
    public static TypingBuffer Append(TypingBuffer buffer, string text, LayoutInfo layout, bool capsLock = false)
    {
        string row = LayoutFixture.RowOf(layout);
        var capsLockState = capsLock ? ModifierKeys.CapsLock : ModifierKeys.None;

        foreach (char c in text)
        {
            char lower = char.ToLowerInvariant(c);
            int index = row.IndexOf(lower);

            if (index < 0)
            {
                // Space and punctuation outside the table: no layout changes them, so the
                // scan code can be anything the table does not claim.
                buffer.Append(new KeyStroke(0, 0, capsLockState, c));
                continue;
            }

            bool shifted = capsLock ? char.IsLower(c) : char.IsUpper(c);
            var modifiers = (shifted ? ModifierKeys.Shift : ModifierKeys.None) | capsLockState;

            buffer.Append(new KeyStroke((ushort)(index + 1), 0, modifiers, c));
        }

        return buffer;
    }
}
