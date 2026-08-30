using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Russian and Ukrainian keyboards differ by three keys, so text typed in the wrong one of
/// the two comes out as perfectly ordinary-looking Cyrillic rather than obvious noise. The
/// scoring has to separate them anyway, or the hotkey converts to the wrong alphabet.
/// </summary>
public class CyrillicLayoutTests(ITestOutputHelper output)
{
    /// <summary>Mirrors the margin the switcher uses before it declines to touch a word.</summary>
    private const double RefusalMargin = 0.20;

    [Theory]
    [InlineData("привіт", "привыт")]
    [InlineData("маєш", "маэш")]
    [InlineData("привіт як ся маєш", "привыт як ся маэш")]
    [InlineData("їжа", "ъжа")]
    public void RussianLayoutMangledUkrainian(string ukrainian, string expected)
    {
        Assert.Equal(expected, LayoutFixture.TypedWithRussianLayout(ukrainian));
    }

    [Theory]
    [InlineData("привіт")]
    [InlineData("маєш")]
    [InlineData("привіт як ся маєш")]
    public void UkrainianReadingBeatsTheRussianLayoutOne(string ukrainian)
    {
        string asTyped = LayoutFixture.TypedWithRussianLayout(ukrainian);

        double typedScore = LanguageCatalog.BuiltIn.Evaluate(asTyped).Score;
        double fixedScore = LanguageCatalog.BuiltIn.Evaluate(ukrainian).Score;

        output.WriteLine($"\"{asTyped}\" = {typedScore:F3}   \"{ukrainian}\" = {fixedScore:F3}");

        Assert.True(
            fixedScore > typedScore,
            $"\"{ukrainian}\" ({fixedScore:F3}) should read better than \"{asTyped}\" ({typedScore:F3})");
    }

    [Theory]
    [InlineData("привіт", "uk")]
    [InlineData("дякую", "uk")]
    [InlineData("їжак", "uk")]
    [InlineData("спасибо", "ru")]
    [InlineData("привет", "ru")]
    [InlineData("hello", "en")]
    public void TextIsJudgedAgainstTheRightLanguage(string text, string expectedLanguage)
    {
        var (profile, _) = LanguageCatalog.BuiltIn.Evaluate(text);

        Assert.Equal(expectedLanguage, profile?.Name);
    }

    [Theory]
    [InlineData("спасибо")]
    [InlineData("привет")]
    [InlineData("дякую")]
    [InlineData("будь")]
    public void CorrectCyrillicIsFarBetterThanItsLatinTransliteration(string correct)
    {
        // This margin is what stops an accidental double-tap from turning a good word into
        // gibberish, so it is worth pinning down rather than leaving to chance.
        string latin = LayoutFixture.TypedInEnglish(correct);

        double correctScore = LanguageCatalog.BuiltIn.Evaluate(correct).Score;
        double latinScore = LanguageCatalog.BuiltIn.Evaluate(latin).Score;

        output.WriteLine($"\"{correct}\" = {correctScore:F3}   \"{latin}\" = {latinScore:F3}   margin {correctScore - latinScore:F3}");

        Assert.True(
            correctScore - latinScore >= RefusalMargin,
            $"\"{correct}\" beats \"{latin}\" by only {correctScore - latinScore:F3}, "
            + $"below the {RefusalMargin} the switcher needs to leave it alone");
    }

    [Theory]
    [InlineData("привыт")]
    [InlineData("маэш")]
    public void MistypedUkrainianIsNotProtectedByThatMargin(string asTyped)
    {
        // The mirror of the test above: text that really is wrong must not look good enough
        // to be left alone.
        string corrected = LayoutFixture.RussianToUkrainian.Convert(asTyped);

        double typedScore = LanguageCatalog.BuiltIn.Evaluate(asTyped).Score;
        double correctedScore = LanguageCatalog.BuiltIn.Evaluate(corrected).Score;

        Assert.True(
            typedScore - correctedScore < RefusalMargin,
            $"\"{asTyped}\" would be left alone; it scores {typedScore:F3} against {correctedScore:F3}");
    }

    [Fact]
    public void AutomaticCorrectionStaysOutOfRussianVersusUkrainian()
    {
        // Both readings are valid Cyrillic and the model cannot separate them reliably, so
        // automatic rewriting must decline. The hotkey still handles this case.
        var detector = new HeuristicWrongLayoutDetector();

        var verdict = detector.Evaluate("привыт", "привіт");

        Assert.False(verdict.ShouldConvert);
        Assert.Contains("same script", verdict.Reason, StringComparison.Ordinal);
    }
}
