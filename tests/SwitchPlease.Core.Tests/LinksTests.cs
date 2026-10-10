using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// A link inside converted text is kept as it was typed.
///
/// "pfgjkyb ghjabkm yf https://freelancehunt.com/" is "заполни профиль на" typed on the US
/// layout, followed by a link that was already right. Converting the line rewrote the link
/// as well, into "реезы..акууд...", and the user had to paste it again.
/// </summary>
public class LinksTests
{
    private const string Link = "https://freelancehunt.com/";

    private static ConversionPlanner PlannerFor(FakeLayoutResolver layouts) =>
        new(layouts, LanguageCatalog.BuiltIn, NoWordValidator.Instance);

    [Fact]
    public void ASelectionKeepsItsLinkAndConvertsTheWords()
    {
        string typed = LayoutFixture.TypedInEnglish("заполни профиль на") + " " + Link;

        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection(typed);

        Assert.NotNull(result);
        Assert.Equal("заполни профиль на " + Link, result.Value.Text);
        Assert.Equal(LayoutFixture.Russian, result.Value.Target);
    }

    [Fact]
    public void ALineKeepsItsLinkAndConvertsTheWords()
    {
        // The selection hotkey with nothing selected converts the line as it was recorded.
        string typed = LayoutFixture.TypedInEnglish("заполни профиль на") + " " + Link + " "
            + LayoutFixture.TypedInEnglish("сегодня");
        var buffer = FakeLayoutResolver.Typed(typed, LayoutFixture.English);

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetAll(), LayoutFixture.English);

        Assert.NotNull(plan);
        Assert.Equal("заполни профиль на " + Link + " сегодня", plan.Converted);
        Assert.Equal(LayoutFixture.Russian, plan.TargetLayout);
    }

    [Fact]
    public void TheWordHotkeyLeavesALinkAlone()
    {
        var buffer = FakeLayoutResolver.Typed(LayoutFixture.TypedInEnglish("на") + " " + Link, LayoutFixture.English);
        string? reason = null;

        var plan = PlannerFor(FakeLayoutResolver.EnglishAndRussian)
            .Build(buffer, buffer.GetLastWord(), LayoutFixture.English, r => reason = r);

        Assert.Null(plan);
        Assert.Equal("left alone: no letters outside its links", reason);
    }

    [Fact]
    public void ASelectionThatIsOnlyALinkChangesNothing()
    {
        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection(Link);

        Assert.Equal((Link, null), result);
    }

    [Fact]
    public void ALinkTypedOnTheWrongKeyboardIsConverted()
    {
        // Typed on the Russian layout, "https://" does not look like a link at all, so it is
        // converted with the words around it.
        string meant = "please open https://example.com/ today";
        string typed = LayoutFixture.TypedInRussian(meant);

        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection(typed);

        Assert.NotNull(result);
        Assert.Equal(meant, result.Value.Text);
    }

    [Fact]
    public void AnEmailAddressIsKeptLikeALink()
    {
        string typed = LayoutFixture.TypedInEnglish("напиши мне на") + " ivan.petrenko@example.com";

        var result = PlannerFor(FakeLayoutResolver.EnglishAndRussian).ConvertSelection(typed);

        Assert.NotNull(result);
        Assert.Equal("напиши мне на ivan.petrenko@example.com", result.Value.Text);
    }

    [Theory]
    [InlineData("https://freelancehunt.com/")]
    [InlineData("http://example.com")]
    [InlineData("ftp://files.example.org/pub")]
    [InlineData("www.example.com")]
    [InlineData("WWW.EXAMPLE.COM")]
    [InlineData("(https://example.com/a?b=c),")]
    [InlineData("ivan@example.com")]
    [InlineData("first.last+tag@mail.example.co.uk.")]
    public void TheseAreLinks(string token)
    {
        var links = Links.Find("yf " + token + " gj");

        Assert.NotNull(links);
        Assert.Equal("yf  gj", Links.Remove("yf " + token + " gj", links));
    }

    [Theory]
    [InlineData("ghbdtn")]
    [InlineData("реезыЖ..акууд")]   // "https://freelancehunt" typed on the Russian layout
    [InlineData("@ghbdtn@")]        // a quoted Russian word typed on the US layout
    [InlineData("crfpfk@ghbdtn")]
    [InlineData("name@domain")]
    [InlineData("name@domain.c")]
    [InlineData("://")]
    [InlineData("www.")]
    [InlineData("a/b.c")]
    public void TheseAreNot(string token)
    {
        Assert.Null(Links.Find(token));
    }
}
