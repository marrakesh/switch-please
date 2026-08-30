namespace SwitchPlease.Core.Detection;

/// <summary>
/// The set of languages the switcher will reason about, assembled from the keyboard layouts
/// the user actually has installed rather than from a list fixed in the source.
///
/// Each installed layout contributes one language. Where hand-written frequency data exists
/// for it -- Russian, Ukrainian, English -- that is used. Otherwise a profile is built from
/// the alphabet the layout itself types, which is enough to tell whose alphabet a word
/// belongs to; judging whether it reads naturally then falls to the system dictionary, and
/// where there is none the catalog says so and the caller declines to act.
///
/// The practical consequence: adding a Czech or Polish layout in Windows makes the switcher
/// aware of Czech or Polish, with no change here.
/// </summary>
public sealed class LanguageCatalog
{
    /// <summary>How much of a word's alphabet a profile must cover to be allowed to judge it.</summary>
    private const double RequiredCoverage = 0.8;

    /// <summary>
    /// How much of a layout's alphabet a built-in profile must already contain before that
    /// profile is accepted as the model for it. Below this the layout types letters the
    /// language does not have, so it is a different language wearing the same label.
    /// </summary>
    private const double BuiltInAlphabetMatch = 0.95;

    private readonly List<LanguageProfile> _profiles;

    public LanguageCatalog(IEnumerable<LanguageProfile> profiles)
    {
        _profiles = [.. profiles];
    }

    /// <summary>The languages carried in the source, used when no layout information is available.</summary>
    public static LanguageCatalog BuiltIn { get; } = new(LanguageProfile.BuiltIn);

    public IReadOnlyList<LanguageProfile> Profiles => _profiles;

    /// <summary>
    /// Builds a catalog from the languages the installed layouts represent.
    ///
    /// A hand-written profile wins over a generated one for the same language, and a
    /// language appearing on two layouts is only added once.
    /// </summary>
    /// <param name="layouts">
    /// One entry per installed layout: its language tag, a display name, and every character
    /// the layout can type.
    /// </param>
    public static LanguageCatalog FromLayouts(IEnumerable<LayoutLanguage> layouts)
    {
        var profiles = new List<LanguageProfile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layout in layouts)
        {
            string alphabet = ExtractAlphabet(layout.TypableCharacters, out var script);

            if (alphabet.Length == 0 || script == Script.Other)
            {
                continue;
            }

            // Keyed on the alphabet as well as the tag, because Windows labels layouts by
            // input language and two very different ones can share a label: a Czech layout
            // reports itself as English. Collapsing those would lose the Czech alphabet.
            if (!seen.Add($"{layout.LanguageTag}|{alphabet}"))
            {
                continue;
            }

            var known = LanguageProfile.BuiltIn.FirstOrDefault(p =>
                string.Equals(p.LanguageTag, layout.LanguageTag, StringComparison.OrdinalIgnoreCase)
                && p.AlphabetCoverage(alphabet) >= BuiltInAlphabetMatch);

            if (known is not null)
            {
                if (!profiles.Contains(known))
                {
                    profiles.Add(known);
                }

                continue;
            }

            // The tag says one language and the alphabet says another. Believing the tag
            // would send this layout's words to the wrong dictionary and have them all come
            // back unrecognised, so it is dropped and the profile stands on its alphabet
            // alone -- which means abstaining rather than guessing.
            var claimant = LanguageProfile.BuiltIn.FirstOrDefault(p =>
                string.Equals(p.LanguageTag, layout.LanguageTag, StringComparison.OrdinalIgnoreCase));

            profiles.Add(LanguageProfile.FromLayoutAlphabet(
                claimant is null ? layout.ShortName : NameByDifference(layout.ShortName, alphabet, claimant),
                claimant is null ? layout.LanguageTag : string.Empty,
                script,
                alphabet));
        }

        // Russian and Ukrainian must both be present whenever either is: their layouts are
        // three keys apart, and judging one without the other is what made mistyped text
        // look correct.
        AddCyrillicCounterpart(profiles);

