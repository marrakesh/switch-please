using System.Text.Json;
using SwitchPlease.Core.Config;
using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The settings file survives a round trip.
///
/// Worth its own tests because the failure is silent and delayed. <see cref="Hotkey"/> is a
/// record with a parameterised constructor, and the day it stops deserialising, the
/// application does not crash -- it starts with default hotkeys, which the user experiences
/// as their configuration having been thrown away for no reason.
/// </summary>
public class SettingsSerializationTests
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [Fact]
    public void EverySettingSurvivesBeingWrittenAndReadBack()
    {
        var original = new AppSettings
        {
            Enabled = false,
            AutoDetectEnabled = true,
            AutoDetectSensitivity = 0.42,
            CorrectionDelayMilliseconds = 33,
            PlaySoundOnConvert = false,
            RunAtStartup = true,
            Language = "uk",
            DoubleTapWindowMilliseconds = 321,
            DoubleTapHoldMilliseconds = 222,
            RespectPasswordFields = false,
            MinimumAutoWordLength = 5,
            LogTextContent = true,
            LogMaximumBytes = 4096,
            CheckForUpdates = true,
            ExcludedProcesses = ["one.exe", "two.exe"],
            ConvertWordHotkey = new Hotkey(VirtualKeys.Pause),
            ConvertSelectionHotkey = new Hotkey((ushort)'L', ModifierKeys.Control | ModifierKeys.Shift),
            UndoHotkey = new Hotkey(VirtualKeys.Menu, Kind: HotkeyKind.DoubleTap),
        };

        var restored = RoundTrip(original);

        Assert.Equal(original.Enabled, restored.Enabled);
        Assert.Equal(original.AutoDetectEnabled, restored.AutoDetectEnabled);
        Assert.Equal(original.AutoDetectSensitivity, restored.AutoDetectSensitivity);
        Assert.Equal(original.CorrectionDelayMilliseconds, restored.CorrectionDelayMilliseconds);
        Assert.Equal(original.PlaySoundOnConvert, restored.PlaySoundOnConvert);
        Assert.Equal(original.RunAtStartup, restored.RunAtStartup);
        Assert.Equal(original.Language, restored.Language);
        Assert.Equal(original.DoubleTapWindowMilliseconds, restored.DoubleTapWindowMilliseconds);
        Assert.Equal(original.DoubleTapHoldMilliseconds, restored.DoubleTapHoldMilliseconds);
        Assert.Equal(original.RespectPasswordFields, restored.RespectPasswordFields);
        Assert.Equal(original.MinimumAutoWordLength, restored.MinimumAutoWordLength);
        Assert.Equal(original.LogTextContent, restored.LogTextContent);
        Assert.Equal(original.LogMaximumBytes, restored.LogMaximumBytes);
        Assert.Equal(original.CheckForUpdates, restored.CheckForUpdates);
        Assert.Equal(original.ExcludedProcesses, restored.ExcludedProcesses);
        Assert.Equal(original.ConvertWordHotkey, restored.ConvertWordHotkey);
        Assert.Equal(original.ConvertSelectionHotkey, restored.ConvertSelectionHotkey);
        Assert.Equal(original.UndoHotkey, restored.UndoHotkey);
    }

    [Fact]
    public void ASettingsFileFromAnEarlierVersionKeepsItsValuesAndGetsDefaultsForTheRest()
    {
        // Exactly what an upgrade reads: the settings that existed before, and nothing else.
        const string Old = """
            {
              "Enabled": true,
              "ConvertWordHotkey": { "VirtualKey": 16, "Modifiers": 0, "Kind": 1 },
              "AutoDetectSensitivity": 0.15,
              "ExcludedProcesses": [ "keepass.exe" ]
            }
            """;

        var restored = JsonSerializer.Deserialize<AppSettings>(Old, Options);

        Assert.NotNull(restored);
        Assert.Equal(0.15, restored.AutoDetectSensitivity);
        Assert.Equal(["keepass.exe"], restored.ExcludedProcesses);
        Assert.Equal(Hotkey.ConvertWord, restored.ConvertWordHotkey);

        // Settings the old file never heard of come back at their defaults, not at zero.
        Assert.True(restored.RespectPasswordFields);
        Assert.Equal(500, restored.DoubleTapWindowMilliseconds);
        Assert.Equal(400, restored.DoubleTapHoldMilliseconds);
        Assert.False(restored.LogTextContent);
        Assert.False(restored.CheckForUpdates);
        Assert.Equal(Hotkey.None, restored.UndoHotkey);
    }

    [Fact]
    public void AnUnboundHotkeySurvivesAsUnbound()
    {
        var restored = RoundTrip(new AppSettings { UndoHotkey = Hotkey.None });

        Assert.False(restored.UndoHotkey.IsSet);
        Assert.Equal("-", restored.UndoHotkey.ToString());
    }

    [Theory]
    [InlineData(VirtualKeys.Shift, HotkeyKind.DoubleTap, "Shift ×2")]
    [InlineData(VirtualKeys.Control, HotkeyKind.DoubleTap, "Ctrl ×2")]
    [InlineData(VirtualKeys.Pause, HotkeyKind.Chord, "Pause/Break")]
    public void AHotkeyDescribesItselfTheWayTheMenuShowsIt(ushort key, HotkeyKind kind, string expected) =>
        Assert.Equal(expected, new Hotkey(key, Kind: kind).ToString());

    [Fact]
    public void AChordListsItsModifiersInAFixedOrder()
    {
        var hotkey = new Hotkey((ushort)'L', ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt);

        Assert.Equal("Ctrl+Alt+Shift+L", hotkey.ToString());
    }

    private static AppSettings RoundTrip(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, Options), Options)!;
}
