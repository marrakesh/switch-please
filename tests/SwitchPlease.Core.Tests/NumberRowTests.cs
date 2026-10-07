using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Layouts;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Czech, Slovak and Hungarian put accented letters on the number row, so a Czech word typed
/// on the US layout arrives with digits in it: "děkuji" as "d2kuji". The word hotkey has to
/// read those digits as the letters they were meant to be.
///
/// It did not. Every digit was passed over like a space, so "m2sto" was judged on its
/// letters alone and found to be good English, and nothing said that the one layout turning
/// the digit back into a letter was the one meant. Measured on the real Windows layouts with
/// no Czech dictionary installed: "m2sto" and "p59li3" were refused as already reading better
/// (0.90 against 0.45, 0.96 against 0.05), and with Russian installed as well "d2kuji" became
/// "в2лгош".
/// </summary>
public class NumberRowTests
{
    private static readonly LayoutInfo English = LayoutFixture.English;
    private static readonly LayoutInfo Czech = LayoutFixture.Czech;

    private static readonly FakeLayoutResolver EnglishAndCzech = new(English, Czech);

    /// <summary>The machine this was measured on: a Czech layout next to the Cyrillic pair.</summary>
    private static readonly FakeLayoutResolver CzechAndCyrillic =
        new(English, Czech, LayoutFixture.Russian, LayoutFixture.Ukrainian);

    private static readonly FakeLayoutResolver CyrillicOnly =
        new(English, LayoutFixture.Russian, LayoutFixture.Ukrainian);

    /// <summary>Czech words as they land on the US layout, with the accent on either side or inside.</summary>
    public static TheoryData<string, string> CzechWords => new()
    {
        { "d2kuji", "děkuji" },
        { "m2sto", "město" },
        { "p59li3", "příliš" },
        { "dob5e", "dobře" },
        { "4as", "čas" },
        { "tak0", "také" },
        { "6e", "že" },
    };

    /// <summary>
    /// What Windows ships: Russian and English spell-checking, plus Czech for those who
    /// installed it. The words are the ones the tests expect back, and nothing else.
    /// </summary>
    private static FakeWordValidator WithCzechDictionary() => FakeWordValidator.RussianAndEnglishOnly()
        .With("cs-CZ", "děkuji", "město", "příliš", "dobře", "čas", "také", "že");

    [Theory]
    [MemberData(nameof(CzechWords))]
    public void WithoutACzechDictionaryTheNumberRowStillSaysCzech(string typed, string meant)
    {
        // Nothing installed can judge Czech, and English marks down every háček. With the
        // digit counted as what it is -- no alphabet's letter -- the word on screen reads no
        // better than the Czech one, so there is nothing to protect and the press goes through.
        var plan = Plan(EnglishAndCzech, typed, NoWordValidator.Instance, out string? reason);

        Assert.True(plan is not null, $"\"{typed}\" was refused: {reason}");
        Assert.Equal(meant, plan.Converted);
        Assert.Equal(Czech, plan.TargetLayout);
    }

    [Theory]
    [MemberData(nameof(CzechWords))]
    public void ALayoutThatTurnsTheDigitIntoALetterBeatsOneThatKeepsIt(string typed, string meant)
    {
        // Russian leaves the 2 in "в2лгош" and has its letters judged by a real model; Czech
        // has only an alphabet. The digit is what settles it.
        var plan = Plan(CzechAndCyrillic, typed, FakeWordValidator.RussianAndEnglishOnly(), out string? reason);

        Assert.True(plan is not null, $"\"{typed}\" was refused: {reason}");
        Assert.Equal(meant, plan.Converted);
        Assert.Equal(Czech, plan.TargetLayout);
    }

    [Theory]
    [MemberData(nameof(CzechWords))]
    public void ACzechDictionaryConfirmsTheSameAnswer(string typed, string meant)
    {
        var plan = Plan(CzechAndCyrillic, typed, WithCzechDictionary(), out string? reason);

        Assert.True(plan is not null, $"\"{typed}\" was refused: {reason}");
        Assert.Equal(meant, plan.Converted);
    }

