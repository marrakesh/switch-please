using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Undoing a correction, and the rule that makes the hotkey toggle.
///
/// The toggle was missing and nothing caught it, because everything involved was working as
/// designed: the correction produced a real word, and the refusal margin then protected that
/// word from being rewritten -- which is exactly its job, and exactly the wrong answer when
/// what is being asked for is "put back what you just did".
///
/// The dangerous half is the other direction. This decides whether a hotkey press erases
/// characters and types different ones, so a predicate that says yes when it should say no
/// deletes text the user typed.
/// </summary>
public class CorrectionUndoTests
{
    private static ConversionPlanner Planner() =>
        new(FakeLayoutResolver.EnglishAndRussian, LanguageCatalog.BuiltIn, NoWordValidator.Instance);

    /// <summary>The state after "ghbdtn" was typed in Latin and corrected to "привет".</summary>
    private static (TypingBuffer Buffer, CorrectionUndo Undo) AfterCorrecting(string typed, string suffix = "")
    {
        var buffer = FakeLayoutResolver.Typed(typed + suffix, LayoutFixture.English);
        var range = buffer.GetLastWord();

        var plan = Planner().Build(buffer, range, LayoutFixture.English);
        Assert.NotNull(plan);

        // What the service does after sending the correction: the buffer is brought in line
        // with the screen, keeping the scan codes.
        var strokes = buffer.Strokes;

        for (int i = 0; i < plan.Word.Length; i++)
        {
            int index = plan.Word.Start + i;
            buffer.Replace(index, strokes[index] with { Character = plan.Converted[i] });
        }

        return (buffer, CorrectionUndo.ForWord(plan));
    }

    [Fact]
    public void PressingTheHotkeyAgainOnTheCorrectedWordUndoesIt()
    {
        var (buffer, undo) = AfterCorrecting(LayoutFixture.TypedInEnglish("привет"));

        Assert.True(undo.Reverses(buffer, buffer.GetLastWord()));
    }

    [Fact]
    public void UndoingRestoresBothTheScreenTextAndTheRecord()
    {
        string typed = LayoutFixture.TypedInEnglish("привет");
        var (buffer, undo) = AfterCorrecting(typed);

        Assert.Equal("привет", buffer.GetText(buffer.GetLastWord()));
        Assert.Equal("привет", undo.Wrote);
        Assert.Equal(typed, undo.Restore);

        undo.RestoreBuffer(buffer);

        Assert.Equal(typed, buffer.GetText(buffer.GetLastWord()));
    }

    [Fact]
    public void TheScanCodesSurviveAnUndoSoTheWordCanBeConvertedAgain()
    {
        // Undo puts the characters back but must not touch which keys were pressed;
        // otherwise the word could be corrected once and never again.
        string typed = LayoutFixture.TypedInEnglish("привет");
        var (buffer, undo) = AfterCorrecting(typed);

        var before = buffer.Strokes.Select(stroke => stroke.ScanCode).ToArray();
        undo.RestoreBuffer(buffer);

        Assert.Equal(before, buffer.Strokes.Select(stroke => stroke.ScanCode));

        var again = Planner().Build(buffer, buffer.GetLastWord(), LayoutFixture.English);

        Assert.NotNull(again);
        Assert.Equal("привет", again.Converted);
    }

    [Fact]
    public void TypingAfterTheCorrectionMakesTheNextPressAnOrdinaryConversion()
    {
        // The danger case. Once the user has typed on, a press must convert whatever is
        // there now rather than erasing it and typing something older in its place.
        var (buffer, undo) = AfterCorrecting(LayoutFixture.TypedInEnglish("привет"));

        buffer.Append(new KeyStroke(0, 0, ModifierKeys.None, ' '));

        foreach (char c in "rfr")
        {
            int index = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`".IndexOf(c);
            buffer.Append(new KeyStroke((ushort)(index + 1), 0, ModifierKeys.None, c));
        }

        Assert.False(undo.Reverses(buffer, buffer.GetLastWord()));
    }

    [Fact]
    public void EditingTheCorrectedWordMakesTheNextPressAnOrdinaryConversion()
    {
        var (buffer, undo) = AfterCorrecting(LayoutFixture.TypedInEnglish("привет"));

        buffer.Backspace();

        Assert.False(undo.Reverses(buffer, buffer.GetLastWord()));
    }

    [Fact]
    public void ClearingTheBufferMakesTheUndoStale()
    {
        var (buffer, undo) = AfterCorrecting(LayoutFixture.TypedInEnglish("привет"));

        buffer.Clear();

        Assert.False(undo.Reverses(buffer, buffer.GetLastWord()));
        Assert.False(undo.Reverses(buffer, new StrokeRange(0, 6)));
    }

    [Fact]
    public void TheTailIsErasedAndRetypedSoTheCaretDoesNotMove()
    {
        string typed = LayoutFixture.TypedInEnglish("привет");
        var (_, undo) = AfterCorrecting(typed, " ");

        Assert.Equal("привет ", undo.Wrote);
        Assert.Equal(typed + " ", undo.Restore);

        // The word alone is what identifies the correction; the tail is not part of it.
        Assert.Equal("привет", undo.ConvertedWord);
    }

    [Fact]
    public void AConvertedSelectionIsUndoableButNeverToggles()
    {
        // A selection leaves nothing in the buffer -- what was highlighted need not be what
        // was typed -- so the hotkey cannot recognise it, and only an explicit undo applies.
        var undo = CorrectionUndo.ForSelection("привет как дела", "ghbdtn rfr ltkf");
        var buffer = FakeLayoutResolver.Typed("привет как дела", LayoutFixture.Russian);

        Assert.Equal("привет как дела", undo.Wrote);
        Assert.Equal("ghbdtn rfr ltkf", undo.Restore);
        Assert.False(undo.Reverses(buffer, buffer.GetLastWord()));
        Assert.False(undo.Reverses(buffer, buffer.GetAll()));
    }

    [Fact]
    public void RestoringAnEmptyRangeDoesNothingRatherThanThrowing()
    {
        var buffer = FakeLayoutResolver.Typed("ghbdtn", LayoutFixture.English);
        string before = buffer.GetText(buffer.GetAll());

        CorrectionUndo.ForSelection("привет", "ghbdtn").RestoreBuffer(buffer);

        Assert.Equal(before, buffer.GetText(buffer.GetAll()));
    }

    [Fact]
    public void ARangePastTheEndOfTheBufferIsRefusedRatherThanIndexingOutOfIt()
    {
        // The buffer drops its oldest quarter when it fills up, which moves every range that
        // was recorded before it happened.
        var buffer = FakeLayoutResolver.Typed("ghbdtn", LayoutFixture.English);
        var undo = new CorrectionUndo("привет", "ghbdtn", new StrokeRange(40, 6), "ghbdtn", "привет");

        Assert.False(undo.Reverses(buffer, new StrokeRange(40, 6)));

        var exception = Record.Exception(() => undo.RestoreBuffer(buffer));

        Assert.Null(exception);
    }
}
