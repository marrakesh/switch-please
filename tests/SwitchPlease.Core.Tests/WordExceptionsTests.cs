using SwitchPlease.Core.Config;
using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The words automatic correction has been told to leave alone.
///
/// The failure worth guarding against is a list that stops matching: the user undoes a
/// correction, sees the notice saying the word is remembered, and the next day it is
/// corrected again because it arrived with a capital or a comma after it.
/// </summary>
public class WordExceptionsTests
{
    [Theory]
    [InlineData("ntcn", "ntcn")]
    [InlineData("Ntcn", "ntcn")]
    [InlineData("ntcn,", "ntcn")]
    [InlineData("«Привыт»", "привыт")]
    [InlineData("...", "")]
    public void WordsAreKeptInLowerCaseWithoutThePunctuationRoundThem(string typed, string kept)
    {
        Assert.Equal(kept, WordExceptions.Normalize(typed));
    }

    [Theory]
    [InlineData("ntcn")]
    [InlineData("NTCN")]
    [InlineData("ntcn.")]
    public void AWordMatchesHoweverItArrives(string typed)
    {
        var exceptions = new WordExceptions(["Ntcn"]);

        Assert.True(exceptions.Contains(typed));
    }

    [Fact]
    public void OtherWordsAreNotCaughtByIt()
    {
        var exceptions = new WordExceptions(["ntcn"]);

        Assert.False(exceptions.Contains("ntcnf"));
        Assert.False(exceptions.Contains(","));
    }

    [Fact]
    public void AddingAWordLeavesTheOriginalAsItWas()
    {
        var before = new WordExceptions(["one"]);
        var after = before.With("two");

        Assert.False(before.Contains("two"));
        Assert.True(after.Contains("two"));
        Assert.True(after.Contains("one"));
    }

    [Fact]
    public void RememberingAWordStoresItOnceInTheFormItIsComparedIn()
    {
        var settings = new AppSettings();

        Assert.True(settings.RememberNeverCorrect("Ntcn,"));
        Assert.False(settings.RememberNeverCorrect("ntcn"));
        Assert.False(settings.RememberNeverCorrect("?!"));

        Assert.Equal(["ntcn"], settings.NeverCorrectWords);
    }

    [Fact]
    public void AnAutomaticCorrectionIsMarkedSoUndoingItCanBeLearnedFrom()
    {
        var buffer = FakeLayoutResolver.Typed("ghbdtn ", LayoutFixture.English);
        var planner = new ConversionPlanner(
            FakeLayoutResolver.EnglishAndRussian, LanguageCatalog.BuiltIn, NoWordValidator.Instance);

        var plan = planner.Build(buffer, buffer.GetLastWord(), LayoutFixture.English);

        Assert.NotNull(plan);

        var undo = CorrectionUndo.ForWord(plan) with { Automatic = true };

        Assert.True(undo.Automatic);
        Assert.Equal("ghbdtn", undo.Original);
        Assert.False(CorrectionUndo.ForWord(plan).Automatic);
    }
}
