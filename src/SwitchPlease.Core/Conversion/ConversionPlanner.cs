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
        var (targetLayout, targetScore) = ChooseTargetLayout(buffer, range, activeLayout, original);

        if (targetLayout is null)
        {
            explain?.Invoke("no other layout would change this text");
            return null;
        }

        // Everything typed after the word (usually the space that ended it) is erased too,
        // then retyped unchanged, so the caret ends up exactly where it started.
        var tail = new StrokeRange(range.End, buffer.Count - range.End);
        string suffix = buffer.GetText(tail);

        var plan = new ConversionPlan(
            range, tail, original, Render(buffer, range, targetLayout), suffix, targetLayout);

        // Refuse to wreck text that is plainly already right. The hotkey is two taps of
        // Shift, which is easy to trigger by accident, and "convert anyway" would turn a
        // correct word into gibberish. Anything genuinely mistyped scores at or below its
        // alternative, so this only ever blocks the misfires.
        //
        // Text in an alphabet we do not model -- Czech, German, Turkish -- cannot be judged
        // this way at all, and pretending otherwise would refuse or mangle it at random.
        // There the hotkey behaves as a plain toggle, which is what the user asked for by
        // pressing it.
        if (!languages.IsFamiliar(original, validator))
        {
            explain?.Invoke("converting without judging it: unfamiliar alphabet");
            return plan;
        }

        double typedScore = Plausibility(original);

        if (typedScore - targetScore >= RefuseConversionMargin)
        {
            explain?.Invoke($"left alone: already reads better than any alternative "
                + $"({typedScore:F2} vs {targetScore:F2})");
            return null;
        }

        return plan;
    }

    /// <summary>
    /// Replays the recorded keys as if <paramref name="layout"/> had been active. Keys with
    /// no counterpart there keep whatever they originally produced.
    /// </summary>
    public string Render(TypingBuffer buffer, StrokeRange range, LayoutInfo layout)
    {
        var strokes = buffer.Strokes;
        var converted = new char[range.Length];

        for (int i = 0; i < range.Length; i++)
        {
            var stroke = strokes[range.Start + i];
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
    public (LayoutInfo? Layout, double Score) ChooseTargetLayout(
        TypingBuffer buffer,
        StrokeRange range,
        LayoutInfo current,
        string asTyped)
    {
        LayoutInfo? best = null;
        double bestScore = double.NegativeInfinity;

        foreach (var candidate in layouts.InstalledLayouts)
        {
            if (candidate.Handle == current.Handle)
            {
                continue;
            }

            string rendered = Render(buffer, range, candidate);

            // A layout that produces the very same text is not a candidate. Russian and
            // Ukrainian share almost every key, so most words look identical in both;
            // choosing one of those would leave the text untouched while silently switching
            // the keyboard to a language the user never asked for.
            if (string.Equals(rendered, asTyped, StringComparison.Ordinal))
            {
                continue;
            }

            double score = Plausibility(rendered);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return (best, bestScore);
    }

    /// <summary>
    /// Finds the source-to-target pair that turns <paramref name="text"/> into the most
    /// plausible reading.
    ///
    /// Used for a selection, where there are no scan codes to replay -- only characters --
    /// so the layout the text was produced in has to be inferred. Trying every ordered pair
    /// also covers the user having switched layouts before reaching for the hotkey, which
    /// would make the active layout a misleading starting point.
    /// </summary>
    public LayoutMap? ChooseMapFor(string text)
    {
        var installed = layouts.InstalledLayouts;
        LayoutMap? best = null;
        double bestScore = double.NegativeInfinity;

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

                double score = Plausibility(rendered);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = map;
                }
            }
        }

        return best;
    }

    private double Plausibility(string text) => languages.Evaluate(text, validator).Score;
}
