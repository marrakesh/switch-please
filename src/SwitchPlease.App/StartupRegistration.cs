using Microsoft.Win32;

namespace SwitchPlease.App;

/// <summary>
/// Whether Windows starts the application at sign-in, and the switch for changing it.
///
/// There is no setting for this in settings.json, deliberately. The registry is where the
/// answer lives, the installer writes it there, and a copy of our own could only drift.
///
/// Two keys decide it between them, which is the whole reason this is not a one-liner. The
/// Run key holds the command to start; Explorer's StartupApproved key holds, separately,
/// whether the user has since switched that entry off. Task Manager's "Startup apps" tab
/// disables an entry by writing the second and <em>leaving the first alone</em>, so reading
/// only the Run key reports an entry as enabled that Windows will not actually start.
/// </summary>
public static class StartupRegistration
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ApprovedSubKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private const string ValueName = "SwitchPlease";

    /// <summary>Whether Windows will start the application at sign-in.</summary>
    /// <remarks>
    /// A registry that cannot be read is reported as "not starting" rather than thrown
    /// from: this is what ticks a menu item, and a tray menu that fails to open is worse
    /// than one showing a stale tick.
    /// </remarks>
    public static bool IsEnabled() => IsEnabled(RunSubKey, ApprovedSubKey);

    /// <summary>Turns starting at sign-in on or off.</summary>
    /// <remarks>
    /// Unlike <see cref="IsEnabled()"/> this throws, because the caller asked for a change
    /// and silently not making it is the one outcome that must not happen quietly.
    /// </remarks>
    public static void SetEnabled(bool enabled, string executablePath) =>
        SetEnabled(enabled, executablePath, RunSubKey, ApprovedSubKey);

    /// <summary>
    /// The form that takes the two keys to work on, so the tests can exercise all of this
    /// somewhere harmless instead of switching the real Switch Please on and off on the
    /// machine running them.
    /// </summary>
    public static bool IsEnabled(string runSubKey, string approvedSubKey)
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(runSubKey);

            if (run?.GetValue(ValueName) is null)
            {
                return false;
            }

            using var approved = Registry.CurrentUser.OpenSubKey(approvedSubKey);

            return !IsDisapproved(approved?.GetValue(ValueName) as byte[]);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc cref="IsEnabled(string, string)"/>
    public static void SetEnabled(
        bool enabled, string executablePath, string runSubKey, string approvedSubKey)
    {
        using (var run = Registry.CurrentUser.OpenSubKey(runSubKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(runSubKey))
        {
            if (enabled)
            {
                run.SetValue(ValueName, $"\"{executablePath}\"");
            }
            else
            {
                run.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }

        // Both ways round, the approval is removed rather than written.
        //
        // Turning on: an entry Task Manager had switched off keeps its approval saying so,
        // and writing the Run value without clearing that leaves the menu ticked and the
        // application still not starting -- exactly the disagreement this class exists to
        // avoid. An absent approval means enabled, which is the state the great majority of
        // Run entries are in, so removing it is both correct and less presumptuous than
        // writing a structure that belongs to Explorer.
        //
        // Turning off: the Run value is gone, so an approval for it is stale by definition.
        using var approved = Registry.CurrentUser.OpenSubKey(approvedSubKey, writable: true);

        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Reads Explorer's verdict out of the twelve bytes it stores against a startup entry.
    /// </summary>
    /// <remarks>
    /// Only the low bit of the first byte is consulted: 0x02 for approved and 0x03 for
    /// disapproved are what Task Manager writes, and the same bit carries the answer in the
    /// 0x06/0x07 pair that also turns up. The eight bytes after it are meant to be the time
    /// the user switched the entry off, and are ignored here because they cannot be relied
    /// on -- among the entries on the machine this was written against, one decodes to the
    /// year 3624. Nothing needs the time, so nothing reads it.
    ///
    /// Absent, empty and unreadable all mean approved. Windows starts the great majority of
    /// Run entries without any approval recorded at all, and a zero-length value is a real
    /// thing to find in this key rather than a hypothetical.
    /// </remarks>
    public static bool IsDisapproved(byte[]? approval) =>
        approval is { Length: > 0 } && (approval[0] & 1) != 0;
}
