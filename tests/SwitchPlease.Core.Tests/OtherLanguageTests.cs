using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// How the switcher behaves for languages it has no model of.
///
/// Converting characters never needed language knowledge: the tables come from Windows, so
/// any pair of installed layouts already works. Judging text does need it, and the danger
/// is not that judgement is unavailable but that it is confidently wrong -- the English
/// profile rated Czech "příliš" at 0.05, which invites a rewrite of a perfectly good word.
/// </summary>
public class OtherLanguageTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("příliš")]
    [InlineData("žluťoučký")]
    [InlineData("Grüße")]
    [InlineData("schön")]
    [InlineData("größer")]
    [InlineData("çalışma")]
    [InlineData("pozdrówka")]
    public void AccentedLatinTextIsRecognisedAsLatin(string word)
    {
        // Before this, accented letters counted as "other", which distorted every judgement
        // built on top of the script.
        Assert.Equal(Script.Latin, TextGuards.DominantScript(word));
    }

    [Theory]
    [InlineData("příliš")]
    [InlineData("žluťoučký")]
    [InlineData("Grüße")]
    [InlineData("çalışma")]
    public void TextInAnUnmodelledAlphabetIsNotJudged(string word)
    {
        Assert.False(
            LanguageCatalog.BuiltIn.IsFamiliar(word),
            $"\"{word}\" is not English, Russian or Ukrainian and must not be scored as if it were");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("computer")]
    [InlineData("привет")]
    [InlineData("привіт")]
    public void TextWeDoModelIsStillJudged(string word)
    {
        Assert.True(LanguageCatalog.BuiltIn.IsFamiliar(word));
    }

    [Theory]
    [InlineData("příliš")]
    [InlineData("Grüße")]
    [InlineData("çalışma")]
    public void AutomaticCorrectionLeavesUnmodelledLanguagesAlone(string word)
    {
        // The important guarantee: a German or Czech user must never have correct text
        // rewritten just because no profile exists for their language.
        var detector = new HeuristicWrongLayoutDetector(0.25, FakeWordValidator.RussianAndEnglishOnly());

        var verdict = detector.Evaluate(word, LayoutFixture.EnglishToRussian.Convert(word));

        output.WriteLine($"{word}: {verdict.Reason}");
        Assert.False(verdict.ShouldConvert);
    }

    [Fact]
    public void MixedScriptDetectionStillWorksWithAccentedLetters()
    {
        // "Grüße" is entirely Latin despite the umlaut, so it is not mixed script.
        Assert.False(TextGuards.IsMixedScript("Grüße"));

        // Genuine mixing must still be caught.
        Assert.True(TextGuards.IsMixedScript("приvet"));
    }
}
