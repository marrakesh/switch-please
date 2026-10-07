using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Caps Lock left on by mistake: "пРИВЕТ" for "Привет".
///
/// The cost of getting this wrong is the same as for a wrong layout, and so is the bar: a
/// word typed in capitals on purpose must come through untouched, however often the slip
/// itself goes uncorrected.
/// </summary>
public class CapsLockSlipTests
{
    private static ConversionPlanner PlannerFor(FakeLayoutResolver layouts) =>
        new(layouts, LanguageCatalog.BuiltIn, NoWordValidator.Instance);

    [Fact]
    public void ShiftHeldWithCapsLockOnMarksTheStretchAsASlip()
    {
        var buffer = FakeLayoutResolver.Typed("пРИВЕТ", LayoutFixture.Russian, capsLock: true);

        var slipped = CapsLockSlip.Find(buffer.Strokes, buffer.GetAll());

        Assert.NotNull(slipped);
        Assert.All(slipped, Assert.True);
    }

    [Fact]
    public void CapitalsTypedOnPurposeAreNotASlip()
    {
        // Caps Lock switched on to write capitals, and no Shift anywhere: that is what it is for.
        var buffer = FakeLayoutResolver.Typed("NASA", LayoutFixture.English, capsLock: true);

        Assert.Null(CapsLockSlip.Find(buffer.Strokes, buffer.GetAll()));
    }

    [Fact]
    public void TheShiftInTheFirstWordSpeaksForTheRestOfTheLine()
    {
        // Only "пРИВЕТ" had Shift in it. "ДЕЛА", corrected on its own when its space is typed,
        // has to be recognised as part of the same slip, or it would stay in capitals.
        var buffer = FakeLayoutResolver.Typed("пРИВЕТ КАК ДЕЛА", LayoutFixture.Russian, capsLock: true);

        Assert.NotNull(CapsLockSlip.Find(buffer.Strokes, buffer.GetLastWord()));
    }

    [Fact]
    public void ASlipThatEndedEarlierDoesNotReachLaterWords()
    {
        // Caps Lock was switched off in between, so the later word is ordinary typing.
        var buffer = FakeLayoutResolver.Typed("hELLO ", LayoutFixture.English, capsLock: true);
        FakeLayoutResolver.Append(buffer, "world", LayoutFixture.English);

        Assert.Null(CapsLockSlip.Find(buffer.Strokes, buffer.GetLastWord()));
    }

    [Theory]
    [InlineData("пРИВЕТ", true)]
    [InlineData("hELLO", true)]
    [InlineData("пРИВЕТ КАК ДЕЛА", true)]
    [InlineData("пРИВЕТ, КАК ДЕЛА?", true)]
    [InlineData("Привет", false)]
    [InlineData("привет", false)]
    [InlineData("NASA", false)]
    [InlineData("пРИВЕТ, как дела", false)]
    [InlineData("Ok", false)]

    // Fits the pattern, and is why a selection is only ever judged this way while Caps Lock
    // is actually on.
    [InlineData("mRNA", true)]
    public void TextAloneIsReadAsInvertedOnlyWhenEveryWordFitsThePattern(string text, bool expected)
    {
        Assert.Equal(expected, CapsLockSlip.LooksInverted(text));
    }

    [Fact]
    public void InvertingSwapsTheCaseOfLettersAndNothingElse()
    {
        Assert.Equal("Привет, как дела? 42", CapsLockSlip.Invert("пРИВЕТ, КАК ДЕЛА? 42"));
    }

    [Fact]
    public void TheRightLayoutWithCapsLockOnHasOnlyItsCaseCorrected()
    {
        var buffer = FakeLayoutResolver.Typed("пРИВЕТ", LayoutFixture.Russian, capsLock: true);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.Russian);

        Assert.NotNull(plan);
        Assert.Equal("Привет", plan.Converted);
        Assert.False(plan.SwitchesLayout);
        Assert.True(plan.FixesCapsLock);
    }

    [Fact]
    public void TheWrongLayoutWithCapsLockOnIsCorrectedInBothRespects()
    {
        // The Russian word "Привет", with Caps Lock on and the US layout active.
        var buffer = FakeLayoutResolver.Typed("gHBDTN", LayoutFixture.English, capsLock: true);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.English);

        Assert.NotNull(plan);
        Assert.Equal("Привет", plan.Converted);
        Assert.Equal("Ghbdtn", plan.Intended);
        Assert.Equal(LayoutFixture.Russian, plan.TargetLayout);
        Assert.True(plan.SwitchesLayout);
        Assert.True(plan.FixesCapsLock);
    }

    [Fact]
    public void AWordWithoutShiftInsideASlipIsCorrectedToo()
    {
        var buffer = FakeLayoutResolver.Typed("пРИВЕТ ДРУГ", LayoutFixture.Russian, capsLock: true);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetLastWord(), LayoutFixture.Russian);

        Assert.NotNull(plan);
        Assert.Equal("друг", plan.Converted);
        Assert.False(plan.SwitchesLayout);
    }

    [Fact]
    public void CapitalsTypedOnPurposeInTheRightLayoutAreLeftAlone()
    {
        var buffer = FakeLayoutResolver.Typed("ПРИВЕТ", LayoutFixture.Russian, capsLock: true);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.Russian);

        Assert.Null(plan);
    }

    [Fact]
    public void ASelectionInTheRightLayoutKeepsItsLayout()
    {
        var (text, target) = PlannerFor(FakeLayoutResolver.EnglishAndRussian).FixCapsLockSelection("пРИВЕТ");

        Assert.Equal("Привет", text);
        Assert.Null(target);
    }

    [Fact]
    public void ASelectionInTheWrongLayoutIsConvertedAsWell()
    {
        var (text, target) = PlannerFor(FakeLayoutResolver.EnglishAndRussian).FixCapsLockSelection("gHBDTN");

        Assert.Equal("Привет", text);
        Assert.Equal(LayoutFixture.Russian, target);
    }

    [Fact]
    public void UndoingACapsLockFixPutsTheOriginalBack()
    {
        var buffer = FakeLayoutResolver.Typed("пРИВЕТ ", LayoutFixture.Russian, capsLock: true);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetLastWord(), LayoutFixture.Russian);

        Assert.NotNull(plan);

        var undo = CorrectionUndo.ForWord(plan);

        Assert.Equal("Привет ", undo.Wrote);
        Assert.Equal("пРИВЕТ ", undo.Restore);
    }
}
