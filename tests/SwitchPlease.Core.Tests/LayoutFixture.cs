using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Layouts;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// A fixed QWERTY/ЙЦУКЕН table so the core tests can run anywhere, without Windows or an
/// installed Russian layout. At runtime the real map is derived from the OS instead.
/// </summary>
internal static class LayoutFixture
{
    // The number row last, so the scan codes of every other key stay what they were.
    private const string LatinRow = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`1234567890";
    private const string CyrillicRow = "йцукенгшщзхъфывапролджэячсмитьбю.ё1234567890";

    /// <summary>
    /// Czech QWERTY: the US letters, with the accented ones where the US layout has its
    /// digits, and "ú" and "ů" on two punctuation keys. That number row is the whole
    /// difficulty: "město" typed on the US layout arrives as "m2sto", a word with a digit in
    /// it, and no layout in the Latin/Cyrillic fixture ever turns a digit into a letter.
    /// </summary>
    private static readonly string CzechRow = LatinRow
        .Replace("1234567890", "+ěščřžýáíé", StringComparison.Ordinal)
        .Replace('[', 'ú')
        .Replace(';', 'ů');

    /// <summary>
    /// The Ukrainian layout is the Russian one with three keys changed. That near-identity
    /// is the whole difficulty: text typed in the wrong one of the two still looks like
    /// ordinary Cyrillic.
    /// </summary>
    private static readonly string UkrainianRow = CyrillicRow
        .Replace('ъ', 'ї')
        .Replace('ы', 'і')
        .Replace('э', 'є');

    public static LayoutInfo English { get; } = new(1, 0x0409, "en", "English (US)");

    public static LayoutInfo Russian { get; } = new(2, 0x0419, "ru", "Русская");

    public static LayoutInfo Ukrainian { get; } = new(3, 0x0422, "uk", "Українська");

    public static LayoutInfo Czech { get; } = new(4, 0x0405, "cs", "Čeština");

    public static LayoutMap RussianToUkrainian { get; } = BuildBetween(Russian, Ukrainian);

    public static LayoutMap UkrainianToRussian { get; } = BuildBetween(Ukrainian, Russian);

    /// <summary>Ukrainian text as it lands when the Russian layout was left active.</summary>
    public static string TypedWithRussianLayout(string ukrainian) => UkrainianToRussian.Convert(ukrainian);

    public static LayoutMap EnglishToRussian { get; } = Build(English, Russian, reverse: false);

    public static LayoutMap RussianToEnglish { get; } = Build(Russian, English, reverse: true);

    /// <summary>Text as it lands when Russian words are typed with the US layout active.</summary>
    public static string TypedInEnglish(string russian) => RussianToEnglish.Convert(russian);

    /// <summary>Text as it lands when English words are typed with the Russian layout active.</summary>
    public static string TypedInRussian(string english) => EnglishToRussian.Convert(english);

    /// <summary>
    /// The characters a layout types, in scan-code order. This is the same statement the
    /// real code gets out of Windows one key at a time.
    /// </summary>
    public static string RowOf(LayoutInfo layout)
    {
        if (ReferenceEquals(layout, Russian))
        {
            return CyrillicRow;
        }

        if (ReferenceEquals(layout, Czech))
        {
            return CzechRow;
        }

        return ReferenceEquals(layout, Ukrainian) ? UkrainianRow : LatinRow;
    }

    /// <summary>
    /// The languages a machine with these layouts would model, built the way the real
    /// catalog is: from the characters each layout types. Czech has no built-in model, so it
    /// becomes a profile that knows its alphabet and nothing more.
    /// </summary>
    public static LanguageCatalog LanguagesOf(params LayoutInfo[] layouts) =>
        LanguageCatalog.FromLayouts(layouts.Select(layout => new LayoutLanguage(
            TagOf(layout),
            layout.CultureName,
            RowOf(layout) + RowOf(layout).ToUpperInvariant())));

    private static string TagOf(LayoutInfo layout) => layout.CultureName switch
    {
        "en" => "en-US",
        "ru" => "ru-RU",
        "uk" => "uk-UA",
        "cs" => "cs-CZ",
        _ => layout.CultureName,
    };

    /// <summary>The table for any ordered pair of the fixture layouts.</summary>
    public static LayoutMap MapFor(LayoutInfo source, LayoutInfo target)
    {
        if (ReferenceEquals(source, Czech) || ReferenceEquals(target, Czech))
        {
            return BuildBetween(source, target);
        }

        if (ReferenceEquals(source, English))
        {
            return ReferenceEquals(target, Russian) ? EnglishToRussian : EnglishToUkrainian;
        }

        if (ReferenceEquals(source, Russian))
        {
            return ReferenceEquals(target, English) ? RussianToEnglish : RussianToUkrainian;
        }

        return ReferenceEquals(target, English) ? UkrainianToEnglish : UkrainianToRussian;
    }

    public static LayoutMap EnglishToUkrainian { get; } = BuildLatinToCyrillic(English, Ukrainian);

    public static LayoutMap UkrainianToEnglish { get; } = BuildCyrillicToLatin(Ukrainian, English);

    private static LayoutMap BuildLatinToCyrillic(LayoutInfo source, LayoutInfo target)
    {
        string to = RowOf(target);
        var builder = new LayoutMap.Builder(source, target);

        for (int i = 0; i < LatinRow.Length && i < to.Length; i++)
        {
            builder.Add(LatinRow[i], to[i]);
            builder.Add(char.ToUpperInvariant(LatinRow[i]), char.ToUpperInvariant(to[i]));
        }

        return builder.Build();
    }

    private static LayoutMap BuildCyrillicToLatin(LayoutInfo source, LayoutInfo target)
    {
        string from = RowOf(source);
        var builder = new LayoutMap.Builder(source, target);

        for (int i = 0; i < from.Length && i < LatinRow.Length; i++)
        {
            builder.Add(from[i], LatinRow[i]);
            builder.Add(char.ToUpperInvariant(from[i]), char.ToUpperInvariant(LatinRow[i]));
        }

        return builder.Build();
    }

    private static LayoutMap BuildBetween(LayoutInfo source, LayoutInfo target)
    {
        string from = RowOf(source);
        string to = RowOf(target);

        var builder = new LayoutMap.Builder(source, target);

        for (int i = 0; i < from.Length; i++)
        {
            builder.Add(from[i], to[i]);
            builder.Add(char.ToUpperInvariant(from[i]), char.ToUpperInvariant(to[i]));
        }

        return builder.Build();
    }

    private static LayoutMap Build(LayoutInfo source, LayoutInfo target, bool reverse)
    {
        var builder = new LayoutMap.Builder(source, target);

        for (int i = 0; i < LatinRow.Length; i++)
        {
            char latin = LatinRow[i];
            char cyrillic = CyrillicRow[i];

            if (reverse)
            {
                builder.Add(cyrillic, latin);
                builder.Add(char.ToUpperInvariant(cyrillic), char.ToUpperInvariant(latin));
            }
            else
            {
                builder.Add(latin, cyrillic);
                builder.Add(char.ToUpperInvariant(latin), char.ToUpperInvariant(cyrillic));
            }
        }

        return builder.Build();
    }
}
