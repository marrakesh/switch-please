using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The hotkey declines to act when the text on screen already reads much better than any
/// alternative. That margin is what stops an accidental double-tap of Shift from turning a
/// correct word into gibberish, so it is measured here rather than guessed.
///
/// Measuring it also draws out a distinction that matters: Cyrillic-versus-Latin mistakes
/// separate cleanly, while Russian-versus-Ukrainian ones do not separate at all. See
/// <see cref="RussianAndUkrainianReadingsAreGenuinelyAmbiguous"/>.
/// </summary>
public class RefusalMarginTests(ITestOutputHelper output)
{
    /// <summary>The value the switcher ships with; kept in step with SwitcherService.</summary>
    private const double RefusalMargin = 0.20;

    /// <summary>
    /// Correct text whose only alternative reading is in the other alphabet. Pressing the
    /// hotkey on any of these is a misfire and must be declined.
    /// </summary>
    private static readonly string[] CorrectWords =
    [
        "спасибо", "привет", "работа", "сегодня", "вопрос", "машина", "будь", "дом", "мир",
        "город", "письмо", "деньги", "друг", "рука", "мама", "путь", "сила", "речь",
        "дякую", "ласка", "добре", "життя",
        "hello", "please", "value", "computer", "message", "friend", "code", "test", "data",
    ];

    /// <summary>Genuinely mistyped text: the hotkey must act on all of these.</summary>
    private static readonly string[] MistypedWords =
    [
        "ghbdtn", "cgfcb,j", "hf,jnf", "ctujlyz", "djghjc", "vfibyf", "ujhjl", "gbcmvj",
        "ltymub", "vfvf", "genm", "cbkf", "htxm", "lheu", "kfcrf", "lj,ht",
        "привыт", "маэш",
        "руддщ", "здуфыу", "мфдгу", "сщьзгеук", "ьуыыфпу", "акшутв", "сщву", "еуые",
    ];

    /// <summary>
    /// Words that differ between the Russian and Ukrainian layouts. Both readings are
    /// ordinary Cyrillic, so no margin can tell them apart.
    /// </summary>
    private static readonly string[] AmbiguousPairs = ["привіт", "місто", "їжа", "гроші", "вечір"];

    [Fact]
    public void CorrectTextSurvivesAnAccidentalPress()
    {
        var unprotected = CorrectWords.Where(w => MarginFor(w) < RefusalMargin).ToArray();

        Assert.True(
            unprotected.Length == 0,
            "a misfire would mangle: "
            + string.Join(", ", unprotected.Select(w => $"{w}={MarginFor(w):F2} -> {BestAlternative(w)}")));
    }

    [Fact]
    public void MistypedTextIsNeverRefused()
    {
        var refused = MistypedWords.Where(w => MarginFor(w) >= RefusalMargin).ToArray();

        Assert.True(
            refused.Length == 0,
            "the hotkey would decline to fix: "
            + string.Join(", ", refused.Select(w => $"{w}={MarginFor(w):F2}")));
    }

    [Fact]
    public void RussianAndUkrainianReadingsAreGenuinelyAmbiguous()
    {
        // Documents a limit rather than a defect. "місто" and "мысто" are both plausible
        // Cyrillic, so the switcher cannot judge which was meant and simply toggles between
        // them. Automatic correction stays out of this case entirely.
        foreach (string word in AmbiguousPairs)
        {
            double margin = MarginFor(word);
            output.WriteLine($"{word} vs {BestAlternative(word)}: {margin:F3}");

            Assert.True(
                Math.Abs(margin) < 0.6,
                $"\"{word}\" separates from \"{BestAlternative(word)}\" by {margin:F3}, "
                + "which would mean the model can judge this pair after all");
        }
    }

    [Fact]
    public void MarginTradeoffIsDocumented()
    {
        output.WriteLine("margin   correct protected   mistyped wrongly refused");

        foreach (double margin in (double[])[0.10, 0.15, 0.20, 0.25, 0.30, 0.40, 0.50])
        {
            int kept = CorrectWords.Count(w => MarginFor(w) >= margin);
            int refused = MistypedWords.Count(w => MarginFor(w) >= margin);

            output.WriteLine($"{margin:F2}     {kept,2}/{CorrectWords.Length}              {refused,2}/{MistypedWords.Length}");
        }

        output.WriteLine(string.Empty);
        output.WriteLine("tightest correct margins:");

        foreach (string word in CorrectWords.OrderBy(MarginFor).Take(6))
        {
            output.WriteLine($"   {MarginFor(word):F3}  {word} vs {BestAlternative(word)}");
        }
    }

    /// <summary>
    /// How much better the text reads as it stands than in the best other layout, mirroring
    /// what the switcher computes before deciding whether to act.
    /// </summary>
    private static double MarginFor(string text) =>
        LanguageCatalog.BuiltIn.Evaluate(text).Score - LanguageCatalog.BuiltIn.Evaluate(BestAlternative(text)).Score;

    private static string BestAlternative(string text)
    {
        string best = text;
        double bestScore = -1;

        foreach (string alternative in Alternatives(text))
        {
            // A layout that leaves the text unchanged is not an alternative at all.
            if (string.Equals(alternative, text, StringComparison.Ordinal))
            {
                continue;
            }

            double score = LanguageCatalog.BuiltIn.Evaluate(alternative).Score;

            if (score > bestScore)
            {
                bestScore = score;
                best = alternative;
            }
        }

        return best;
    }

    private static IEnumerable<string> Alternatives(string text)
    {
        yield return LayoutFixture.EnglishToRussian.Convert(text);
        yield return LayoutFixture.RussianToEnglish.Convert(text);
        yield return LayoutFixture.RussianToUkrainian.Convert(text);
        yield return LayoutFixture.UkrainianToRussian.Convert(text);
    }
}
