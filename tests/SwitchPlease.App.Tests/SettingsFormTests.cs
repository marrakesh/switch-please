using SwitchPlease.App.Localization;
using SwitchPlease.Core.Config;
using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.App.Tests;

/// <summary>
/// The settings window, driven the way the tray drives it.
///
/// The bug this guards against is dull and certain: someone adds a setting, puts a control
/// on the form for it, and forgets the line in <c>ApplyTo</c>. Nothing fails to compile,
/// nothing throws, and the setting simply never saves. It is invisible in review and only
/// noticed by a user wondering why the thing they changed keeps coming back.
///
/// So this builds the window from one set of settings and reads it back into another, which
/// is exactly the trip the real dialog makes.
/// </summary>
public class SettingsFormTests
{
    private static AppSettings Distinctive() => new()
    {
        AutoDetectSensitivity = 0.42,
        CorrectionDelayMilliseconds = 33,
        DoubleTapWindowMilliseconds = 321,
        DoubleTapHoldMilliseconds = 222,
        MinimumAutoWordLength = 5,
        RespectPasswordFields = false,
        PauseInFullscreenApps = false,
        ShowLayoutAtCaret = true,
        LayoutIndicatorMilliseconds = 1700,
        LogTextContent = true,
        CheckForUpdates = true,
        TypewriterMillisecondsPerCharacter = 20,
        ExcludedProcesses = ["one.exe", "two.exe"],
        NeverCorrectWords = ["ntcn", "привыт"],
    };

    [Fact]
    public void EverySettingOnTheFormMakesItBackOut()
    {
        Sta.Run(() =>
        {
            Localizer.Use("en");

            var original = Distinctive();
            using var form = new SettingsForm(original);

            var applied = new AppSettings();
            form.ApplyTo(applied);

            Assert.Equal(original.AutoDetectSensitivity, applied.AutoDetectSensitivity);
            Assert.Equal(original.CorrectionDelayMilliseconds, applied.CorrectionDelayMilliseconds);
            Assert.Equal(original.DoubleTapWindowMilliseconds, applied.DoubleTapWindowMilliseconds);
            Assert.Equal(original.DoubleTapHoldMilliseconds, applied.DoubleTapHoldMilliseconds);
            Assert.Equal(original.MinimumAutoWordLength, applied.MinimumAutoWordLength);
            Assert.Equal(original.RespectPasswordFields, applied.RespectPasswordFields);
            Assert.Equal(original.PauseInFullscreenApps, applied.PauseInFullscreenApps);
            Assert.Equal(original.ShowLayoutAtCaret, applied.ShowLayoutAtCaret);
            Assert.Equal(original.LayoutIndicatorMilliseconds, applied.LayoutIndicatorMilliseconds);
            Assert.Equal(original.LogTextContent, applied.LogTextContent);
            Assert.Equal(original.CheckForUpdates, applied.CheckForUpdates);
            Assert.Equal(original.ExcludedProcesses, applied.ExcludedProcesses);
            Assert.Equal(original.NeverCorrectWords, applied.NeverCorrectWords);

            // Kept as a speed rather than a flag, so switching it on must not reset a value
            // the user chose by hand.
            Assert.Equal(original.TypewriterMillisecondsPerCharacter, applied.TypewriterMillisecondsPerCharacter);
        });
    }

    [Fact]
    public void TheDefaultsSurviveTheTripUnchanged()
    {
        Sta.Run(() =>
        {
            Localizer.Use("en");

            var defaults = new AppSettings();
            using var form = new SettingsForm(defaults);

            var applied = new AppSettings { AutoDetectSensitivity = 0.9, MinimumAutoWordLength = 9 };
            form.ApplyTo(applied);

            Assert.Equal(defaults.AutoDetectSensitivity, applied.AutoDetectSensitivity);
            Assert.Equal(defaults.MinimumAutoWordLength, applied.MinimumAutoWordLength);
            Assert.Equal(defaults.ExcludedProcesses, applied.ExcludedProcesses);
            Assert.Equal(0, applied.TypewriterMillisecondsPerCharacter);
        });
    }

    [Fact]
    public void ValuesFromAnOlderFileAreClampedRatherThanRefused()
    {
        Sta.Run(() =>
        {
            Localizer.Use("en");

            // A hand-edited or older file can hold anything. The controls have ranges, and
            // handing one a value outside its range throws -- which would mean the settings
            // window refusing to open at all.
            var wild = new AppSettings
            {
                AutoDetectSensitivity = 5.0,
                CorrectionDelayMilliseconds = 100_000,
                DoubleTapWindowMilliseconds = 1,
                DoubleTapHoldMilliseconds = 0,
                MinimumAutoWordLength = -3,
                LayoutIndicatorMilliseconds = 0,
            };

            using var form = new SettingsForm(wild);

            var applied = new AppSettings();
            form.ApplyTo(applied);

            Assert.InRange(applied.AutoDetectSensitivity, 0.05, 0.95);
            Assert.InRange(applied.CorrectionDelayMilliseconds, 0, 200);
            Assert.InRange(applied.DoubleTapWindowMilliseconds, 120, 2000);
            Assert.InRange(applied.DoubleTapHoldMilliseconds, 80, 2000);
            Assert.InRange(applied.MinimumAutoWordLength, 2, 10);
            Assert.InRange(applied.LayoutIndicatorMilliseconds, 200, 5000);
        });
    }

    [Fact]
    public void TheExclusionListIsTidiedOnTheWayOut()
    {
        Sta.Run(() =>
        {
            Localizer.Use("en");

            var settings = new AppSettings
            {
                ExcludedProcesses = ["  KeePass.exe  ", "chrome.exe", "chrome.exe", "", "   "],
            };

            using var form = new SettingsForm(settings);

            var applied = new AppSettings();
            form.ApplyTo(applied);

            Assert.Equal(["keepass.exe", "chrome.exe"], applied.ExcludedProcesses);
        });
    }

    [Fact]
    public void TheWindowOpensInEveryShippedLanguage()
    {
        // A translation longer than the English original used to run off the edge of the
        // control it belonged to. It cannot now, but a missing string would still throw
        // here rather than in front of somebody who reads that language.
        Sta.Run(() =>
        {
            foreach (var language in SwitchPlease.Core.Localization.Translations.All)
            {
                Localizer.Use(language.Tag);

                using var form = new SettingsForm(new AppSettings());
                using var hotkeys = new HotkeySettingsForm(
                    Hotkey.ConvertWord, Hotkey.ConvertSelection, Hotkey.None);
                using var about = new AboutForm();
                using var status = new StatusForm("status", "line one\nline two");
                using var welcome = new WelcomeForm(new AppSettings());

                Assert.NotEqual(Size.Empty, form.PreferredSize);
                Assert.NotEqual(Size.Empty, hotkeys.PreferredSize);
                Assert.NotEqual(Size.Empty, welcome.PreferredSize);
            }

            Localizer.Use("en");
        });
    }

    [Fact]
    public void TheHotkeyWindowGivesBackWhatItWasHanded()
    {
        Sta.Run(() =>
        {
            Localizer.Use("en");

            var word = new Hotkey(VirtualKeys.Pause);
            var selection = new Hotkey((ushort)'L', ModifierKeys.Control | ModifierKeys.Shift);

            using var form = new HotkeySettingsForm(word, selection, Hotkey.None);

            Assert.Equal(word, form.WordHotkey);
            Assert.Equal(selection, form.SelectionHotkey);
            Assert.Equal(Hotkey.None, form.UndoHotkey);
            Assert.False(form.UndoHotkey.IsSet);
        });
    }
}
