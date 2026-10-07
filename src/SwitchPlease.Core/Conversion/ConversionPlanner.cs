using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Layouts;

namespace SwitchPlease.Core.Conversion;

/// <summary>
/// What the switcher needs to know about the installed layouts.
///
/// An interface rather than the Windows class directly, because everything below this line
/// is arithmetic on characters and every decision it makes -- which layout to switch to,
/// when to refuse -- is worth a test. Windows supplies the real implementation; the tests
/// supply a fixed QWERTY/ЙЦУКЕН table and run anywhere.
/// </summary>
public interface ILayoutResolver
{
    IReadOnlyList<LayoutInfo> InstalledLayouts { get; }

    /// <summary>What the key that produced <paramref name="stroke"/> types under <paramref name="layout"/>.</summary>
    char ResolveCharacter(in KeyStroke stroke, LayoutInfo layout);

    LayoutMap GetMap(LayoutInfo source, LayoutInfo target);
}

/// <summary>What one correction should erase and what it should type in its place.</summary>
/// <param name="Word">Where the word sits in the buffer.</param>
/// <param name="Tail">Everything after it, usually the space that ended it.</param>
/// <param name="Original">The word as typed.</param>
/// <param name="Converted">The word as it reads in the target layout.</param>
/// <param name="Suffix">The tail, retyped unchanged so the caret lands where it started.</param>
/// <param name="TargetLayout">The layout to switch to afterwards.</param>
public sealed record ConversionPlan(
    StrokeRange Word,
    StrokeRange Tail,
    string Original,
    string Converted,
    string Suffix,
    LayoutInfo TargetLayout)
{
    /// <summary>
    /// The word as the user meant it in the layout that was active: <see cref="Original"/>
    /// with a Caps Lock slip undone, and otherwise the same. This, not what is on screen, is
    /// what a reading in another layout has to beat.
    /// </summary>
    public string Intended { get; init; } = Original;

    /// <summary>
    /// False for a correction that only undoes Caps Lock: the letters were right, so the
    /// keyboard already is too.
    /// </summary>
    public bool SwitchesLayout { get; init; } = true;

    /// <summary>Whether Caps Lock was on by mistake, so it should be switched off afterwards.</summary>
    public bool FixesCapsLock => !string.Equals(Intended, Original, StringComparison.Ordinal);

    /// <summary>How many characters have to be erased before the replacement is typed.</summary>
    public int EraseCount => Word.Length + Tail.Length;

    public bool ChangesNothing => string.Equals(Original, Converted, StringComparison.Ordinal);
}

