using SwitchPlease.Core.Detection;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// A dictionary stand-in. Which languages it covers is deliberately configurable: on a real
/// machine some are installed and some are not, and the switcher has to behave sensibly
/// either way.
/// </summary>
internal sealed class FakeWordValidator : IWordValidator
{
    private readonly Dictionary<string, HashSet<string>> _words = new(StringComparer.OrdinalIgnoreCase);

    public int Lookups { get; private set; }

    public FakeWordValidator With(string languageTag, params string[] words)
    {
        _words[languageTag] = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
        return this;
    }

    public bool HasDictionary(string languageTag) => _words.ContainsKey(languageTag);

    public WordStatus Check(string word, string languageTag)
    {
        Lookups++;

        if (!_words.TryGetValue(languageTag, out var known))
        {
            return WordStatus.NoDictionary;
        }

        return known.Contains(word) ? WordStatus.Known : WordStatus.Unknown;
    }

    /// <summary>
    /// Mirrors the machine this was developed against: Windows ships Russian and English
    /// spell-checking, but Ukrainian has to be installed separately.
    /// </summary>
    public static FakeWordValidator RussianAndEnglishOnly() => new FakeWordValidator()
        .With(
            "ru-RU",
            "привет", "спасибо", "работа", "сегодня", "вопрос", "машина", "город", "письмо",
            "деньги", "друг", "рука", "мама", "путь", "сила", "речь", "будь", "ласка", "дом", "мир")
        .With(
            "en-US",
            "hello", "please", "value", "computer", "message", "friend", "code", "test",
            "data", "keyboard", "layout", "week", "city", "money", "rule", "release");
}
