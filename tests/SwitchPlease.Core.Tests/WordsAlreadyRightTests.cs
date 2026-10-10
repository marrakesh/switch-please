using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// A word that was typed right is left alone when the line or the selection around it is
/// converted.
///
/// "рудщщ рщц фку ты" is "heloo how are" typed on the Russian keyboard, followed by a Russian
/// word that was typed right. Ctrl ×2 chose English for the line and took every word with it,
/// so "ты" came out as "ns".
/// </summary>
public class WordsAlreadyRightTests
{
    private static ConversionPlanner PlannerFor(FakeLayoutResolver layouts, IWordValidator? validator = null) =>
        new(layouts, LanguageCatalog.BuiltIn, validator ?? NoWordValidator.Instance);

    [Fact]
    public void ASelectionKeepsTheWordThatWasRight()
    {
        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection("рудщщ рщц фку ты");

        Assert.NotNull(result);
        Assert.Equal("heloo how are ты", result.Value.Text);
        Assert.Equal(LayoutFixture.English, result.Value.Target);
    }

    [Fact]
    public void ALineKeepsTheWordThatWasRight()
    {
        string typed = LayoutFixture.TypedInRussian("hello how are") + " ты";
        var buffer = FakeLayoutResolver.Typed(typed, LayoutFixture.Russian);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.Russian);

        Assert.NotNull(plan);
        Assert.Equal("hello how are ты", plan.Converted);
        Assert.Equal(LayoutFixture.English, plan.TargetLayout);
    }

    [Fact]
    public void AnEnglishWordTypedRightIsKeptToo()
    {
        string typed = "hello " + LayoutFixture.TypedInEnglish("привет друг");

        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection(typed);

        Assert.NotNull(result);
        Assert.Equal("hello привет друг", result.Value.Text);
    }

    [Fact]
    public void PunctuationAfterTheWordDoesNotHideIt()
    {
        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection("рщц фку ты, ьн акшутв");

        Assert.NotNull(result);
        Assert.Equal("how are ты, my friend", result.Value.Text);
    }

    [Fact]
    public void AWordInBothDictionariesGoesWithTheLine()
    {
        // "next" typed on the Russian keyboard is "туче", which is Russian too. Only a word
        // that is nothing once converted was plainly typed right.
        var validator = new FakeWordValidator()
            .With("ru-RU", "туче", "ты")
            .With("en-US", "next", "week", "see", "you");
        string typed = LayoutFixture.TypedInRussian("see you next week");

        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian, validator).ConvertSelection(typed);

        Assert.NotNull(result);
        Assert.Equal("see you next week", result.Value.Text);
    }

    [Fact]
    public void ASingleSelectedWordIsConvertedAsAsked()
    {
        // Selecting one word and pressing the hotkey is deliberate, however right the word
        // looks.
        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection("ты");

        Assert.Equal(("ns", LayoutFixture.English), result);
    }

    [Fact]
    public void NoWordOfAMistypedSentenceIsKept()
    {
        // The other side of the trade: a line typed wholly in the wrong layout must come back
        // whole, short and common words included.
        var planner = PlannerFor(FakeLayoutResolver.EnglishAndRussian);
        var cases = DetectionCorpus.RussianSentences
            .Select(s => (Meant: s, Typed: LayoutFixture.TypedInEnglish(s), Layout: LayoutFixture.English))
            .Concat(DetectionCorpus.EnglishSentences
                .Select(s => (Meant: s, Typed: LayoutFixture.TypedInRussian(s), Layout: LayoutFixture.Russian)));

        var wrong = new List<string>();

        foreach (var (meant, typed, layout) in cases)
        {
            string? selection = planner.ConvertSelection(typed)?.Text;
            var buffer = FakeLayoutResolver.Typed(typed, layout);
            string? line = planner.Build(buffer, buffer.GetAll(), layout)?.Converted;

            if (selection != meant)
            {
                wrong.Add($"selection: {selection}");
            }

            if (line != meant)
            {
                wrong.Add($"line: {line}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }
}
