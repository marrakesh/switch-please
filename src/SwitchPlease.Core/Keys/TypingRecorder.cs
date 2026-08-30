namespace SwitchPlease.Core.Keys;

/// <summary>What one key event did to the record of what has been typed.</summary>
public enum RecordingOutcome
{
    /// <summary>Nothing changed: a modifier on its own, or a key that produces no character.</summary>
    Ignored,

    /// <summary>A character was added, or one was taken back.</summary>
    Recorded,

    /// <summary>A character was added and it ended a word, so a correction may be due.</summary>
    WordEnded,

    /// <summary>The record was abandoned: the caret has moved, or the key was a command.</summary>
    Discarded,
}

/// <summary>
/// What a physical key produces right now.
///
/// An interface because the answer comes from Windows -- the active layout, and what
/// <c>ToUnicodeEx</c> says that layout makes of a scan code -- and because everything the
/// recorder does with the answer is worth testing without it.
/// </summary>
public interface ICharacterResolver
{
    /// <summary>
    /// The scan code for a virtual key, for events that arrive without one. The on-screen
    /// keyboard, remote desktop clients and automation tools all send zero.
    /// </summary>
    ushort ScanCodeFor(ushort virtualKey);

    /// <summary>What the key at <paramref name="scanCode"/> types under the active layout.</summary>
    char Resolve(ushort scanCode, ModifierKeys modifiers);
}

/// <summary>
/// Keeps the record of what has been typed in step with what is on the screen.
///
/// This is the part where being wrong is silent and expensive. Every correction is a number
/// of backspaces followed by replacement text, and that number comes from here: a record
/// holding one character more than the screen does deletes something the user typed and never
/// meant to lose. There is no error to notice, because from the application's point of view
/// nothing failed.
///
/// Separated from the service that owns the hook so that all of it can be exercised directly.
/// The service is left with the questions only Windows can answer -- has focus moved, is this
/// a password field, is a game running -- and hands the keystroke here once they are settled.
/// </summary>
public sealed class TypingRecorder(TypingBuffer buffer)
{
    public TypingBuffer Buffer { get; } = buffer;

    /// <summary>
    /// Applies one key press to the record.
    ///
    /// Only presses: a release changes nothing, and feeding both would count every character
    /// twice.
    /// </summary>
    /// <param name="scanCode">As reported, or zero if the source supplied none.</param>
    public RecordingOutcome Record(
        ushort virtualKey,
        ushort scanCode,
        ModifierKeys modifiers,
        ICharacterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        switch (KeystrokePolicy.Classify(virtualKey, modifiers))
        {
            case KeystrokeEffect.Backspace:
                Buffer.Backspace();
                return RecordingOutcome.Recorded;

            case KeystrokeEffect.Reset:
                Buffer.Clear();
                return RecordingOutcome.Discarded;

            case KeystrokeEffect.Ignore:
                return RecordingOutcome.Ignored;
        }

        ushort resolved = scanCode != 0 ? scanCode : resolver.ScanCodeFor(virtualKey);

        if (resolved == 0)
        {
            return RecordingOutcome.Ignored;
        }

        char character = resolver.Resolve(resolved, modifiers);

        // A dead key, or one this layout does not map. Recording nothing is right: nothing
        // reached the screen either.
        if (character == '\0')
        {
            return RecordingOutcome.Ignored;
        }

        var stroke = new KeyStroke(resolved, virtualKey, modifiers, character);

        Buffer.Append(stroke);

        return stroke.IsWhitespace ? RecordingOutcome.WordEnded : RecordingOutcome.Recorded;
    }

    /// <summary>
    /// Abandons the record. Called when the caret has gone somewhere that cannot be followed:
    /// a different window, a mouse click, a password field.
    /// </summary>
    public void Discard() => Buffer.Clear();
}
