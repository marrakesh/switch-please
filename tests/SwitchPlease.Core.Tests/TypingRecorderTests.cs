using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Keeping the record in step with the screen.
///
/// Every correction is a count of backspaces followed by replacement text, and that count
/// comes from here. A record holding one character more than the screen does deletes
/// something the user typed and never meant to lose, and nothing anywhere reports a failure,
/// because from the application's point of view nothing failed. That is the whole reason
/// these tests exist: the failure is silent, so it has to be caught before it ships.
/// </summary>
public class TypingRecorderTests
{
    /// <summary>A layout where every letter key types itself and space types a space.</summary>
    private sealed class Latin : ICharacterResolver
    {
        /// <summary>Scan codes this pretends the keyboard has, one per virtual key.</summary>
        public ushort ScanCodeFor(ushort virtualKey) => (ushort)(virtualKey + 1000);

        public char Resolve(ushort scanCode, ModifierKeys modifiers)
        {
            // Scan codes this handed out are the virtual key plus a thousand; anything
            // smaller came from the caller and stands for the virtual key itself, so a
            // supplied scan code is honoured rather than reinterpreted as nonsense.
            ushort virtualKey = scanCode >= 1000 ? (ushort)(scanCode - 1000) : scanCode;

            if (virtualKey == VirtualKeys.Space)
            {
                return ' ';
            }

            if (virtualKey is < (ushort)'A' or > (ushort)'Z')
            {
                return '\0';
            }

            return (modifiers & ModifierKeys.Shift) != 0
                ? (char)virtualKey
                : char.ToLowerInvariant((char)virtualKey);
        }
    }

    private static (TypingRecorder Recorder, TypingBuffer Buffer) New()
    {
        var buffer = new TypingBuffer();
        return (new TypingRecorder(buffer), buffer);
    }

    private static RecordingOutcome Press(
        TypingRecorder recorder, char key, ModifierKeys modifiers = ModifierKeys.None, ushort scanCode = 0)
    {
        ushort virtualKey = (ushort)char.ToUpperInvariant(key);
        return recorder.Record(virtualKey, scanCode, modifiers, new Latin());
    }

    private static string TextOf(TypingBuffer buffer) => buffer.GetText(buffer.GetAll());

    [Fact]
    public void OrdinaryTypingIsRecordedCharacterForCharacter()
    {
        var (recorder, buffer) = New();

        foreach (char c in "hello")
        {
            Assert.Equal(RecordingOutcome.Recorded, Press(recorder, c));
        }

        Assert.Equal("hello", TextOf(buffer));
    }

    [Fact]
    public void SpaceEndsTheWordAndIsStillRecorded()
    {
        // Both halves matter. The caller corrects on the word ending, and the space itself
        // has to be in the record because the correction erases it and types it back.
        var (recorder, buffer) = New();

        Press(recorder, 'h');
        Press(recorder, 'i');

        Assert.Equal(RecordingOutcome.WordEnded, recorder.Record(VirtualKeys.Space, 0, ModifierKeys.None, new Latin()));
        Assert.Equal("hi ", TextOf(buffer));
    }

    [Fact]
    public void BackspaceTakesTheLastCharacterBack()
    {
        var (recorder, buffer) = New();

        Press(recorder, 'a');
        Press(recorder, 'b');

        Assert.Equal(
            RecordingOutcome.Recorded,
            recorder.Record(VirtualKeys.Back, 0, ModifierKeys.None, new Latin()));

        Assert.Equal("a", TextOf(buffer));
    }

    [Fact]
    public void BackspacingPastTheStartIsHarmless()
    {
        // The user may have been typing before the switcher started watching, so the record
        // can legitimately be shorter than what is on screen.
        var (recorder, buffer) = New();

        for (int i = 0; i < 5; i++)
        {
            recorder.Record(VirtualKeys.Back, 0, ModifierKeys.None, new Latin());
        }

        Assert.Equal(string.Empty, TextOf(buffer));

        Press(recorder, 'x');
        Assert.Equal("x", TextOf(buffer));
    }

    [Theory]
    [InlineData(VirtualKeys.Return)]
    [InlineData(VirtualKeys.Tab)]
    [InlineData(VirtualKeys.Escape)]
    [InlineData(VirtualKeys.Left)]
    [InlineData(VirtualKeys.Home)]
    [InlineData(VirtualKeys.End)]
    [InlineData(VirtualKeys.Prior)]
    [InlineData(VirtualKeys.Delete)]
    public void KeysThatMoveTheCaretThrowTheRecordAway(ushort virtualKey)
    {
        // After any of these the record no longer describes what sits in front of the caret,
        // and a correction built on it would delete the wrong text.
        var (recorder, buffer) = New();

        Press(recorder, 'a');
        Press(recorder, 'b');

        Assert.Equal(RecordingOutcome.Discarded, recorder.Record(virtualKey, 0, ModifierKeys.None, new Latin()));
        Assert.Equal(string.Empty, TextOf(buffer));
    }

