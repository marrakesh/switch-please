using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The catalog is what ties detection to the machine: the languages it reasons about come
/// from the keyboard layouts the user installed, not from a list in the source. Adding a
/// Czech layout in Windows should make the switcher aware of Czech.
/// </summary>
public class LanguageCatalogTests(ITestOutputHelper output)
{
    private const string UsCharacters = "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";

    private const string CzechCharacters =
        "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNMěščřžýáíéúůĚŠČŘŽÝÁÍÉÚŮ";

    private const string RussianCharacters =
        "йцукенгшщзхъфывапролджэёячсмитьбюЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЁЯЧСМИТЬБЮ";

    [Fact]
    public void ALayoutWithNoBuiltInModelStillBecomesALanguage()
    {
        var catalog = LanguageCatalog.FromLayouts([
            new LayoutLanguage("en-US", "en", UsCharacters),
            new LayoutLanguage("cs-CZ", "cs", CzechCharacters),
        ]);

        var czech = catalog.Profiles.SingleOrDefault(p => p.LanguageTag == "cs-CZ");

        Assert.NotNull(czech);
        Assert.Equal(Script.Latin, czech.Script);
        Assert.False(czech.HasStatisticalModel);

        output.WriteLine($"czech alphabet from the layout: {string.Concat(CzechAlphabetOf(czech))}");
    }

    [Fact]
    public void TheAlphabetComesFromWhatTheLayoutTypes()
    {
        var catalog = LanguageCatalog.FromLayouts([new LayoutLanguage("cs-CZ", "cs", CzechCharacters)]);
        var czech = catalog.Profiles.Single();

        // Accented Czech letters belong to it; Cyrillic does not.
        Assert.Equal(1.0, czech.AlphabetCoverage("příliš"));
        Assert.Equal(0.0, czech.AlphabetCoverage("привет"));

        // An alphabet read off a layout misses letters that need a dead key -- Czech "ť" is
        // typed as caron then t, so it never appears among the characters a single key
        // produces. Coverage therefore has to be judged with room to spare rather than
        // demanded in full, which is why the familiarity threshold sits at 0.8.
        double withDeadKeyLetter = czech.AlphabetCoverage("žluťoučký");

        output.WriteLine($"coverage of a word containing a dead-key letter: {withDeadKeyLetter:F2}");
        Assert.InRange(withDeadKeyLetter, 0.8, 1.0);
    }

    [Fact]
    public void HandWrittenModelsWinOverGeneratedOnes()
    {
        var catalog = LanguageCatalog.FromLayouts([new LayoutLanguage("ru-RU", "ru", RussianCharacters)]);

        var russian = catalog.Profiles.Single(p => p.LanguageTag == "ru-RU");

        Assert.True(russian.HasStatisticalModel);
        Assert.Same(LanguageProfile.Russian, russian);
    }

    [Fact]
    public void RussianAndUkrainianAlwaysArriveTogether()
    {
        // Only the Russian layout is installed, but judging Cyrillic without the Ukrainian
        // alphabet is what let "привыт" pass as a real word.
        var catalog = LanguageCatalog.FromLayouts([new LayoutLanguage("ru-RU", "ru", RussianCharacters)]);

        Assert.Contains(catalog.Profiles, p => p.LanguageTag == "uk-UA");
    }

    [Fact]
    public void ALayoutWithoutADictionaryOrAModelIsNotJudged()
    {
        var catalog = LanguageCatalog.FromLayouts([
            new LayoutLanguage("en-US", "en", UsCharacters),
            new LayoutLanguage("cs-CZ", "cs", CzechCharacters),
        ]);

        // Czech is now a known alphabet, but nothing here can say whether "příliš" reads
        // naturally, so the switcher must still decline to act on it.
        Assert.False(catalog.IsFamiliar("příliš", NoWordValidator.Instance));
    }

    [Fact]
    public void ADictionaryMakesThatSameLayoutJudgeable()
    {
        var catalog = LanguageCatalog.FromLayouts([
            new LayoutLanguage("en-US", "en", UsCharacters),
            new LayoutLanguage("cs-CZ", "cs", CzechCharacters),
        ]);

        var withCzech = new FakeWordValidator().With("cs-CZ", "příliš", "žluťoučký");

        Assert.True(catalog.IsFamiliar("příliš", withCzech));
        Assert.Equal(1.0, catalog.Evaluate("příliš", withCzech).Score);

        // A Czech word absent from the dictionary is not condemned for it.
        double unknown = catalog.Evaluate("kůň", withCzech).Score;
        output.WriteLine($"unknown czech word: {unknown:F3}");
        Assert.InRange(unknown, 0.01, 0.99);
    }

    [Fact]
    public void DuplicateLayoutsForOneLanguageAreCollapsed()
    {
        var catalog = LanguageCatalog.FromLayouts([
            new LayoutLanguage("en-US", "en", UsCharacters),
            new LayoutLanguage("en-US", "en", UsCharacters),
        ]);

        Assert.Single(catalog.Profiles, p => p.LanguageTag == "en-US");
    }

    [Fact]
    public void AnEmptyLayoutListFallsBackToTheBuiltInLanguages()
    {
        var catalog = LanguageCatalog.FromLayouts([]);

        Assert.Equal(LanguageCatalog.BuiltIn.Profiles.Count, catalog.Profiles.Count);
    }

    [Fact]
    public void ScriptsAreKeptApart()
    {
        var catalog = LanguageCatalog.FromLayouts([
            new LayoutLanguage("en-US", "en", UsCharacters),
            new LayoutLanguage("cs-CZ", "cs", CzechCharacters),
            new LayoutLanguage("ru-RU", "ru", RussianCharacters),
        ]);

        Assert.All(catalog.ForScript(Script.Latin), p => Assert.Equal(Script.Latin, p.Script));
        Assert.All(catalog.ForScript(Script.Cyrillic), p => Assert.Equal(Script.Cyrillic, p.Script));
        Assert.Contains(catalog.ForScript(Script.Latin), p => p.LanguageTag == "cs-CZ");
    }

    private static IEnumerable<char> CzechAlphabetOf(LanguageProfile profile) =>
        CzechCharacters.ToLowerInvariant().Distinct().Where(c => profile.AlphabetCoverage(c.ToString()) > 0);
}
