using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// A word does not arrive alone.
///
/// The detector used to judge each word entirely on its own letters, which throws away the
/// most obvious evidence there is: after three Russian words the fourth is very likely
/// Russian too. These tests pin down both directions of that -- a correction into the
/// language being written becomes slightly easier, and rewriting a word that already matches
/// its surroundings becomes slightly harder -- and, just as importantly, that the effect
/// stays small enough never to overrule the letters themselves.
/// </summary>
public class ContextDetectionTests(ITestOutputHelper output)
{
    private static HeuristicWrongLayoutDetector Detector(double margin = 0.25) => new(margin);

    [Fact]
    public void ContextInTheSameLanguageMakesTheCorrectionEasier()
    {
        string typed = LayoutFixture.TypedInEnglish("сегодня");

        var without = Detector().Evaluate(typed, "сегодня");
        var with = Detector().Evaluate(typed, "сегодня", "привет как дела ");

        output.WriteLine($"without: {without.Reason}");
        output.WriteLine($"with:    {with.Reason}");

        Assert.True(with.Confidence >= without.Confidence);
    }

    [Fact]
    public void ContextIsNamedInTheExplanationSoAMisfireCanBeTracedToIt()
    {
        var verdict = Detector().Evaluate(LayoutFixture.TypedInEnglish("сегодня"), "сегодня", "привет как дела ");

        Assert.Contains("after=", verdict.Reason);
    }

    [Fact]
    public void TooLittleContextIsIgnoredRatherThanGuessedFrom()
    {
        // One short word is as likely to be a stray as it is to be evidence.
        var verdict = Detector().Evaluate(LayoutFixture.TypedInEnglish("сегодня"), "сегодня", "уж ");

        Assert.DoesNotContain("after=", verdict.Reason);
    }

    [Fact]
    public void ContextThatIsItselfGibberishIsNotBelieved()
    {
        // Text that scores poorly in every language is most likely mistyped itself. Taking
        // its word for what comes next would let one wrong guess drag the whole line after
        // it.
        var verdict = Detector().Evaluate(
            LayoutFixture.TypedInEnglish("сегодня"), "сегодня", "qwrtpsdfgh zxcvbnm ");

        Assert.DoesNotContain("after=", verdict.Reason);
    }

    [Fact]
    public void ContextCannotOnItsOwnTurnACorrectWordIntoGibberish()
    {
        // The whole risk of using context: an English word inside a Russian sentence must
        // still survive. The nudge is deliberately far too small to carry a verdict on its
        // own.
        foreach (string word in (string[])["github", "download", "software", "keyboard"])
        {
            var verdict = Detector().Evaluate(word, LayoutFixture.TypedInRussian(word), "привет как дела вот ");

            output.WriteLine($"{word}: {verdict.ShouldConvert} [{verdict.Reason}]");
            Assert.False(verdict.ShouldConvert, $"\"{word}\" was rewritten because of its surroundings");
        }
    }

    [Fact]
    public void NoContextBehavesExactlyAsBefore()
    {
        // Every existing decision has to be unchanged when there is nothing to go on, which
        // is the case for the first word of every line.
        string typed = LayoutFixture.TypedInEnglish("привет");

        var withNull = Detector().Evaluate(typed, "привет");
        var withEmpty = Detector().Evaluate(typed, "привет", string.Empty);
        var withSpaces = Detector().Evaluate(typed, "привет", "   ");

        Assert.Equal(withNull.ShouldConvert, withEmpty.ShouldConvert);
        Assert.Equal(withNull.Reason, withEmpty.Reason);
        Assert.Equal(withNull.Reason, withSpaces.Reason);
    }

    [Fact]
    public void AWholeLineInTheWrongLayoutIsRecognisedFromItsOwnBeginning()
    {
        // The commonest mistake there is: noticing halfway through a sentence that the whole
        // thing went in wrong. What came before is gibberish as it stands and perfectly
        // ordinary once converted, which is strong evidence about the word that follows.
        string[] words = ["завтра", "утром", "будет", "совещание"];
        string typed = string.Join(' ', words.Select(LayoutFixture.TypedInEnglish));
        string context = typed[..typed.LastIndexOf(' ')];

        var blind = Detector().Evaluate(LayoutFixture.TypedInEnglish("совещание"), "совещание");
        var informed = Detector().Evaluate(
            LayoutFixture.TypedInEnglish("совещание"),
            "совещание",
            context,
            "завтра утром будет");

        output.WriteLine($"blind:    {blind.Reason}");
        output.WriteLine($"informed: {informed.Reason}");

        // Which of the two Cyrillic languages it settles on is not the point and cannot be
        // pinned down without a dictionary -- their models score ordinary Russian within a
        // hair of each other. What matters is that the context was read as Cyrillic at all.
        Assert.True(IsCyrillicContext(informed.Reason), informed.Reason);
        Assert.True(informed.Confidence > blind.Confidence);
    }

    [Fact]
    public void OrdinaryTextIsReadAsItStandsRatherThanAsItWouldConvert()
    {
        // Correct text converts to gibberish, and that gibberish must never get a vote. The
        // as-typed reading is tried first for exactly this reason.
        var verdict = Detector().Evaluate(
            "привет",
            LayoutFixture.TypedInEnglish("привет"),
            "сегодня хорошая погода",
            LayoutFixture.TypedInEnglish("сегодня хорошая погода"));

        // Had the converted reading won, the context would have come back as English and
        // pushed towards rewriting a perfectly good Russian word.
        Assert.True(IsCyrillicContext(verdict.Reason), verdict.Reason);
        Assert.False(verdict.ShouldConvert);
    }

    /// <summary>Whether the detector read the surrounding text as one of the Cyrillic languages.</summary>
    private static bool IsCyrillicContext(string reason) =>
        reason.Contains("after=ru") || reason.Contains("after=uk");

    [Fact]
    public void AShorterMinimumLengthLetsShortWordsThroughTheGuard()
    {
        // The guard is configurable now; at the default, three letters is the floor.
        string typed = LayoutFixture.TypedInEnglish("ещё");

        Assert.Contains("too short", new HeuristicWrongLayoutDetector(0.25, minimumWordLength: 4)
            .Evaluate(typed, "ещё").Reason);

        Assert.DoesNotContain("too short", new HeuristicWrongLayoutDetector(0.25, minimumWordLength: 3)
            .Evaluate(typed, "ещё").Reason);
    }
}
