using SwitchPlease.Core.Keys;

namespace SwitchPlease.Core.Conversion;

/// <summary>
/// What one correction changed, and what it takes to put it back.
///
/// Two jobs. The first is undo proper: erase what was typed and type the original in its
/// place. The second is subtler and is what makes the hotkey behave the way everyone expects
/// -- pressing it again on a word it just corrected puts that word back, rather than doing
/// nothing.
///
/// It did nothing before, and the reason was not a bug anywhere: after a correction the word
/// is a real word, so the refusal margin, which exists to protect correct text from a stray
/// double tap, declined to touch it. The margin is right. What was missing is that reversing
/// this program's own last correction is not the case it was guarding against.
/// </summary>
/// <param name="Wrote">Text now on screen, all of which has to be erased.</param>
/// <param name="Restore">Text to type in its place.</param>
/// <param name="Word">Where the word sits in the buffer, so the record can be restored too.</param>
/// <param name="Original">The word as originally typed. Empty for a converted selection.</param>
/// <param name="ConvertedWord">
/// The word alone, without the tail retyped after it. Compared against the buffer to tell
/// "the hotkey was pressed again on the word we just corrected" from "it was pressed on
/// something else".
/// </param>
public sealed record CorrectionUndo(
    string Wrote,
    string Restore,
    StrokeRange Word,
    string Original,
    string ConvertedWord)
{
    /// <summary>
    /// Whether the switcher made this correction on its own. Undoing one of those is the user
    /// saying the word was right, which is worth remembering.
    /// </summary>
    public bool Automatic { get; init; }

    public static CorrectionUndo ForWord(ConversionPlan plan) => new(
        plan.Converted + plan.Suffix,
        plan.Original + plan.Suffix,
        plan.Word,
        plan.Original,
        plan.Converted);

    /// <summary>
    /// A converted selection. There is no range: the buffer is dropped when a selection is
    /// rewritten, because what was highlighted need not be what was typed.
    /// </summary>
    public static CorrectionUndo ForSelection(string converted, string original) =>
        new(converted, original, StrokeRange.Empty, string.Empty, converted);

    /// <summary>
    /// Whether pressing the hotkey again on <paramref name="word"/> means "put that back".
    ///
    /// Only the exact word that was corrected counts. Typing anything after it moves the
    /// range or changes the text, and then the press is an ordinary conversion again.
    /// </summary>
    public bool Reverses(TypingBuffer buffer, StrokeRange word) =>
        !Word.IsEmpty
        && Word.Start == word.Start
        && Word.Length == word.Length
        && word.End <= buffer.Count
        && string.Equals(buffer.GetText(word), ConvertedWord, StringComparison.Ordinal);

    /// <summary>
    /// Brings the record back in line with the screen after the text has been put back, so
    /// the hotkey works on the restored word rather than on the one no longer there.
    ///
    /// The scan codes are left alone: they describe which keys were pressed, which undoing a
    /// conversion does not change.
    /// </summary>
    public void RestoreBuffer(TypingBuffer buffer)
    {
        if (Word.IsEmpty || Word.End > buffer.Count || Original.Length != Word.Length)
        {
            return;
        }

        var strokes = buffer.Strokes;

        for (int i = 0; i < Word.Length; i++)
        {
            int index = Word.Start + i;
            buffer.Replace(index, strokes[index] with { Character = Original[i] });
        }
    }
}
