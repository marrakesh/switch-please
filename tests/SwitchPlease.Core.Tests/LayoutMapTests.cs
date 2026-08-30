using Xunit;

namespace SwitchPlease.Core.Tests;

public class LayoutMapTests
{
    [Theory]
    [InlineData("ghbdtn", "привет")]
    [InlineData("cgfcb,j", "спасибо")]
    [InlineData("rfr ltkf", "как дела")]
    public void LatinGibberishReadsAsRussian(string typed, string expected)
    {
        Assert.Equal(expected, LayoutFixture.EnglishToRussian.Convert(typed));
    }

    [Theory]
    [InlineData("руддщ", "hello")]
    [InlineData("црфе", "what")]
    public void CyrillicGibberishReadsAsEnglish(string typed, string expected)
    {
        Assert.Equal(expected, LayoutFixture.RussianToEnglish.Convert(typed));
    }

    [Fact]
    public void ConversionRoundTrips()
    {
        const string Original = "привет как дела";

        string typed = LayoutFixture.RussianToEnglish.Convert(Original);
        string back = LayoutFixture.EnglishToRussian.Convert(typed);

        Assert.Equal(Original, back);
    }

    [Fact]
    public void CaseIsPreserved()
    {
        Assert.Equal("Привет", LayoutFixture.EnglishToRussian.Convert("Ghbdtn"));
    }

    [Fact]
    public void UnmappedCharactersPassThrough()
    {
        Assert.Equal("привет123", LayoutFixture.EnglishToRussian.Convert("ghbdtn123"));
    }

    [Fact]
    public void TextWithNothingToMapIsReturnedUnchanged()
    {
        const string Digits = "12345";

        Assert.Same(Digits, LayoutFixture.EnglishToRussian.Convert(Digits));
    }

    [Fact]
    public void CoverageDistinguishesTheUsefulDirection()
    {
        const string Latin = "ghbdtn";

        double forward = LayoutFixture.EnglishToRussian.CoverageOf(Latin);
        double backward = LayoutFixture.RussianToEnglish.CoverageOf(Latin);

        Assert.True(forward > backward, $"forward={forward}, backward={backward}");
    }
}
