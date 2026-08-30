namespace SwitchPlease.Core.Localization;

/// <summary>
/// Every piece of text the interface shows.
///
/// Each property is <c>required</c> on purpose: adding one here fails the build until every
/// translation supplies it. A resource file would let a missing translation slip through
/// silently and surface as a blank menu item at runtime.
///
/// Placeholders are documented on each string, because a translator cannot guess what
/// <c>{0}</c> stands for.
/// </summary>
public sealed class UiStrings
{
    /// <summary>Name of the language in its own language, shown in the language menu.</summary>
    public required string LanguageName { get; init; }

    // Tray menu.
    public required string MenuEnabled { get; init; }

    public required string MenuAutoDetect { get; init; }

    /// <summary>{0} = hotkey, e.g. "Shift x2".</summary>
    public required string MenuConvertWord { get; init; }

    /// <summary>{0} = hotkey.</summary>
    public required string MenuConvertLine { get; init; }

    public required string MenuHotkeys { get; init; }

    public required string MenuSound { get; init; }

    public required string MenuStartup { get; init; }

    public required string MenuDiagnostics { get; init; }

    public required string MenuStatus { get; init; }

    public required string MenuRefreshLayouts { get; init; }

    public required string MenuOpenSettings { get; init; }

    public required string MenuLanguage { get; init; }

    /// <summary>Menu entry meaning "follow the Windows display language".</summary>
    public required string MenuLanguageAuto { get; init; }

    public required string MenuAbout { get; init; }

    public required string MenuExit { get; init; }

    /// <summary>{0} = active layout tag, {1} = word hotkey, {2} = line hotkey.</summary>
    public required string TooltipActive { get; init; }

    public required string TooltipDisabled { get; init; }

    // Status window.
    public required string StatusTitle { get; init; }

    /// <summary>{0} = StatusInstalled or StatusNotInstalled.</summary>
    public required string StatusHook { get; init; }

    public required string StatusInstalled { get; init; }

    /// <summary>{0} = how many times the hook had to be put back. Only shown when above zero.</summary>
    public required string StatusHookRecovered { get; init; }

    public required string StatusNotInstalled { get; init; }

    /// <summary>{0} = StatusOn or StatusOff.</summary>
    public required string StatusProcessing { get; init; }

    /// <summary>{0} = StatusOn or StatusOff.</summary>
    public required string StatusAutoDetect { get; init; }

    public required string StatusOn { get; init; }

    public required string StatusOff { get; init; }

    public required string StatusLayouts { get; init; }

    /// <summary>Appended to the layout that is currently active.</summary>
    public required string StatusActiveMarker { get; init; }

    public required string StatusMaps { get; init; }

    /// <summary>{0} = source tag, {1} = target tag, {2} = number of characters.</summary>
    public required string StatusMapLine { get; init; }

    /// <summary>{0} = comma-separated language tags.</summary>
    public required string StatusDictionaries { get; init; }

    public required string StatusDictionariesNone { get; init; }

    /// <summary>{0} = comma-separated languages with the evidence behind each.</summary>
    public required string StatusLanguages { get; init; }

    public required string StatusLatencyTitle { get; init; }

    /// <summary>{0} = number of samples.</summary>
    public required string StatusLatencySamples { get; init; }

    /// <summary>{0} = microseconds.</summary>
    public required string StatusLatencyAverage { get; init; }

    /// <summary>{0} = microseconds.</summary>
    public required string StatusLatencyMaximum { get; init; }

    /// <summary>{0} = number of dropped events.</summary>
    public required string StatusLatencyDropped { get; init; }

    public required string StatusLatencyLimit { get; init; }

    public required string StatusLatencyDisabled { get; init; }

    public required string StatusRecentEvents { get; init; }

    // Worker health.
    /// <summary>{0} = StatusWorkerRunning or StatusWorkerStopped.</summary>
    public required string StatusWorker { get; init; }

    public required string StatusWorkerRunning { get; init; }

    public required string StatusWorkerStopped { get; init; }

    /// <summary>{0} = keystrokes waiting, {1} = seconds since the last one was handled.</summary>
    public required string StatusQueue { get; init; }

    /// <summary>{0} = corrections made this session.</summary>
    public required string StatusCorrections { get; init; }

    /// <summary>{0} = milliseconds taken by the slowest keystroke.</summary>
    public required string StatusSlowest { get; init; }

    // Hotkey window.
    public required string HotkeysTitle { get; init; }

    public required string HotkeysWord { get; init; }

    public required string HotkeysSelection { get; init; }

    public required string HotkeysPress { get; init; }

