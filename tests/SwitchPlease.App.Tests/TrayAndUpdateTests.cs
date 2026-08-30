using SwitchPlease.App.Localization;
using SwitchPlease.Core.Localization;
using Xunit;

namespace SwitchPlease.App.Tests;

/// <summary>
/// The parts of the application layer that are neither a window nor the settings file: the
/// tray icon it draws for itself, the language it decides to speak, and the version
/// comparison behind the update check.
/// </summary>
public class TrayAndUpdateTests
{
    [Fact]
    public void TheTrayIconIsDrawnAtTheSizeWindowsAsksFor()
    {
        Sta.Run(() =>
        {
            using var icon = TrayIconFactory.Create("RU", active: true);

            Assert.Equal(32, icon.Width);
            Assert.Equal(32, icon.Height);
        });
    }

    [Fact]
    public void EveryFrameOfTheCorrectionAnimationDiffersFromTheOneBeforeIt()
    {
        // The animation is a carriage sweeping across the tag. If two consecutive frames
        // came out identical the icon would visibly stutter, and nothing else would notice.
        Sta.Run(() =>
        {
            var seen = new List<string>();

            for (int frame = 0; frame <= TrayIconFactory.AnimationFrames; frame++)
            {
                using var icon = TrayIconFactory.Create("RU", active: true, frame);
                using var bitmap = icon.ToBitmap();

                seen.Add(Fingerprint(bitmap));
            }

            Assert.Equal(seen.Count, seen.Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public void TheRestingIconDiffersFromTheAnimatedOnesAndFromTheDisabledOne()
    {
        Sta.Run(() =>
        {
            using var resting = TrayIconFactory.Create("RU", active: true);
            using var midway = TrayIconFactory.Create("RU", active: true, TrayIconFactory.AnimationFrames / 2);
            using var off = TrayIconFactory.Create("RU", active: false);
            using var other = TrayIconFactory.Create("EN", active: true);

            string a = Fingerprint(resting.ToBitmap());
            string b = Fingerprint(midway.ToBitmap());
            string c = Fingerprint(off.ToBitmap());
            string d = Fingerprint(other.ToBitmap());

            Assert.NotEqual(a, b);
            Assert.NotEqual(a, c);
            Assert.NotEqual(a, d);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("RUS")]
    [InlineData("русский")]
    public void AnOddLayoutTagStillProducesAnIcon(string tag)
    {
        // The tag comes from whatever Windows calls the layout, which is not always two
        // latin letters. An exception here would take the tray icon down on the tick.
        Sta.Run(() =>
        {
            using var icon = TrayIconFactory.Create(tag, active: true);

            Assert.NotNull(icon);
        });
    }

    [Theory]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    public void OnlyAGenuinelyNewerReleaseCountsAsAnUpdate(string latest, string current, bool expected) =>
        Assert.Equal(expected, UpdateCheck.IsNewer(latest, current));

    [Theory]
    [InlineData("not a version")]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("latest")]
    public void AReleaseNamedSomethingUnreadableIsNotAnUpdate(string latest)
    {
        // Telling somebody their copy is out of date because a tag could not be parsed is
        // worse than saying nothing.
        Assert.False(UpdateCheck.IsNewer(latest, "1.0.0"));
    }

    [Theory]
    [InlineData("1.2.0-beta", "1.2.0")]
    [InlineData("1.2.0+build7", "1.2.0")]
    [InlineData("1.2.0 rc1", "1.2.0")]
    [InlineData("1.2.0", "1.2.0")]
    public void PreReleaseSuffixesAreTrimmedBeforeComparing(string tag, string expected) =>
        Assert.Equal(expected, UpdateCheck.Normalise(tag));

    [Fact]
    public void APreReleaseOfTheSameVersionIsNotOfferedAsAnUpgrade() =>
        Assert.False(UpdateCheck.IsNewer("1.0.0-beta", "1.0.0"));

    [Fact]
    public void ChoosingALanguageOverridesTheWindowsSetting()
    {
        foreach (var language in Translations.All)
        {
            Localizer.Use(language.Tag);

            Assert.Equal(language.Tag, Localizer.Selected);
            Assert.Same(language, Localizer.Effective);
            Assert.Same(language.Strings, Localizer.Text);
        }

        Localizer.Use("en");
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("")]
    [InlineData("klingon")]
    [InlineData(null)]
    public void AnythingUnrecognisedFallsBackToFollowingWindows(string? setting)
    {
        Localizer.Use(setting);

        // Whatever Windows is set to, the result has to be a language that actually ships,
        // and the setting written back has to be the one that means "follow Windows".
        Assert.Equal(Localizer.Auto, Localizer.Selected);
        Assert.Contains(Localizer.Effective, Translations.All);

        Localizer.Use("en");
    }

    [Fact]
    public void TheLanguageMenuOffersEveryShippedTranslation()
    {
        Assert.Equal(Translations.All.Count, Localizer.Available.Count);
        Assert.All(Localizer.Available, language => Assert.False(string.IsNullOrWhiteSpace(language.Name)));
    }

    /// <summary>A cheap summary of what a bitmap looks like, enough to tell two icons apart.</summary>
    private static string Fingerprint(Bitmap bitmap)
    {
        using (bitmap)
        {
            var builder = new System.Text.StringBuilder();

            for (int y = 0; y < bitmap.Height; y += 2)
            {
                for (int x = 0; x < bitmap.Width; x += 2)
                {
                    builder.Append(bitmap.GetPixel(x, y).ToArgb().ToString("X8", null));
                }
            }

            return builder.ToString();
        }
    }
}
