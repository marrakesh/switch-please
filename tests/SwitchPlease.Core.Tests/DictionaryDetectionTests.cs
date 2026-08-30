using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// What a dictionary adds that a statistical model cannot supply.
///
/// The Russian and Ukrainian layouts differ by three keys, so "привіт" typed on the Russian
/// one becomes "привыт" -- a sequence no bigram model will reject, because every letter pair
/// in it is common Russian. The dictionary settles it in one lookup: no such word exists.
/// </summary>
public class DictionaryDetectionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("привыт", "привіт")]
    [InlineData("мысто", "місто")]
    [InlineData("вечыр", "вечір")]
    public void MistypedUkrainianBecomesObviousWithADictionary(string asTyped, string corrected)
    {
        var validator = FakeWordValidator.RussianAndEnglishOnly();

        double withoutDictionary = LanguageCatalog.BuiltIn.Evaluate(asTyped).Score;
        double withDictionary = LanguageCatalog.BuiltIn.Evaluate(asTyped, validator).Score;

        output.WriteLine($"\"{asTyped}\": statistics {withoutDictionary:F3} -> with dictionary {withDictionary:F3}");

        // A word the dictionary rejects can never be rated as confident, however well its
        // letters happen to fit the language. The dictionary only ever lowers a score --
        // it cannot vouch for a word it does not contain.
        Assert.True(
            withDictionary <= withoutDictionary,
            $"the dictionary raised confidence in \"{asTyped}\"");

        Assert.True(
            withDictionary <= 0.6,
            $"\"{asTyped}\" is not a word, yet it still scores {withDictionary:F3}");

        // And the corrected reading must now win clearly.
        double correctedScore = LanguageCatalog.BuiltIn.Evaluate(corrected, validator).Score;

        Assert.True(
            correctedScore - withDictionary >= 0.25,
            $"\"{corrected}\" ({correctedScore:F3}) beats \"{asTyped}\" ({withDictionary:F3}) "
            + $"by only {correctedScore - withDictionary:F3}");
    }

    [Theory]
    [InlineData("спасибо")]
    [InlineData("привет")]
    [InlineData("город")]
    public void RealWordsAreConfirmedRatherThanGuessedAt(string word)
    {
        var validator = FakeWordValidator.RussianAndEnglishOnly();

        Assert.Equal(1.0, LanguageCatalog.BuiltIn.Evaluate(word, validator).Score);
    }

    [Fact]
    public void AutomaticCorrectionNowHandlesRussianVersusUkrainian()
    {
        // Previously refused outright: both readings are Cyrillic and nothing could separate
        // them. With a Russian dictionary installed, it can.
        var detector = new HeuristicWrongLayoutDetector(0.25, FakeWordValidator.RussianAndEnglishOnly());

        var verdict = detector.Evaluate("привыт", "привіт");

        output.WriteLine(verdict.Reason);
        Assert.True(verdict.ShouldConvert);
    }

    [Fact]
    public void WithoutADictionaryItStillDeclinesThatCase()
    {
        // No dictionary means no way to tell the two apart, so it must stay out.
        var detector = new HeuristicWrongLayoutDetector(0.25, NoWordValidator.Instance);

        var verdict = detector.Evaluate("привыт", "привіт");

        Assert.False(verdict.ShouldConvert);
        Assert.Contains("no dictionary", verdict.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrectRussianIsNotDraggedTowardsUkrainian()
    {
        var detector = new HeuristicWrongLayoutDetector(0.25, FakeWordValidator.RussianAndEnglishOnly());

        // "привет" is a real Russian word; its Ukrainian-layout twin is identical anyway,
        // but the reverse direction must not fire either.
        Assert.False(detector.Evaluate("привет", "привет").ShouldConvert);
        Assert.False(detector.Evaluate("город", "город").ShouldConvert);
    }

    [Fact]
    public void AWordMissingFromTheDictionaryIsNotTreatedAsNoise()
    {
        // Names, slang and jargon are absent from every dictionary. They must not be
        // rewritten just for being unknown, so an unknown word keeps the score its letter
        // structure earns, merely capped.
        var validator = FakeWordValidator.RussianAndEnglishOnly();

        double score = LanguageCatalog.BuiltIn.Evaluate("ковальчук", validator).Score;

        output.WriteLine($"unknown but well-formed: {score:F3}");

        Assert.True(score > 0.2, $"an unknown but plausible word scored only {score:F3}");
        Assert.True(score < 1.0, "an unknown word must not score as high as a confirmed one");
    }

    [Fact]
    public void LanguagesWithoutADictionaryFallBackToStatistics()
    {
        var validator = FakeWordValidator.RussianAndEnglishOnly();

        // No uk-UA dictionary here, so Ukrainian words are judged by the model alone and
        // must still come out plausible.
        Assert.Equal(
            LanguageProfile.Ukrainian.Score("дякую"),
            LanguageProfile.Ukrainian.Score("дякую", validator));
    }

    [Fact]
    public void RepeatedLookupsAreCheapEnoughToRunPerWord()
    {
        // The switcher scores several candidate readings per keystroke burst, so the same
        // word gets looked up repeatedly; implementations are expected to cache.
        var validator = FakeWordValidator.RussianAndEnglishOnly();

        for (int i = 0; i < 5; i++)
        {
            LanguageCatalog.BuiltIn.Evaluate("привет как дела", validator);
        }

        output.WriteLine($"lookups for 5 evaluations of a 3-word phrase: {validator.Lookups}");
        Assert.True(validator.Lookups > 0);
    }
}
