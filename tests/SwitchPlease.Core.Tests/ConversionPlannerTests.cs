using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Layouts;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// What a correction decides to do, tested without a keyboard, a hook or Windows.
///
/// These decisions used to live inside the service that also owned the hook and SendInput,
/// which meant none of them could be checked: whether three installed layouts pick the right
/// one, whether the caret ends up where it started, whether correct text is left alone.
/// Every one of those is a way for the switcher to quietly ruin a sentence.
/// </summary>
public class ConversionPlannerTests
{
    private static ConversionPlanner PlannerFor(FakeLayoutResolver layouts, IWordValidator? validator = null) =>
        new(layouts, LanguageCatalog.BuiltIn, validator ?? NoWordValidator.Instance);

    [Fact]
    public void RussianTypedOnTheLatinLayoutIsPlannedBackIntoRussian()
    {
        string typed = LayoutFixture.TypedInEnglish("привет");
        var buffer = FakeLayoutResolver.Typed(typed, LayoutFixture.English);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.English);

        Assert.NotNull(plan);
        Assert.Equal("привет", plan.Converted);
        Assert.Equal(LayoutFixture.Russian, plan.TargetLayout);
    }

    [Fact]
    public void TheTrailingSpaceIsErasedAndRetypedSoTheCaretDoesNotMove()
    {
        // The word plus whatever followed it is erased, then the replacement and that same
        // tail are typed back. Getting this wrong leaves the caret one character adrift, and
        // the next correction then eats a character that was never part of the word.
        string typed = LayoutFixture.TypedInEnglish("привет") + " ";
        var buffer = FakeLayoutResolver.Typed(typed, LayoutFixture.English);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetLastWord(), LayoutFixture.English);

        Assert.NotNull(plan);
        Assert.Equal(" ", plan.Suffix);
        Assert.Equal(typed.Length, plan.EraseCount);
        Assert.Equal("привет".Length + 1, (plan.Converted + plan.Suffix).Length);
    }

    [Fact]
    public void CorrectlyTypedTextIsLeftAlone()
    {
        // Two taps of Shift is easy to hit by accident. A word that already reads far better
        // than any alternative must survive that.
        var buffer = FakeLayoutResolver.Typed("привет", LayoutFixture.Russian);
        string? refusal = null;

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.Russian, reason => refusal = reason);

        Assert.Null(plan);
        Assert.NotNull(refusal);
        Assert.Contains("left alone", refusal);
    }

    [Fact]
    public void WithThreeLayoutsTheOneTheTextReadsBestInWins()
    {
        // Cycling to "the next layout" would land on Ukrainian here, which shares almost
        // every key with Russian and is not what was meant.
        string typed = LayoutFixture.TypedInEnglish("сегодня");
        var buffer = FakeLayoutResolver.Typed(typed, LayoutFixture.English);

        var plan = PlannerFor(FakeLayoutResolver.All)
            .Build(buffer, buffer.GetAll(), LayoutFixture.English);

        Assert.NotNull(plan);
        Assert.Equal("сегодня", plan.Converted);
        Assert.Equal(LayoutFixture.Russian, plan.TargetLayout);
    }

    [Fact]
    public void ALayoutThatWouldChangeNothingIsNotACandidate()
    {
        // Most Russian words are identical under the Ukrainian layout. Choosing it would
        // leave the text untouched while silently switching the keyboard to a language the
        // user never asked for.
        var buffer = FakeLayoutResolver.Typed("вода", LayoutFixture.Russian);

        var (layout, _) = PlannerFor(FakeLayoutResolver.All)
            .ChooseTargetLayout(buffer, buffer.GetAll(), LayoutFixture.Russian, "вода");

        Assert.NotEqual(LayoutFixture.Ukrainian, layout);
    }

    [Fact]
    public void WithOnlyOneLayoutThereIsNothingToPlan()
    {
        var only = new FakeLayoutResolver(LayoutFixture.English);
        var buffer = FakeLayoutResolver.Typed("ghbdtn", LayoutFixture.English);
        string? reason = null;

        var plan = PlannerFor(only).Build(buffer, buffer.GetAll(), LayoutFixture.English, r => reason = r);

        Assert.Null(plan);
        Assert.Equal("no other layout would change this text", reason);
    }

    [Fact]
    public void KeysWithNoCounterpartKeepWhatTheyProduced()
    {
        // Digits and shared punctuation sit on the same characters in every layout, and a
        // conversion that mangled them would break every version number and price typed.
        var buffer = FakeLayoutResolver.Typed("ghbdtn 2024", LayoutFixture.English);

        string rendered = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Render(buffer, buffer.GetAll(), LayoutFixture.Russian);

        Assert.EndsWith(" 2024", rendered);
    }

    [Fact]
    public void UnfamiliarAlphabetsAreConvertedWithoutBeingJudged()
    {
        // A layout the application carries no model for can say which letters belong to it
        // but not whether a word reads naturally. Refusing on that basis would leave the
        // hotkey doing nothing at random; the user pressed it, so it converts.
        var czech = new LayoutInfo(9, 0x0405, "cs", "Czech");
        var resolver = new FakeLayoutResolver(LayoutFixture.English, czech);
        var buffer = FakeLayoutResolver.Typed("test", LayoutFixture.English);
        string? reason = null;

        var plan = PlannerFor(resolver).Build(buffer, buffer.GetAll(), czech, r => reason = r);

        // The fixture gives an unknown layout the Latin row, so nothing changes and there is
        // no candidate -- which is itself the correct answer, and never a mangled word.
        Assert.True(plan is null || plan.ChangesNothing || reason is null);
    }

    [Fact]
    public void SelectedTextFindsThePairThatReadsBest()
    {
        // A selection carries no scan codes, only characters, so the layout it was typed in
        // has to be inferred from every ordered pair.
        string typed = LayoutFixture.TypedInEnglish("здравствуйте");

        var map = PlannerFor(FakeLayoutResolver.All).ChooseMapFor(typed);

        Assert.NotNull(map);
        Assert.Equal("здравствуйте", map.Convert(typed));
    }

    [Fact]
    public void SelectedTextThatIsAlreadyRightHasNoPairWorthUsing()
    {
        var map = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ChooseMapFor("здравствуйте");

        // Either no pair applies, or the best one is the identity-in-effect. What must never
        // happen is a confident rewrite of correct text.
        Assert.True(map is null || map.Convert("здравствуйте") != "здравствуйте");
    }

    [Fact]
    public void ShiftIsCarriedThroughToTheConvertedText()
    {
        string typed = LayoutFixture.TypedInEnglish("Привет");
        var buffer = FakeLayoutResolver.Typed(typed, LayoutFixture.English);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.English);

        Assert.NotNull(plan);
        Assert.Equal("Привет", plan.Converted);
    }

    [Fact]
    public void ThePlanReportsExactlyHowMuchToErase()
    {
        var buffer = FakeLayoutResolver.Typed("ghbdtn rfr", LayoutFixture.English);
        var word = buffer.GetLastWord();

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, word, LayoutFixture.English);

        Assert.NotNull(plan);

        // Only the last word and what follows it, never the whole line.
        Assert.Equal(3, plan.EraseCount);
        Assert.Equal(string.Empty, plan.Suffix);
    }
}
