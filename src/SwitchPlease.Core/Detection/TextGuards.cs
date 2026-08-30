using System.Text;

namespace SwitchPlease.Core.Detection;

/// <summary>
/// Cases where auto-conversion must never fire. A false positive inside a password, a file
/// path or an identifier is far more annoying than a missed correction, so these run before
/// any scoring and are deliberately blunt.
/// </summary>
public static class TextGuards
{
    public const int MinimumWordLength = 3;
    public const int MaximumWordLength = 32;

    public static bool IsIneligible(string word, out string reason) =>
        IsIneligible(word, MinimumWordLength, out reason);

    /// <param name="minimumLength">
    /// Shortest word worth judging. Configurable because it is the one guard whose right
    /// value is a matter of taste: shorter words are both the most common and the ones with
    /// the least evidence to go on.
    /// </param>
    public static bool IsIneligible(string word, int minimumLength, out string reason)
    {
        if (word.Length < Math.Max(minimumLength, 2))
        {
            reason = "too short";
            return true;
        }

        if (word.Length > MaximumWordLength)
        {
            reason = "too long";
            return true;
        }

        bool hasLetter = false;
        bool hasDigit = false;
        bool hasSeparator = false;
        bool hasInnerUpper = false;

        for (int i = 0; i < word.Length; i++)
        {
            char c = word[i];

            if (char.IsLetter(c))
            {
                hasLetter = true;
                if (i > 0 && char.IsUpper(c))
                {
                    hasInnerUpper = true;
                }
            }
            else if (char.IsDigit(c))
            {
                hasDigit = true;
            }
            else if (c is '/' or '\\' or '@' or '_' or '.' or ':' or '#' or '$' or '%')
            {
                hasSeparator = true;
            }
        }

        if (!hasLetter)
        {
            reason = "no letters";
            return true;
        }

        // Identifiers, versions, paths, emails, passwords: leave them alone.
        if (hasDigit)
        {
            reason = "contains digits";
            return true;
        }

        if (hasSeparator)
        {
            reason = "path-like or address-like";
            return true;
        }

        if (hasInnerUpper)
        {
            reason = "mixed case (identifier)";
            return true;
        }

        if (IsMixedScript(word))
        {
            reason = "already mixed script";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>True when the word already contains letters from two different alphabets.</summary>
    public static bool IsMixedScript(string word)
    {
        bool latin = false;
        bool cyrillic = false;

        foreach (char c in word)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            var block = ScriptOf(c);
            latin |= block == Script.Latin;
            cyrillic |= block == Script.Cyrillic;
        }

        return latin && cyrillic;
    }

    /// <summary>
    /// Which alphabet a letter belongs to.
    ///
    /// The accented ranges matter: Czech "příliš" and German "Grüße" are Latin words, and
    /// treating their accented letters as foreign made the switcher score real words near
    /// zero and consider rewriting them.
    /// </summary>
    public static Script ScriptOf(char c)
    {
        if (!char.IsLetter(c))
        {
            return Script.Other;
        }

        return c switch
        {
            >= 'a' and <= 'z' or >= 'A' and <= 'Z' => Script.Latin,

            // Latin-1 Supplement, Latin Extended-A and Extended-B: the accented letters of
            // German, Czech, Polish, French, Turkish and the rest.
            >= 'À' and <= 'ɏ' => Script.Latin,

            // Cyrillic and its supplement.
            >= 'Ѐ' and <= 'ԯ' => Script.Cyrillic,

            _ => Script.Other,
        };
    }

    /// <summary>The alphabet the majority of letters in <paramref name="word"/> belong to.</summary>
    public static Script DominantScript(string word)
    {
        int latin = 0;
        int cyrillic = 0;

        foreach (char c in word)
        {
            switch (ScriptOf(c))
            {
                case Script.Latin:
                    latin++;
                    break;
                case Script.Cyrillic:
                    cyrillic++;
                    break;
            }
        }

        if (latin == 0 && cyrillic == 0)
        {
            return Script.Other;
        }

        return latin >= cyrillic ? Script.Latin : Script.Cyrillic;
    }
}

public enum Script
{
    Other,
    Latin,
    Cyrillic,
}
