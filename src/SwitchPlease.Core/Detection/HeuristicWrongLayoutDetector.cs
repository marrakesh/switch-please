namespace SwitchPlease.Core.Detection;

/// <summary>
/// Decides whether a word reads better in the other layout by scoring both readings and
/// requiring the alternative to win by a clear margin. Ties and near-ties are left alone:
/// silence costs the user one hotkey press, a wrong rewrite costs them their sentence.
/// </summary>
public sealed class HeuristicWrongLayoutDetector(
    double minimumMargin = 0.25,
    IWordValidator? validator = null,
    LanguageCatalog? languages = null,
    int minimumWordLength = TextGuards.MinimumWordLength) : IWrongLayoutDetector
{
    /// <summary>
    /// How far the surrounding text may move a score.
    ///
    /// Deliberately small. The preceding words are a hint about which language is being
    /// written, not a statement about this word: people switch languages mid-sentence, quote
    /// names and paste terms. Large enough to break the ties the letter statistics cannot,
    /// too small to overrule them.
    /// </summary>
    private const double ContextWeight = 0.08;

    /// <summary>
    /// Least amount of preceding text worth reading a language off. One short word is as
    /// likely to be a stray as it is to be evidence.
    /// </summary>
    private const int MinimumContextLetters = 6;

    private readonly double _minimumMargin = Math.Clamp(minimumMargin, 0.05, 0.95);
    private readonly IWordValidator _validator = validator ?? NoWordValidator.Instance;
    private readonly LanguageCatalog _languages = languages ?? LanguageCatalog.BuiltIn;
    private readonly int _minimumWordLength = minimumWordLength;

    public DetectionVerdict Evaluate(
        string asTyped,
        string converted,
        string? precedingText = null,
        string? precedingConverted = null)
    {
        if (string.IsNullOrEmpty(asTyped) || string.Equals(asTyped, converted, StringComparison.Ordinal))
        {
            return DetectionVerdict.No("nothing to convert");
        }

        if (TextGuards.IsIneligible(asTyped, _minimumWordLength, out string reason))
        {
            return DetectionVerdict.No(reason);
        }

        // Neither reading may be judged unless we actually model its alphabet. Czech and
        // German words scored near zero under the English profile, which would have made
        // correct text look like noise worth rewriting.
        if (!_languages.IsFamiliar(asTyped, _validator) || !_languages.IsFamiliar(converted, _validator))
        {
            return DetectionVerdict.No("unfamiliar alphabet");
        }

        var (typedProfile, typedScore) = _languages.Evaluate(asTyped, _validator);
        var (convertedProfile, convertedScore) = _languages.Evaluate(converted, _validator);

        if (typedProfile is null || convertedProfile is null)
        {
            return DetectionVerdict.No("unsupported script");
        }

        if (typedProfile.Script == convertedProfile.Script)
        {
            if (ReferenceEquals(typedProfile, convertedProfile))
            {
                return DetectionVerdict.No("same language on both sides");
            }

            // Two languages sharing a script -- Russian and Ukrainian -- produce readings
            // that both look like ordinary text, and a statistical model cannot separate
            // them without rewriting correct words. A dictionary can, so this is allowed
            // only when one is installed for at least one of the two.
            bool canArbitrate = _validator.HasDictionary(typedProfile.LanguageTag)
                || _validator.HasDictionary(convertedProfile.LanguageTag);

            if (!canArbitrate)
            {
                return DetectionVerdict.No("same script, no dictionary to separate them");
            }
        }

        // The text before this word, read whichever way makes sense of it. Preferring the
        // as-typed reading matters: while it is ordinary text, that is what the line is in,
        // and the converted reading of ordinary text is gibberish that must not get a vote.
        var expected = LanguageOf(precedingText) ?? LanguageOf(precedingConverted);

        typedScore += ContextBonus(typedProfile, expected);
        convertedScore += ContextBonus(convertedProfile, expected);

        double margin = convertedScore - typedScore;

        string detail = $"{typedProfile.Name}={typedScore:F2} {convertedProfile.Name}={convertedScore:F2} margin={margin:F2}"
            + (expected is null ? string.Empty : $" after={expected.Name}");

        return margin >= _minimumMargin
            ? DetectionVerdict.Yes(margin, detail)
            : DetectionVerdict.No(detail);
    }

    /// <summary>
    /// Nudges a reading towards or away from the language the user has been writing in.
    /// Symmetric on purpose: it makes a correction back into the current language slightly
    /// easier, and rewriting a word that already matches its surroundings slightly harder.
    /// </summary>
    private static double ContextBonus(LanguageProfile candidate, LanguageProfile? expected)
    {
        if (expected is null)
        {
            return 0;
        }

        return ReferenceEquals(candidate, expected) ? ContextWeight : -ContextWeight;
    }

    /// <summary>
    /// The language the preceding text is written in, or null when there is too little of it
    /// to say. Only confident readings count: text that scores poorly in every language is
    /// most likely itself mistyped, and taking its word for what comes next would let one
    /// wrong guess drag the rest of the line after it.
    /// </summary>
    private LanguageProfile? LanguageOf(string? precedingText)
    {
        if (string.IsNullOrWhiteSpace(precedingText))
        {
            return null;
        }

        int letters = 0;

        foreach (char c in precedingText)
        {
            if (char.IsLetter(c))
            {
                letters++;
            }
        }

        if (letters < MinimumContextLetters)
        {
            return null;
        }

        var (profile, score) = _languages.Evaluate(precedingText, _validator);

        return score >= 0.75 ? profile : null;
    }
}

/// <summary>Detector that never fires, used when auto-detection is switched off.</summary>
public sealed class DisabledDetector : IWrongLayoutDetector
{
    public static DisabledDetector Instance { get; } = new();

    public DetectionVerdict Evaluate(
        string asTyped,
        string converted,
        string? precedingText = null,
        string? precedingConverted = null) =>
        DetectionVerdict.No("auto-detection disabled");
}