        return profiles.Count == 0 ? BuiltIn : new LanguageCatalog(profiles);
    }

    /// <summary>Languages of a given script, in the order they were registered.</summary>
    public IReadOnlyList<LanguageProfile> ForScript(Script script) =>
        [.. _profiles.Where(p => p.Script == script)];

    /// <summary>
    /// Scores <paramref name="text"/> against every language of its script and returns the
    /// best fit. Taking the maximum is what stops a Ukrainian word being marked implausible
    /// merely because it is not Russian.
    /// </summary>
    public (LanguageProfile? Profile, double Score) Evaluate(string text) =>
        Evaluate(text, NoWordValidator.Instance);

    public (LanguageProfile? Profile, double Score) Evaluate(string text, IWordValidator validator)
    {
        LanguageProfile? best = null;
        double bestScore = 0;

        foreach (var profile in ForScript(TextGuards.DominantScript(text)))
        {
            double score = profile.Score(text, validator);

            if (best is null || score > bestScore)
            {
                best = profile;
                bestScore = score;
            }
        }

        return (best, bestScore);
    }

    /// <summary>
    /// Whether any language here can actually judge <paramref name="text"/>.
    ///
    /// Covering the alphabet is not enough on its own: a profile derived from a layout knows
    /// which letters belong to it but nothing about how they combine, so without a
    /// dictionary it has no opinion worth acting on. Saying so is what stops correct Czech
    /// or Turkish being rewritten as though it were noise.
    /// </summary>
    public bool IsFamiliar(string text, IWordValidator validator)
    {
        foreach (var profile in ForScript(TextGuards.DominantScript(text)))
        {
            if (profile.AlphabetCoverage(text) < RequiredCoverage)
            {
                continue;
            }

            if (profile.HasStatisticalModel || validator.HasDictionary(profile.LanguageTag))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsFamiliar(string text) => IsFamiliar(text, NoWordValidator.Instance);

    /// <summary>
    /// Reduces the characters a layout types to the alphabet of its language: letters only,
    /// lower-cased, and only from the script the layout is predominantly in.
    /// </summary>
    private static string ExtractAlphabet(string typableCharacters, out Script script)
    {
        script = TextGuards.DominantScript(typableCharacters);

        if (script == Script.Other)
        {
            return string.Empty;
        }

        var letters = new SortedSet<char>();

        foreach (char c in typableCharacters)
        {
            if (char.IsLetter(c) && TextGuards.ScriptOf(c) == script)
            {
                letters.Add(char.ToLowerInvariant(c));
            }
        }

        return string.Concat(letters);
    }

    /// <summary>
    /// Names a layout that Windows has labelled with someone else's language by the letters
    /// that give it away, so the diagnostics view shows "en+áéíč" rather than a second,
    /// indistinguishable "en".
    /// </summary>
    private static string NameByDifference(string shortName, string alphabet, LanguageProfile claimant)
    {
        var extra = alphabet.Where(c => claimant.AlphabetCoverage(c.ToString()) == 0).Take(4);
        string hint = string.Concat(extra);

        return hint.Length == 0 ? shortName : $"{shortName}+{hint}";
    }

    private static void AddCyrillicCounterpart(List<LanguageProfile> profiles)
    {
        bool hasRussian = profiles.Contains(LanguageProfile.Russian);
        bool hasUkrainian = profiles.Contains(LanguageProfile.Ukrainian);

        if (hasRussian && !hasUkrainian)
        {
            profiles.Add(LanguageProfile.Ukrainian);
        }
        else if (hasUkrainian && !hasRussian)
        {
            profiles.Add(LanguageProfile.Russian);
        }
    }
}

/// <summary>One installed keyboard layout, described in terms the catalog can use.</summary>
/// <param name="LanguageTag">BCP-47 tag, e.g. "cs-CZ".</param>
/// <param name="ShortName">Short label for logging, e.g. "cs".</param>
/// <param name="TypableCharacters">Every character the layout can produce.</param>
public readonly record struct LayoutLanguage(string LanguageTag, string ShortName, string TypableCharacters);
