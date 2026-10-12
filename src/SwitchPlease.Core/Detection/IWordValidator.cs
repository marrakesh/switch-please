namespace SwitchPlease.Core.Detection;

/// <summary>What a dictionary knows about a word.</summary>
public enum WordStatus
{
    /// <summary>No dictionary is installed for this language; nothing can be said.</summary>
    NoDictionary,

    /// <summary>The dictionary recognises the word.</summary>
    Known,

    /// <summary>The dictionary has a dictionary for this language and the word is not in it.</summary>
    Unknown,
}

/// <summary>
/// How certainly a string is a word, weakest first.
///
/// The dictionary alone is not enough to tell a word from a slip. Windows accepts "ns" and
/// "vs" as English, abbreviations both, and they are exactly what "ты" and "мы" become on the
/// wrong keyboard. Being one of the commonest words of a language is the stronger claim.
/// </summary>
public enum WordStanding
{
    /// <summary>Nothing says it is a word.</summary>
    Unknown,

    /// <summary>The dictionary knows it.</summary>
    InDictionary,

    /// <summary>One of the commonest words of the language.</summary>
    Common,
}

/// <summary>
/// A dictionary lookup, which is the one thing a statistical model cannot replace.
///
/// Russian and Ukrainian keyboards differ by three keys, so "привіт" typed on the Russian
/// layout becomes "привыт" -- phonotactically flawless Russian that no bigram model will
/// reject. Only asking "is that actually a word?" settles it.
/// </summary>
public interface IWordValidator
{
    /// <summary>Whether anything is known about <paramref name="languageTag"/> at all, e.g. "ru-RU".</summary>
    bool HasDictionary(string languageTag);

    /// <summary>Looks up a single word. Implementations are expected to cache.</summary>
    WordStatus Check(string word, string languageTag);
}

/// <summary>Used when no dictionary service is available, leaving the model to decide alone.</summary>
public sealed class NoWordValidator : IWordValidator
{
    public static NoWordValidator Instance { get; } = new();

    public bool HasDictionary(string languageTag) => false;

    public WordStatus Check(string word, string languageTag) => WordStatus.NoDictionary;
}
