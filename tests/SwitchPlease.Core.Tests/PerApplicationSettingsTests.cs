using System.Text.Json;
using SwitchPlease.Core.Config;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Which applications automatic correction applies in.
///
/// The rule has to get one thing right above all: an application on the exclusion list is
/// one the switcher keeps out of, and no per-application setting may talk it back in. The
/// list is where password managers and terminals live, so a precedence bug here is the
/// difference between "does nothing" and "reads a password field".
/// </summary>
public class PerApplicationSettingsTests
{
    [Fact]
    public void WithoutAnEntryAnApplicationFollowsTheGlobalSetting()
    {
        var on = new AppSettings { AutoDetectEnabled = true };
        var off = new AppSettings { AutoDetectEnabled = false };

        Assert.True(on.AutoDetectIn("chrome.exe"));
        Assert.False(off.AutoDetectIn("chrome.exe"));
    }

    [Fact]
    public void AnEntryOverridesTheGlobalSettingInBothDirections()
    {
        var settings = new AppSettings
        {
            AutoDetectEnabled = false,
            AutoDetectPerApplication = { ["chrome.exe"] = true },
        };

        Assert.True(settings.AutoDetectIn("chrome.exe"));
        Assert.False(settings.AutoDetectIn("notepad.exe"));

        settings.AutoDetectEnabled = true;
        settings.AutoDetectPerApplication["rider64.exe"] = false;

        Assert.False(settings.AutoDetectIn("rider64.exe"));
        Assert.True(settings.AutoDetectIn("notepad.exe"));
    }

    [Fact]
    public void AnExcludedApplicationStaysExcludedWhateverElseItSays()
    {
        // The important one. Turning automatic correction on for something on the exclusion
        // list must not let the switcher back into it.
        var settings = new AppSettings
        {
            AutoDetectEnabled = true,
            AutoDetectPerApplication = { ["keepassxc.exe"] = true },
        };

        Assert.True(settings.IsExcluded("keepassxc.exe"));
        Assert.False(settings.AutoDetectIn("keepassxc.exe"));
    }

    [Theory]
    [InlineData("KeePassXC.exe")]
    [InlineData("KEEPASSXC.EXE")]
    [InlineData("keepassxc.exe")]
    public void ExecutableNamesAreComparedWithoutCase(string process)
    {
        // Windows filenames are case insensitive, and the name comes from whatever the user
        // typed into the settings file.
        Assert.True(new AppSettings().IsExcluded(process));
    }

    [Theory]
    [InlineData("Chrome.exe")]
    [InlineData("CHROME.EXE")]
    public void PerApplicationEntriesAreMatchedWithoutCaseToo(string process)
    {
        var settings = new AppSettings
        {
            AutoDetectEnabled = false,
            AutoDetectPerApplication = { ["chrome.exe"] = true },
        };

        Assert.True(settings.AutoDetectIn(process));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnUnknownApplicationFollowsTheGlobalSetting(string? process)
    {
        // The process name comes back empty for a window whose process cannot be opened,
        // which is normal for anything elevated.
        Assert.True(new AppSettings { AutoDetectEnabled = true }.AutoDetectIn(process));
        Assert.False(new AppSettings { AutoDetectEnabled = false }.AutoDetectIn(process));
        Assert.False(new AppSettings().IsExcluded(process));
    }

    [Fact]
    public void TheDefaultExclusionListCoversPasswordManagersTerminalsAndEditors()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsExcluded("keepassxc.exe"));
        Assert.True(settings.IsExcluded("powershell.exe"));
        Assert.True(settings.IsExcluded("rider64.exe"));
        Assert.False(settings.IsExcluded("chrome.exe"));
    }

    [Fact]
    public void PerApplicationEntriesStayCaseInsensitiveAfterASaveAndLoad()
    {
        // The settings file is the only way these ever come back, so matching that works in
        // memory and not after a restart is the same as not working at all. It did exactly
        // that: given a setter, System.Text.Json threw the dictionary away and built its own
        // with the default comparer.
        var original = new AppSettings
        {
            AutoDetectEnabled = false,
            AutoDetectPerApplication = { ["chrome.exe"] = true },
        };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;

        Assert.True(restored.AutoDetectIn("chrome.exe"));
        Assert.True(restored.AutoDetectIn("Chrome.exe"));
        Assert.True(restored.AutoDetectIn("CHROME.EXE"));
    }

    [Fact]
    public void DeletingAnExcludedApplicationKeepsItDeletedAcrossASaveAndLoad()
    {
        // The mirror image, and the reason the exclusion list must keep its setter while the
        // dictionary above must not: filling a list that already holds the defaults would
        // bring back every entry the user removed.
        var original = new AppSettings { ExcludedProcesses = ["only.exe"] };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;

        Assert.Equal(["only.exe"], restored.ExcludedProcesses);
        Assert.False(restored.IsExcluded("keepassxc.exe"));
    }
}