    [Theory]
    [InlineData("město")]
    [InlineData("příliš")]
    [InlineData("čas")]
    [InlineData("dobře")]
    public void CorrectCzechSurvivesAStrayDoubleTap(string word)
    {
        // The other direction of the same flaw. Typed correctly on the Czech layout, "město"
        // reads as "m2sto" on the US one, which used to score 0.90 with its digit passed over
        // -- close enough to the dictionary's 1.0 that a stray double tap rewrote the word.
        var buffer = FakeLayoutResolver.Typed(word, Czech);
        string? reason = null;

        var plan = PlannerFor(CzechAndCyrillic, WithCzechDictionary())
            .Build(buffer, buffer.GetAll(), Czech, r => reason = r);

        Assert.True(plan is null, $"\"{word}\" would become \"{plan?.Converted}\"");
        Assert.Contains("left alone", reason);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("utf8")]
    [InlineData("covid19")]
    public void DigitsNoLayoutTurnsIntoLettersStillSayNothing(string identifier)
    {
        // Russian and Ukrainian keep their digits. A digit both readings share is no evidence
        // for either, and counting it against both alike would narrow the margin that keeps
        // these from a stray double tap; they must be judged exactly as before.
        var plan = Plan(CyrillicOnly, identifier, FakeWordValidator.RussianAndEnglishOnly(), out string? reason);

        Assert.True(plan is null, $"\"{identifier}\" would become \"{plan?.Converted}\"");
        Assert.Contains("left alone", reason);
    }

    [Fact]
    public void ASelectionFindsTheCzechReadingToo()
    {
        // A selection has no scan codes, only characters, but the same digits are there to
        // read. Only where they are a good part of the word, though: a pair of layouts is
        // tried on a selection only if it changes half of it, and Czech QWERTY differs from
        // US on the number row alone. "p59li3" is three keys in six; "d2kuji", one in six,
        // never gets as far as being judged.
        var map = PlannerFor(CzechAndCyrillic, FakeWordValidator.RussianAndEnglishOnly()).ChooseMapFor("p59li3");

        Assert.NotNull(map);
        Assert.Equal("příliš", map.Convert("p59li3"));
    }

    [Fact]
    public void ANumberOnItsOwnIsNoEvidenceForCzech()
    {
        // Every number in a line would otherwise vote for the Czech layout, which has letters
        // on all those keys, and a Russian sentence with a year in it would come out Czech.
        string typed = LayoutFixture.TypedInEnglish("привет") + " 2024";

        var map = PlannerFor(CzechAndCyrillic, FakeWordValidator.RussianAndEnglishOnly()).ChooseMapFor(typed);

        Assert.NotNull(map);
        Assert.Equal("привет 2024", map.Convert(typed));
    }

    [Theory]
    [InlineData("2024")]
    [InlineData("150")]
    [InlineData("07.10.2026")]
    [InlineData("3,50")]
    public void TheWordHotkeyLeavesANumberAlone(string number)
    {
        // A number has no letters for any language to judge, and it used to go through as a
        // plain toggle, the path meant for an alphabet nothing models. With Czech installed
        // that is a stray double tap away from "ěéěč" for "2024" and "+řé" for "150".
        foreach (var layouts in new[] { EnglishAndCzech, CzechAndCyrillic })
        {
            var plan = Plan(layouts, number, FakeWordValidator.RussianAndEnglishOnly(), out string? reason);

            Assert.True(plan is null, $"\"{number}\" would become \"{plan?.Converted}\"");
            Assert.Equal("left alone: no letters in it", reason);
        }
    }

    [Theory]
    [InlineData("3959", "šíří")]
    [InlineData("49", "čí")]
    public void AWordOfAccentsAloneIsTakenForTheNumberItLooksLike(string typed, string meant)
    {
        // The price of the test above, paid on purpose. On the US layout these are the very
        // keys of a number, and a Czech dictionary that knows the word changes nothing: "49"
        // and "čí" are typed the same way, and both are what somebody meant.
        var withThem = FakeWordValidator.RussianAndEnglishOnly().With("cs-CZ", meant);

        var plan = Plan(CzechAndCyrillic, typed, withThem, out string? reason);

        Assert.True(plan is null, $"\"{typed}\" would become \"{plan?.Converted}\"");
        Assert.Equal("left alone: no letters in it", reason);
    }

    [Theory]
    [InlineData("3959", "šíří")]
    [InlineData("49", "čí")]
    public void ASelectionStillConvertsOne(string typed, string meant)
    {
        // Selecting a number and asking for it to be converted is no accident, so the
        // selection is where such a word can still be had.
        var map = PlannerFor(CzechAndCyrillic, FakeWordValidator.RussianAndEnglishOnly()).ChooseMapFor(typed);

        Assert.NotNull(map);
        Assert.Equal(meant, map.Convert(typed));
    }

    private static ConversionPlanner PlannerFor(FakeLayoutResolver layouts, IWordValidator validator) =>
        new(layouts, LayoutFixture.LanguagesOf([.. layouts.InstalledLayouts]), validator);

    /// <summary>What the word hotkey does with <paramref name="typed"/>, typed on the US layout.</summary>
    private static ConversionPlan? Plan(
        FakeLayoutResolver layouts,
        string typed,
        IWordValidator validator,
        out string? reason)
    {
        var buffer = FakeLayoutResolver.Typed(typed, English);
        string? explained = null;

        var plan = PlannerFor(layouts, validator).Build(buffer, buffer.GetAll(), English, r => explained = r);

        reason = explained;
        return plan;
    }
}
