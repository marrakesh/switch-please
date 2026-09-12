using System.Text.Json;
using SwitchPlease.Core.Config;
using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.App.Tests;

/// <summary>
/// Reading and writing the settings file.
///
/// The failures here are all silent. A file that fails to parse comes back as defaults,
/// which the user experiences as their configuration having been thrown away for no reason;
/// a write that is not atomic loses everything if the machine goes down inside it. Neither
/// throws, neither logs, and neither is noticeable until it has already happened.
/// </summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "switch-please-tests", Guid.NewGuid().ToString("N"));

    private string Path0 => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temporary directory is not worth failing a test run over.
        }
    }

    [Fact]
    public void SettingsSurviveBeingSavedAndLoaded()
    {
        var original = new AppSettings
        {
            Enabled = false,
            AutoDetectEnabled = true,
            AutoDetectSensitivity = 0.42,
            Language = "uk",
            PauseInFullscreenApps = false,
            TypewriterMillisecondsPerCharacter = 12,
            ExcludedProcesses = ["one.exe", "two.exe"],
            UndoHotkey = new Hotkey(VirtualKeys.Menu, Kind: HotkeyKind.DoubleTap),
        };

        SettingsStore.Save(original, Path0);
        var restored = SettingsStore.Load(Path0);

        Assert.False(restored.Enabled);
        Assert.True(restored.AutoDetectEnabled);
        Assert.Equal(0.42, restored.AutoDetectSensitivity);
        Assert.Equal("uk", restored.Language);
        Assert.False(restored.PauseInFullscreenApps);
        Assert.Equal(12, restored.TypewriterMillisecondsPerCharacter);
        Assert.Equal(["one.exe", "two.exe"], restored.ExcludedProcesses);
        Assert.Equal(original.UndoHotkey, restored.UndoHotkey);
    }

    [Fact]
    public void SavingCreatesTheFolderItWasGivenAPathIn()
    {
        // First run: nothing under %APPDATA% exists yet.
        Assert.False(Directory.Exists(_directory));

        SettingsStore.Save(new AppSettings(), Path0);

        Assert.True(File.Exists(Path0));
    }

    [Fact]
    public void SavingLeavesNoTemporaryFileBehind()
    {
        // The write goes through a temporary file and a rename. If the rename ever stopped
        // happening, the settings would silently never change and a .tmp would pile up.
        SettingsStore.Save(new AppSettings(), Path0);

        Assert.False(File.Exists(Path0 + ".tmp"));
        Assert.Equal(["settings.json"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public void SavingOverAnExistingFileReplacesItRatherThanFailing()
    {
        SettingsStore.Save(new AppSettings { Language = "ru" }, Path0);
        SettingsStore.Save(new AppSettings { Language = "en" }, Path0);

        Assert.Equal("en", SettingsStore.Load(Path0).Language);
    }

    [Fact]
    public void AMissingFileGivesDefaultsRatherThanThrowing()
    {
        var settings = SettingsStore.Load(Path0);

        Assert.True(settings.Enabled);
        Assert.Equal(Hotkey.ConvertWord, settings.ConvertWordHotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"Enabled\": ")]
    [InlineData("{ \"AutoDetectSensitivity\": \"not a number\" }")]
    [InlineData("null")]
    public void ACorruptFileGivesDefaultsRatherThanStoppingTheApplication(string content)
    {
        // Whatever is in there, the application has to start. Refusing to would leave the
        // user with a program that cannot run and no way to find out why.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path0, content);

        var settings = SettingsStore.Load(Path0);

        Assert.True(settings.Enabled);
        Assert.Equal(0.25, settings.AutoDetectSensitivity);
    }

    [Fact]
    public void AFileFromAnEarlierVersionKeepsWhatItHasAndGetsDefaultsForTheRest()
    {
        // Exactly what an upgrade reads, and what was actually found in the wild: a file
        // written before half of these settings existed.
        const string Old = """
            {
              "Enabled": true,
              "ConvertWordHotkey": { "VirtualKey": 16, "Modifiers": 0, "Kind": 1 },
              "AutoDetectSensitivity": 0.15,
              "ExcludedProcesses": [ "keepass.exe" ]
            }
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path0, Old);

        var settings = SettingsStore.Load(Path0);

        Assert.Equal(0.15, settings.AutoDetectSensitivity);
        Assert.Equal(["keepass.exe"], settings.ExcludedProcesses);
        Assert.Equal(Hotkey.ConvertWord, settings.ConvertWordHotkey);

        Assert.True(settings.RespectPasswordFields);
        Assert.True(settings.PauseInFullscreenApps);
        Assert.False(settings.LogTextContent);
        Assert.Equal(Hotkey.None, settings.UndoHotkey);
    }

    [Fact]
    public void AFileNamingASettingThatNoLongerExistsStillLoadsTheRest()
    {
        // RunAtStartup was dropped once the Run key became the only place autostart is
        // recorded, so every file written by an earlier version still names it. A leftover
        // key has to be ignored rather than treated as a parse failure: Load answers a bad
        // file with defaults, so getting this wrong would quietly discard real settings.
        const string WithRemovedSetting = """
            {
              "Enabled": false,
              "RunAtStartup": true,
              "AutoDetectSensitivity": 0.35,
              "ExcludedProcesses": [ "wt.exe" ]
            }
            """;

        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path0, WithRemovedSetting);

        var settings = SettingsStore.Load(Path0);

        // Every value here differs from the default, so a file rejected outright -- which
        // Load answers with defaults, silently -- fails rather than slipping through.
        Assert.False(settings.Enabled);
        Assert.Equal(0.35, settings.AutoDetectSensitivity);
        Assert.Equal(["wt.exe"], settings.ExcludedProcesses);
    }

    [Fact]
    public void TheFileIsIndentedJsonSomebodyCanEditByHand()
    {
        // The README tells people they can edit it, and the tray has an "open settings
        // folder" entry for exactly that.
        SettingsStore.Save(new AppSettings(), Path0);

        string json = File.ReadAllText(Path0);

        Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
        Assert.Contains("  \"Enabled\"", json, StringComparison.Ordinal);

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void TheWellKnownLocationSitsUnderTheRoamingProfile()
    {
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwitchPlease"),
            SettingsStore.DirectoryPath);

        Assert.StartsWith(SettingsStore.DirectoryPath, SettingsStore.FilePath, StringComparison.Ordinal);
        Assert.StartsWith(SettingsStore.DirectoryPath, SettingsStore.LogPath, StringComparison.Ordinal);
    }
}
