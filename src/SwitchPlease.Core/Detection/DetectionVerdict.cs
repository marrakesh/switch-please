namespace SwitchPlease.Core.Detection;

/// <summary>Outcome of asking "was this word typed in the wrong layout?".</summary>
/// <param name="ShouldConvert">Whether the caller should rewrite the word.</param>
/// <param name="Confidence">0..1. Compared against the user's sensitivity setting.</param>
/// <param name="Reason">Short human-readable explanation, surfaced in the debug log.</param>
public readonly record struct DetectionVerdict(bool ShouldConvert, double Confidence, string Reason)
{
    public static DetectionVerdict No(string reason) => new(false, 0, reason);

    public static DetectionVerdict Yes(double confidence, string reason) => new(true, confidence, reason);
}

public interface IWrongLayoutDetector
{
    /// <summary>
    /// Compares the word as it was typed against the same keys read in the other layout.
    /// </summary>
    /// <param name="precedingText">
    /// What was typed before this word, where it is available. A word does not arrive alone:
    /// after three Russian words the fourth is far more likely to be Russian too, and that is
    /// evidence no amount of letter statistics about the word itself can supply.
    /// </param>
    /// <param name="precedingConverted">
    /// The same preceding text read in the layout this word would be converted to.
    ///
    /// This is what covers the commonest mistake of all -- noticing halfway through a
    /// sentence that the whole thing went in wrong. There the text before the word is
    /// gibberish as it stands and perfectly ordinary once converted, which says a great deal
    /// about the word that follows it.
    /// </param>
    DetectionVerdict Evaluate(
        string asTyped,
        string converted,
        string? precedingText = null,
        string? precedingConverted = null);
}
