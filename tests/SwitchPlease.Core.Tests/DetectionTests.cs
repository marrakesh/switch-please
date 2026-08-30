using SwitchPlease.Core.Detection;
using Xunit;

namespace SwitchPlease.Core.Tests;

public class DetectionTests
{
    private readonly HeuristicWrongLayoutDetector _detector = new();

    [Theory]
    [InlineData("привет")]
    [InlineData("спасибо")]
    [InlineData("сегодня")]
    [InlineData("работа")]
    [InlineData("почему")]
    public void RussianTypedOnEnglishLayoutIsCorrected(string russian)
    {
        string asTyped = LayoutFixture.TypedInEnglish(russian);

        var verdict = _detector.Evaluate(asTyped, russian);

        Assert.True(
            verdict.ShouldConvert,
            $"expected \"{asTyped}\" to be recognised as \"{russian}\" ({verdict.Reason})");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("please")]
    [InlineData("because")]
    [InlineData("value")]
    public void EnglishTypedOnRussianLayoutIsCorrected(string english)
    {
        string asTyped = LayoutFixture.TypedInRussian(english);

        var verdict = _detector.Evaluate(asTyped, english);

        Assert.True(
            verdict.ShouldConvert,
            $"expected \"{asTyped}\" to be recognised as \"{english}\" ({verdict.Reason})");
    }

    [Theory]
    [InlineData("привет")]
    [InlineData("спасибо")]
    [InlineData("работа")]
    public void CorrectlyTypedRussianIsLeftAlone(string russian)
    {
        string wouldBecome = LayoutFixture.TypedInEnglish(russian);

        var verdict = _detector.Evaluate(russian, wouldBecome);

        Assert.False(verdict.ShouldConvert, $"\"{russian}\" should not be touched ({verdict.Reason})");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("because")]
    [InlineData("please")]
    public void CorrectlyTypedEnglishIsLeftAlone(string english)
    {
        string wouldBecome = LayoutFixture.TypedInRussian(english);

        var verdict = _detector.Evaluate(english, wouldBecome);

        Assert.False(verdict.ShouldConvert, $"\"{english}\" should not be touched ({verdict.Reason})");
    }

    [Theory]
    [InlineData("ab", "фи")]                 // too short to judge
    [InlineData("abc123", "фыс123")]         // identifiers and versions
    [InlineData("user@host", "гыукЁрщые")]   // an address
    [InlineData("src/main", "ыксЁьфшт")]     // a path
    [InlineData("camelCase", "сфьудСфыу")]   // an identifier
    public void RiskyInputIsNeverRewritten(string asTyped, string converted)
    {
        var verdict = _detector.Evaluate(asTyped, converted);

        Assert.False(verdict.ShouldConvert, $"\"{asTyped}\" should be guarded ({verdict.Reason})");
    }

    [Fact]
    public void IdenticalTextIsNotConverted()
    {
        var verdict = _detector.Evaluate("12345", "12345");

        Assert.False(verdict.ShouldConvert);
    }

    [Fact]
    public void DisabledDetectorNeverFires()
    {
        var verdict = DisabledDetector.Instance.Evaluate("ghbdtn", "привет");

        Assert.False(verdict.ShouldConvert);
    }
}
