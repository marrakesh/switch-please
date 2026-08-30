using System.Reflection;
using System.Text.RegularExpressions;
using SwitchPlease.Core.Localization;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Guards the translations against the failure a compiler cannot catch.
///
/// Completeness is already enforced: every property is <c>required</c>, so a missing string
/// fails the build. What remains is the placeholders. A translation that writes {1} where
/// the original had {0}, or drops one altogether, compiles perfectly and then throws a
/// FormatException the first time that menu is opened -- in a language the author does not
/// use and will never see.
/// </summary>
public partial class TranslationTests(ITestOutputHelper output)
{
    private static readonly PropertyInfo[] StringProperties = typeof(UiStrings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string))
        .ToArray();

    public static TheoryData<string> TranslationNames =>
        new(Named().Select(t => t.Name));

    [Fact]
    public void EveryTranslationUsesTheSamePlaceholdersAsEnglish()
    {
        var problems = new List<string>();

        foreach (var property in StringProperties)
        {
            var expected = PlaceholdersIn(Value(Translations.English, property));

            foreach (var (name, translation) in Named().Skip(1))
            {
                var actual = PlaceholdersIn(Value(translation, property));

                if (!expected.SetEquals(actual))
                {
                    problems.Add(
                        $"{property.Name} [{name}]: expected {Describe(expected)}, found {Describe(actual)}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(TranslationNames))]
    public void NoTranslationIsEmpty(string name)
    {
        var translation = Named().Single(t => t.Name == name).Strings;
        var blank = StringProperties
            .Where(p => string.IsNullOrWhiteSpace(Value(translation, p)))
            .Select(p => p.Name)
            .ToArray();

        Assert.True(blank.Length == 0, $"blank strings in {name}: {string.Join(", ", blank)}");
    }

    [Theory]
    [MemberData(nameof(TranslationNames))]
    public void EveryFormatStringActuallyFormats(string name)
    {
        // Formatting each string with dummy arguments catches a stray unescaped brace as
        // well as a placeholder index nothing supplies.
        var translation = Named().Single(t => t.Name == name).Strings;
        object[] arguments = ["a", "b", "c", "d"];

        foreach (var property in StringProperties)
        {
            string value = Value(translation, property);

            var exception = Record.Exception(() => string.Format(value, arguments));

            Assert.True(exception is null, $"{name}.{property.Name} does not format: {exception?.Message}");
        }
    }

    [Fact]
    public void TranslationsAreNotAccidentalCopiesOfEnglish()
    {
        // A translation left as English is worse than no translation: it looks finished.
        // A handful of strings legitimately match, so this checks the proportion.
        foreach (var (name, translation) in Named().Skip(1))
        {
            int identical = StringProperties.Count(p =>
                string.Equals(Value(translation, p), Value(Translations.English, p), StringComparison.Ordinal));

            double share = (double)identical / StringProperties.Length;
            output.WriteLine($"{name}: {identical}/{StringProperties.Length} strings identical to English");

            Assert.True(share < 0.2, $"{name} looks like it was never translated ({share:P0} identical)");
        }
    }

    [Fact]
    public void EachLanguageNamesItselfInItsOwnLanguage()
    {
        Assert.Equal("English", Translations.English.LanguageName);
        Assert.Equal("Русский", Translations.Russian.LanguageName);
        Assert.Equal("Українська", Translations.Ukrainian.LanguageName);
        Assert.Equal("Deutsch", Translations.German.LanguageName);
        Assert.Equal("Čeština", Translations.Czech.LanguageName);
    }

    [Fact]
    public void TheRegistryListsEveryTranslationExactlyOnce()
    {
        // The registry is what the menu, the setting and these tests all read. A language
        // whose strings exist but which was never added to it is invisible everywhere.
        var declared = typeof(Translations)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(UiStrings))
            .Select(p => (UiStrings)p.GetValue(null)!)
            .ToArray();

        Assert.Equal(declared.Length, Translations.All.Count);
        Assert.All(declared, strings => Assert.Contains(Translations.All, l => ReferenceEquals(l.Strings, strings)));

        Assert.Equal(
            Translations.All.Select(l => l.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Translations.All.Count);
    }

    [Fact]
    public void EveryLanguageIsReachableByItsTagAndByWindowsIdentifier()
    {
        foreach (var language in Translations.All)
        {
            Assert.Same(language, Translations.Find(language.Tag));
            Assert.Same(language, Translations.Find(language.Tag.ToUpperInvariant()));
            Assert.Same(language, Translations.ForWindowsLanguage(language.WindowsPrimaryLanguage));
        }

        Assert.Null(Translations.Find("auto"));
        Assert.Null(Translations.Find(null));
        Assert.Null(Translations.ForWindowsLanguage(0x3FF));
    }

    /// <summary>
    /// Every shipped language, read off the registry rather than listed here, so a language
    /// added to Translations.All is covered by all of these checks without touching this
    /// file. English comes first: it is the reference the rest are compared to.
    /// </summary>
    private static (string Name, UiStrings Strings)[] Named() =>
        [.. Translations.All.Select(l => (l.Tag, l.Strings))];

    private static string Value(UiStrings strings, PropertyInfo property) =>
        (string)property.GetValue(strings)!;

    private static HashSet<string> PlaceholdersIn(string value) =>
        [.. PlaceholderPattern().Matches(value).Select(m => m.Value)];

    private static string Describe(HashSet<string> placeholders) =>
        placeholders.Count == 0 ? "none" : string.Join(" ", placeholders.Order());

    [GeneratedRegex(@"\{\d+(?::[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
