using System.Collections.Frozen;

namespace SwitchPlease.Core.Detection;

/// <summary>
/// Words automatic correction leaves alone: names, logins, slang, a word in a language the
/// models do not know -- anything that reads like a mistake and is not one.
///
/// Measured accuracy says nothing about these. No correctly typed word in the test corpus is
/// ever rewritten, but the corpus is not anyone's surname, and the one word a particular user
/// keeps typing is exactly the kind no corpus has. So the list fills itself the one way that
/// costs the user nothing: undoing an automatic correction puts the word on it.
///
/// Matching ignores case and the punctuation round a word, because the same word arrives as
/// "ntcn", "Ntcn" and "ntcn," and deserves the same answer each time.
/// </summary>
public sealed class WordExceptions
{
    private readonly FrozenSet<string> _words;

    public WordExceptions(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        _words = words
            .Select(Normalize)
            .Where(word => word.Length > 0)
            .ToFrozenSet(StringComparer.Ordinal);
    }

    public static WordExceptions Empty { get; } = new([]);

    public int Count => _words.Count;

    public bool Contains(string word)
    {
        string key = Normalize(word);

        return key.Length > 0 && _words.Contains(key);
    }

    /// <summary>A copy with <paramref name="word"/> added, for use before the settings catch up.</summary>
    public WordExceptions With(string word) => new(_words.Append(word));

    /// <summary>
    /// The form a word is kept and compared in: lower case, without the punctuation that
    /// happened to be typed before or after it. Empty when nothing of it is left.
    /// </summary>
    public static string Normalize(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        int start = 0;
        int end = word.Length;

        while (start < end && !char.IsLetterOrDigit(word[start]))
        {
            start++;
        }

        while (end > start && !char.IsLetterOrDigit(word[end - 1]))
        {
            end--;
        }

        return word[start..end].ToLowerInvariant();
    }
}
