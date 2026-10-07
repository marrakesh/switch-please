using SwitchPlease.Core.Keys;

namespace SwitchPlease.Core.Conversion;

/// <summary>
/// Text typed with Caps Lock left on by mistake: "пРИВЕТ" for "Привет", "hELLO" for "Hello".
///
/// The evidence is the Shift key. Someone who switched Caps Lock on because they wanted
/// capitals has no reason to hold Shift as well; someone who does not know it is on reaches
/// for Shift at the start of a sentence or a name, and gets a small letter for it. So a
/// stretch of typing done with Caps Lock on counts as a slip only when Shift was used on a
/// letter somewhere inside it, and then the whole stretch is read as if Caps Lock had been
/// off -- including the words in it that had no Shift, which is most of them.
///
/// Deliberate capitals survive: "NASA" typed with Caps Lock on has no Shift in it. So do the
/// words that look the part but were typed with Caps Lock off, "iOS" and "mRNA".
/// </summary>
public static class CapsLockSlip
{
    /// <summary>
    /// Marks the strokes that belong to a slip, or returns null when no slip reaches into
    /// <paramref name="range"/>.
    ///
    /// The whole buffer is read, not only the range, because the evidence is often outside
    /// it: in "пРИВЕТ КАК ДЕЛА" the Shift is in the first word, and the last one, converted on
    /// its own, has to know.
    /// </summary>
    public static bool[]? Find(IReadOnlyList<KeyStroke> strokes, StrokeRange range)
    {
        ArgumentNullException.ThrowIfNull(strokes);

        bool[]? slipped = null;
        int start = 0;

        while (start < strokes.Count)
        {
            if (!HasCapsLock(strokes[start]))
            {
                start++;
                continue;
            }

            int end = start;
            bool shifted = false;

            while (end < strokes.Count && HasCapsLock(strokes[end]))
            {
                shifted |= IsShiftedLetter(strokes[end]);
                end++;
            }

            if (shifted && start < range.End && end > range.Start)
            {
                slipped ??= new bool[strokes.Count];
                Array.Fill(slipped, true, start, end - start);
            }

            start = end;
        }

        return slipped;
    }

    /// <summary>
    /// Whether a piece of text reads as typed with Caps Lock on by mistake, judged from the
    /// characters alone. For a selection, which comes with no record of the keys behind it.
    ///
    /// Every word has to be capitals after its first letter, and at least one has to start
    /// with a small letter followed by two or more capitals: "пРИВЕТ КАК ДЕЛА". That alone
    /// would also describe "mRNA", which is why the caller asks it only while Caps Lock is on.
    /// </summary>
    public static bool LooksInverted(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        bool inverted = false;
        int position = 0;
        bool firstIsLower = false;
        int capitalsAfterFirst = 0;

        foreach (char c in text)
        {
            if (!char.IsLetter(c))
            {
                inverted |= firstIsLower && capitalsAfterFirst >= 2;
                position = 0;
                firstIsLower = false;
                capitalsAfterFirst = 0;
                continue;
            }

            if (position == 0)
            {
                firstIsLower = char.IsLower(c);
            }
            else if (char.IsLower(c))
            {
                // An ordinary word, "Привет" or "привет". Not a slip anywhere in this text.
                return false;
            }
            else
            {
                capitalsAfterFirst++;
            }

            position++;
        }

        return inverted || (firstIsLower && capitalsAfterFirst >= 2);
    }

    /// <summary>Swaps the case of every letter, which is all Caps Lock did to them.</summary>
    public static string Invert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return string.Create(text.Length, text, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];

                span[i] = char.IsUpper(c) ? char.ToLowerInvariant(c)
                    : char.IsLower(c) ? char.ToUpperInvariant(c)
                    : c;
            }
        });
    }

    private static bool HasCapsLock(in KeyStroke stroke) => (stroke.Modifiers & ModifierKeys.CapsLock) != 0;

    /// <summary>
    /// A letter Shift was held for and which came out small, because Caps Lock turned it
    /// round. A layout whose Caps Lock leaves some letter alone produces a capital there, and
    /// that is not evidence of anything.
    /// </summary>
    private static bool IsShiftedLetter(in KeyStroke stroke) =>
        (stroke.Modifiers & ModifierKeys.Shift) != 0
        && char.IsLetter(stroke.Character)
        && char.IsLower(stroke.Character);
}