    public required string HotkeysHint { get; init; }

    public required string HotkeysDefaults { get; init; }

    public required string HotkeysDuplicate { get; init; }

    // About window.
    public required string AboutTitle { get; init; }

    public required string AboutTagline { get; init; }

    /// <summary>{0} = version number.</summary>
    public required string AboutVersion { get; init; }

    public required string AboutLicense { get; init; }

    public required string AboutSource { get; init; }

    /// <summary>Label for the funding link, whichever platform it points at.</summary>
    public required string AboutDonate { get; init; }

    // Shared buttons.
    public required string ButtonOk { get; init; }

    public required string ButtonCancel { get; init; }

    public required string ButtonClose { get; init; }

    // Errors.
    /// <summary>{0} = error message.</summary>
    public required string ErrorHookFailed { get; init; }

    /// <summary>{0} = error message.</summary>
    public required string ErrorStartup { get; init; }

    /// <summary>{0} = error message.</summary>
    public required string ErrorOpenFolder { get; init; }

    public required string ErrorAlreadyRunning { get; init; }

    /// <summary>{0} = error message, {1} = log file path.</summary>
    public required string ErrorCrash { get; init; }

    // Added commands.
    /// <summary>{0} = hotkey, or "-" when the command has none bound.</summary>
    public required string MenuUndo { get; init; }

    /// <summary>{0} = executable name, e.g. "chrome.exe".</summary>
    public required string MenuExclude { get; init; }

    /// <summary>{0} = executable name. Shown when it is already excluded.</summary>
    public required string MenuInclude { get; init; }

    /// <summary>Shown in place of the exclusion entry before anything has been typed.</summary>
    public required string MenuExcludeUnknown { get; init; }

    /// <summary>{0} = executable name. Ticked when automatic correction applies there.</summary>
    public required string MenuAutoDetectHere { get; init; }

    public required string MenuSettings { get; init; }

    public required string MenuCheckUpdates { get; init; }

    // Settings window.
    public required string SettingsTitle { get; init; }

    public required string SettingsSensitivity { get; init; }

    public required string SettingsSensitivityHint { get; init; }

    public required string SettingsDelay { get; init; }

    public required string SettingsDelayHint { get; init; }

    public required string SettingsTapWindow { get; init; }

    public required string SettingsTapHold { get; init; }

    public required string SettingsMinimumWord { get; init; }

    public required string SettingsPasswordFields { get; init; }

    public required string SettingsLogText { get; init; }

    public required string SettingsLogTextHint { get; init; }

    public required string SettingsUpdates { get; init; }

    public required string SettingsExcluded { get; init; }

    public required string SettingsExcludedHint { get; init; }

    // Update check.
    public required string UpdateTitle { get; init; }

    /// <summary>{0} = the newer version, {1} = the version running.</summary>
    public required string UpdateAvailable { get; init; }

    /// <summary>{0} = the version running.</summary>
    public required string UpdateCurrent { get; init; }

    /// <summary>{0} = error message.</summary>
    public required string UpdateFailed { get; init; }

    // Blocked input notification.
    public required string BlockedTitle { get; init; }

    public required string BlockedBody { get; init; }

    /// <summary>Button that puts the status report on the clipboard.</summary>
    public required string StatusCopy { get; init; }

    /// <summary>Label for the undo binding in the hotkey window.</summary>
    public required string HotkeysUndo { get; init; }

    public required string SettingsFullscreen { get; init; }

    public required string SettingsFullscreenHint { get; init; }

    // Typewriter effect.
    public required string SettingsTypewriter { get; init; }

    public required string SettingsTypewriterHint { get; init; }

    // First run.
    public required string WelcomeTitle { get; init; }

    public required string WelcomeHeading { get; init; }

    /// <summary>Where the application lives once the window is closed. No placeholders.</summary>
    public required string WelcomeBody { get; init; }

    /// <summary>What the word hotkey does, shown beside the key it is bound to.</summary>
    public required string WelcomeActionWord { get; init; }

    /// <summary>What the selection hotkey does.</summary>
    public required string WelcomeActionSelection { get; init; }

    // Settings window sections.
    public required string SettingsGroupCorrection { get; init; }

    public required string SettingsGroupHotkeys { get; init; }

    public required string SettingsGroupPrivacy { get; init; }

    public required string SettingsGroupApplications { get; init; }

    /// <summary>Demonstration: a phrase as it lands when the layout was wrong.</summary>
    public required string WelcomeDemoTyped { get; init; }

    /// <summary>The same phrase put right. Must be the same language as the interface.</summary>
    public required string WelcomeDemoFixed { get; init; }
}