    [Fact]
    public void AShortcutThrowsTheRecordAway()
    {
        var (recorder, buffer) = New();

        Press(recorder, 'a');

        Assert.Equal(RecordingOutcome.Discarded, Press(recorder, 'C', ModifierKeys.Control));
        Assert.Equal(string.Empty, TextOf(buffer));
    }

    [Fact]
    public void AltGrIsTypingRatherThanAShortcut()
    {
        // On Polish, German and others AltGr is how ordinary letters are reached. Treating
        // Ctrl+Alt as a command there would throw the record away on every accented letter.
        var (recorder, buffer) = New();

        Assert.Equal(RecordingOutcome.Recorded, Press(recorder, 'a', ModifierKeys.AltGr));
        Assert.Equal("a", TextOf(buffer));
    }

    [Fact]
    public void AModifierHeldOnItsOwnChangesNothing()
    {
        // Checked before the shortcut rule, which would otherwise see the modifier the key
        // itself just set and wipe the record every time the user reached for a hotkey.
        var (recorder, buffer) = New();

        Press(recorder, 'a');

        foreach (ushort modifier in (ushort[])
                 [VirtualKeys.Shift, VirtualKeys.LShift, VirtualKeys.Control, VirtualKeys.LMenu, VirtualKeys.LWin])
        {
            Assert.Equal(
                RecordingOutcome.Ignored,
                recorder.Record(modifier, 0, ModifierKeys.None, new Latin()));
        }

        Assert.Equal("a", TextOf(buffer));
    }

    [Fact]
    public void AKeyThatTypesNothingUnderThisLayoutIsNotRecorded()
    {
        // A dead key, or one the layout does not map. Nothing reached the screen, so nothing
        // may reach the record either -- otherwise the counts drift apart by one.
        var (recorder, buffer) = New();

        Press(recorder, 'a');

        Assert.Equal(RecordingOutcome.Ignored, recorder.Record(0xDE, 0, ModifierKeys.None, new Latin()));
        Assert.Equal("a", TextOf(buffer));
    }

    [Fact]
    public void AMissingScanCodeIsRecoveredFromTheVirtualKey()
    {
        // The on-screen keyboard, remote desktop clients and automation tools all send zero,
        // and the whole conversion path is built on scan codes.
        var (recorder, buffer) = New();

        Assert.Equal(RecordingOutcome.Recorded, Press(recorder, 'k', scanCode: 0));

        Assert.Equal("k", TextOf(buffer));
        Assert.NotEqual(0, buffer.Strokes[0].ScanCode);
    }

    [Fact]
    public void AScanCodeThatArrivesIsKeptRatherThanRecomputed()
    {
        // The scan code is what lets the word be replayed under another layout. Overwriting
        // a real one with a guess would break conversion on any non-standard keyboard.
        var (recorder, buffer) = New();

        Press(recorder, 'k', scanCode: (ushort)'K');

        Assert.Equal((ushort)'K', buffer.Strokes[0].ScanCode);
        Assert.Equal("k", TextOf(buffer));
    }

    [Fact]
    public void ShiftIsCarriedIntoTheRecord()
    {
        var (recorder, buffer) = New();

        Press(recorder, 'h', ModifierKeys.Shift);
        Press(recorder, 'i');

        Assert.Equal("Hi", TextOf(buffer));
        Assert.Equal(ModifierKeys.Shift, buffer.Strokes[0].Modifiers);
    }

    [Fact]
    public void DiscardingEmptiesTheRecordWithoutBreakingWhatFollows()
    {
        var (recorder, buffer) = New();

        Press(recorder, 'a');
        recorder.Discard();

        Assert.Equal(string.Empty, TextOf(buffer));

        Press(recorder, 'b');
        Assert.Equal("b", TextOf(buffer));
    }

    [Fact]
    public void TheWordTheHotkeyWouldCorrectIsTheOneJustTyped()
    {
        // The end-to-end shape of the thing: type two words, and the last word is the second.
        var (recorder, buffer) = New();

        foreach (char c in "one")
        {
            Press(recorder, c);
        }

        recorder.Record(VirtualKeys.Space, 0, ModifierKeys.None, new Latin());

        foreach (char c in "two")
        {
            Press(recorder, c);
        }

        Assert.Equal("two", buffer.GetText(buffer.GetLastWord()));
    }

    [Fact]
    public void ARecorderRefusesToWorkWithoutAResolver() =>
        Assert.Throws<ArgumentNullException>(
            () => new TypingRecorder(new TypingBuffer()).Record(65, 0, ModifierKeys.None, null!));
}