/// <summary>
/// Decides what a correction should do, without doing any of it.
///
/// Kept apart from the service that sends keystrokes for one reason: this is where every
/// judgement call lives -- which of three installed layouts the user meant, whether the text
/// is already right and should be left alone -- and none of those could be tested while they
/// were tangled up with SendInput and a live keyboard hook.
/// </summary>
public sealed class ConversionPlanner(
    ILayoutResolver layouts,
    LanguageCatalog languages,
    IWordValidator validator)
{
    /// <summary>
    /// How much more plausible the text already on screen must be, compared with the best
    /// alternative, before the hotkey declines to touch it. Measured in RefusalMarginTests:
    /// at this value every correctly typed word is protected from a stray double-tap and no
    /// genuinely mistyped one is refused.
    ///
    /// It does not apply to the Russian/Ukrainian pair, whose two readings are both ordinary
    /// Cyrillic and score within a hair of each other; there the hotkey simply toggles.
    /// </summary>
    public const double RefuseConversionMargin = 0.20;

    /// <summary>
    /// How much of a text a source-to-target table must actually know before that pair is
    /// considered, so a table that would leave the text almost untouched cannot win by
    /// default.
    /// </summary>
    private const double MinimumMapCoverage = 0.5;

    /// <summary>
    /// Works out what to erase and what to type in its place. Returns null when there is
    /// nothing sensible to do: one installed layout, no layout that would change the text,
    /// or text that plainly reads better as it stands.
    /// </summary>
    /// <param name="explain">Receives the reason whenever the answer is "nothing".</param>
    public ConversionPlan? Build(
        TypingBuffer buffer,
        StrokeRange range,
        LayoutInfo activeLayout,
        Action<string>? explain = null)
    {
        string original = buffer.GetText(range);

        // A word typed with Caps Lock on by mistake is judged as it was meant, "Ghbdtn"
        // rather than "gHBDTN", and corrected as it was meant, "Привет". Read as it stands it
        // is mixed case, which every guard treats as an identifier and leaves alone.
        var slipped = CapsLockSlip.Find(buffer.Strokes, range);
        string intended = slipped is null ? original : Render(buffer, range, activeLayout, slipped);
        var capsLockOnly = CapsLockFix(buffer, range, original, intended, activeLayout);

        // A number is left alone. It has no letters, so no language can judge it, and it
        // used to fall through to the plain toggle below, which is meant for letters in an
        // alphabet nothing here models. With a Czech layout installed the number row is
        // letters, and a stray double tap after a year or a price rewrote it: measured on the
        // real layouts, "2024" became "ěéěč" and "150" became "+řé".
        //
        // The same keys are also how a Czech word made only of accented letters arrives:
        // "šíří" typed on the US layout is "3959", and "čí" is "49". Nothing tells the two
        // apart -- not the keys, and not a dictionary, since "čí" is a word. This favours the
        // number. Numbers are typed all the time and those words are a handful; a mangled
        // price or date can pass unnoticed, where "3959" left standing is plain to see.
        //
        // A selection is converted regardless (see ChooseMapFor). Selecting a number and
        // asking for it to be converted is deliberate, never a stray tap, and it is how such
        // a word can still be had.
        if (!intended.Any(char.IsLetter))
        {
            explain?.Invoke(capsLockOnly is null
                ? "left alone: no letters in it"
                : "only Caps Lock to undo: no letters in it");
            return capsLockOnly;
        }

        var (targetLayout, targetScore) = ChooseTargetLayout(buffer, range, activeLayout, intended, slipped);

        if (targetLayout is null)
        {
            explain?.Invoke(capsLockOnly is null
                ? "no other layout would change this text"
                : "only Caps Lock to undo: no other layout would change this text");
            return capsLockOnly;
        }

        var tail = TailAfter(buffer, range);

        var plan = new ConversionPlan(
            range, tail, original, Render(buffer, range, targetLayout, slipped), buffer.GetText(tail), targetLayout)
        {
            Intended = intended,
        };

        // Refuse to wreck text that is plainly already right. The hotkey is two taps of
        // Shift, which is easy to trigger by accident, and "convert anyway" would turn a
        // correct word into gibberish. Anything genuinely mistyped scores at or below its
        // alternative, so this only ever blocks the misfires.
        //
        // Text in an alphabet we do not model -- Czech, German, Turkish -- cannot be judged
        // this way at all, and pretending otherwise would refuse or mangle it at random.
        // There the hotkey behaves as a plain toggle, which is what the user asked for by
        // pressing it.
        if (!languages.IsFamiliar(intended, validator))
        {
            explain?.Invoke("converting without judging it: unfamiliar alphabet");
            return plan;
        }

        double typedScore = Plausibility(intended, plan.Converted);

        if (typedScore - targetScore >= RefuseConversionMargin)
        {
            explain?.Invoke(capsLockOnly is null
                ? $"left alone: already reads better than any alternative ({typedScore:F2} vs {targetScore:F2})"
                : $"only Caps Lock to undo: already reads better than any alternative ({typedScore:F2} vs {targetScore:F2})");
            return capsLockOnly;
        }

        return plan;
    }

    /// <summary>
    /// The correction that undoes a Caps Lock slip and nothing else, keeping the layout. Null
    /// when there was no slip.
    ///
    /// Automatic correction falls back to this when the word, once its case is put right,
    /// turns out to have been in the right layout all along: "пРИВЕТ" is Russian typed on the
    /// Russian keyboard, and only the capitals are wrong.
    /// </summary>
    public ConversionPlan? BuildCapsLockFix(TypingBuffer buffer, StrokeRange range, LayoutInfo activeLayout)
    {
        var slipped = CapsLockSlip.Find(buffer.Strokes, range);

        return slipped is null
            ? null
            : CapsLockFix(buffer, range, buffer.GetText(range), Render(buffer, range, activeLayout, slipped), activeLayout);
    }

    private static ConversionPlan? CapsLockFix(
        TypingBuffer buffer,
        StrokeRange range,
        string original,
        string intended,
        LayoutInfo activeLayout)
    {
        if (string.Equals(original, intended, StringComparison.Ordinal))
        {
            return null;
        }

        var tail = TailAfter(buffer, range);

        return new ConversionPlan(range, tail, original, intended, buffer.GetText(tail), activeLayout)
        {
            Intended = intended,
            SwitchesLayout = false,
        };
    }

    /// <summary>
    /// Everything typed after the word, usually the space that ended it. It is erased too,
    /// then retyped unchanged, so the caret ends up exactly where it started.
    /// </summary>
    private static StrokeRange TailAfter(TypingBuffer buffer, StrokeRange range) =>
        new(range.End, buffer.Count - range.End);

    /// <summary>
    /// Replays the recorded keys as if <paramref name="layout"/> had been active. Keys with
    /// no counterpart there keep whatever they originally produced.
    /// </summary>
    /// <param name="slipped">
    /// Strokes to replay as if Caps Lock had been off, from <see cref="CapsLockSlip.Find"/>.
    /// </param>
    public string Render(TypingBuffer buffer, StrokeRange range, LayoutInfo layout, bool[]? slipped = null)
    {
        var strokes = buffer.Strokes;
        var converted = new char[range.Length];

        for (int i = 0; i < range.Length; i++)
        {
            int index = range.Start + i;
            var stroke = strokes[index];

            if (slipped is not null && slipped[index])
            {
                stroke = stroke with { Modifiers = stroke.Modifiers & ~ModifierKeys.CapsLock };
            }

            char resolved = layouts.ResolveCharacter(stroke, layout);
            converted[i] = resolved == '\0' ? stroke.Character : resolved;
        }

        return new string(converted);
    }

    /// <summary>
    /// Picks the layout the text reads best in, rather than simply the next one in the
    /// list. With two layouts installed this is the same thing; with three or more -- a
    /// Russian and a Ukrainian layout alongside English, say -- cycling would land on a
    /// layout the user did not mean.
    /// </summary>
    /// <param name="asTyped">The text as meant in the current layout, Caps Lock slip undone.</param>
    /// <param name="slipped">Strokes to replay without Caps Lock, as for <see cref="Render"/>.</param>
    public (LayoutInfo? Layout, double Score) ChooseTargetLayout(
        TypingBuffer buffer,
        StrokeRange range,
        LayoutInfo current,
        string asTyped,
        bool[]? slipped = null)
    {
        LayoutInfo? best = null;
        double bestScore = double.NegativeInfinity;
        int bestTurned = -1;

        foreach (var candidate in layouts.InstalledLayouts)
        {
            if (candidate.Handle == current.Handle)
            {
                continue;
            }

            string rendered = Render(buffer, range, candidate, slipped);

            // A layout that produces the very same text is not a candidate. Russian and
            // Ukrainian share almost every key, so most words look identical in both;
            // choosing one of those would leave the text untouched while silently switching
            // the keyboard to a language the user never asked for.
            if (string.Equals(rendered, asTyped, StringComparison.Ordinal))
            {
                continue;
            }

            double score = Plausibility(rendered, asTyped);
            int turned = WordDigits(asTyped, rendered).Count(static d => d);

            if (IsBetter(turned, score, bestTurned, bestScore))
            {
                bestScore = score;
                bestTurned = turned;
                best = candidate;
            }
        }

        return (best, bestScore);
    }

    /// <summary>
    /// Ranks one reading against the best so far: first by how many of the digits typed
    /// inside the word it turns into letters, then by how plausible it reads.
    ///
    /// Evidence before statistics. No language writes a digit inside a word, so when one
    /// layout has a letter on the key that typed it and another leaves the digit where it
    /// was, the first accounts for what was typed and the second does not, however well the
    /// letters around the digit read. Measured on the real layouts, Russian "в2лгош" beat
    /// Czech "děkuji" for "d2kuji", 0.64 to 0.30: nothing installed could judge Czech, and
    /// English marks down every háček. Layouts that keep their digits, as Russian and
    /// Ukrainian do, tie here and are ranked exactly as before.
    ///
    /// The price is a number glued to a word typed in the wrong layout, "5км" on the US
    /// keyboard, which goes to Czech when a Czech layout is installed. With a Czech
    /// dictionary it already did, the unknown word outscoring the digit.
    /// </summary>
    private static bool IsBetter(int turned, double score, int bestTurned, double bestScore) =>
        turned > bestTurned || (turned == bestTurned && score > bestScore);

    /// <summary>
    /// Marks the digits in <paramref name="text"/> that belong to a word: those stuck to
    /// letters, on keys where <paramref name="other"/> -- the same keys read in another
    /// layout -- has a letter. A letter there is one that layout types, so it is in that
    /// layout's own alphabet by definition.
    ///
    /// Czech, Slovak and Hungarian put accented letters on the number row, so "město" typed
    /// on the US layout arrives as "m2sto", and that 2 is a letter that came out wrong. Passed
    /// over like a space, as the scorer passes over every other digit, it flattered the word
    /// around it: "m2sto" was judged on m, s, t and o, with "st" and "to" the only pairs
    /// considered, and scored 0.90 as English -- better than most English words, and enough
    /// for the hotkey to refuse to make it "město".
    ///
    /// Only where the other reading has a letter. A digit both readings share says nothing
    /// about which of them is right, and counting it anyway marked both down alike: with
    /// Russian installed, that narrowed the gap protecting "mp3" from a stray double tap until
    /// it was gone. A number standing on its own is not part of any word either way.
    /// </summary>
    private static bool[] WordDigits(string text, string other)
    {
        var marked = new bool[text.Length];
        int start = 0;

        while (start < text.Length)
        {
            if (!char.IsLetterOrDigit(text[start]))
            {
                start++;
                continue;
            }

            int end = start;
            bool hasLetter = false;

            while (end < text.Length && char.IsLetterOrDigit(text[end]))
            {
                hasLetter |= char.IsLetter(text[end]);
                end++;
            }

            for (int i = start; hasLetter && i < end && i < other.Length; i++)
            {
                marked[i] = char.IsDigit(text[i]) && char.IsLetter(other[i]);
            }

            start = end;
        }

        return marked;
    }

    /// <summary>
    /// Finds the source-to-target pair that turns <paramref name="text"/> into the most
    /// plausible reading.
    ///
    /// Used for a selection, where there are no scan codes to replay -- only characters --
    /// so the layout the text was produced in has to be inferred. Trying every ordered pair
    /// also covers the user having switched layouts before reaching for the hotkey, which
    /// would make the active layout a misleading starting point.
    ///
    /// Text with no letters in it is converted too, unlike the word hotkey's; why is in
    /// <see cref="Build"/>.
    /// </summary>
    public LayoutMap? ChooseMapFor(string text)
    {
        var installed = layouts.InstalledLayouts;
        LayoutMap? best = null;
        double bestScore = double.NegativeInfinity;
        int bestTurned = -1;

        foreach (var source in installed)
        {
            foreach (var target in installed)
            {
                if (source.Handle == target.Handle)
                {
                    continue;
                }

                var map = layouts.GetMap(source, target);

                if (map.CoverageOf(text) < MinimumMapCoverage)
                {
                    continue;
                }

                string rendered = map.Convert(text);

                if (string.Equals(rendered, text, StringComparison.Ordinal))
                {
                    continue;
                }

                double score = Plausibility(rendered, text);
                int turned = WordDigits(text, rendered).Count(static d => d);

                if (IsBetter(turned, score, bestTurned, bestScore))
                {
                    bestScore = score;
                    bestTurned = turned;
                    best = map;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// What a selection typed with Caps Lock on by mistake should become: the same text with
    /// its capitals put right, and in another layout as well only when that reads clearly
    /// better. A plain selection is simply toggled, because converting it is all the user can
    /// have meant; here they may have meant either, and the text has to say which.
    /// </summary>
    /// <returns>The replacement, and the layout to switch to or null to keep the current one.</returns>
    public (string Text, LayoutInfo? Target) FixCapsLockSelection(string selected)
    {
        string intended = CapsLockSlip.Invert(selected);
        var map = ChooseMapFor(intended);

        if (map is null)
        {
            return (intended, null);
        }

        string converted = map.Convert(intended);

        if (languages.IsFamiliar(intended, validator)
            && Plausibility(intended, converted) - Plausibility(converted, intended) >= RefuseConversionMargin)
        {
            return (intended, null);
        }

        return (converted, map.Target);
    }

    /// <summary>
    /// How plausible <paramref name="text"/> reads, judged against <paramref name="other"/>,
    /// the same keys read in another layout: a digit counts as part of the word where the
    /// other reading has a letter. Both sides of a comparison are scored this way round, so
    /// neither gets its digits for free.
    /// </summary>
    private double Plausibility(string text, string other) =>
        languages.Evaluate(text, validator, WordDigits(text, other)).Score;
}
