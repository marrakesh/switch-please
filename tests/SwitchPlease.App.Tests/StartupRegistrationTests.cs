using Microsoft.Win32;
using SwitchPlease.App;
using Xunit;

namespace SwitchPlease.App.Tests;

/// <summary>
/// Starting with Windows, which two registry keys decide between them.
///
/// The trap these cover is that Task Manager's "Startup apps" tab does not delete a Run
/// entry when you switch it off -- it leaves the command in place and records the refusal
/// separately, under Explorer's StartupApproved. Read only the first key and the answer is
/// "yes, it starts" for an application Windows has been told not to start, which is the same
/// class of disagreement that having a copy of this in settings.json used to cause.
///
/// Everything here runs against a throwaway key of its own. Nothing in this file goes near
/// the real Run key: a test run must not switch the tester's own copy of Switch Please on or
/// off, and one that did would be discovered at the next sign-in rather than in the output.
/// </summary>
public sealed class StartupRegistrationTests : IDisposable
{
    private readonly string _root =
        @"Software\SwitchPlease.Tests\" + Guid.NewGuid().ToString("N");

    private string RunKey => _root + @"\Run";

    private string ApprovedKey => _root + @"\StartupApproved";

    private const string ValueName = "SwitchPlease";

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
        }
        catch (Exception)
        {
            // A leftover test key is not worth failing a run over.
        }
    }

    private void WriteApproval(params byte[] approval)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedKey);
        key.SetValue(ValueName, approval, RegistryValueKind.Binary);
    }

    private byte[]? ReadApproval()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return key?.GetValue(ValueName) as byte[];
    }

    private string? ReadCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    [Fact]
    public void NothingRegisteredMeansItDoesNotStart() =>
        Assert.False(StartupRegistration.IsEnabled(RunKey, ApprovedKey));

    [Fact]
    public void TurningItOnRegistersTheExecutableInQuotes()
    {
        StartupRegistration.SetEnabled(true, @"C:\Program Files\Switch Please\SwitchPlease.exe", RunKey, ApprovedKey);

        // Quoted because the path has a space in it, and an unquoted one is read as a
        // command plus arguments -- the "C:\Program" that never launches anything.
        Assert.Equal(@"""C:\Program Files\Switch Please\SwitchPlease.exe""", ReadCommand());
        Assert.True(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    [Fact]
    public void TurningItOffTakesTheEntryAway()
    {
        StartupRegistration.SetEnabled(true, @"C:\app.exe", RunKey, ApprovedKey);
        StartupRegistration.SetEnabled(false, @"C:\app.exe", RunKey, ApprovedKey);

        Assert.Null(ReadCommand());
        Assert.False(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    [Fact]
    public void TurningItOffTwiceIsNotAnError()
    {
        StartupRegistration.SetEnabled(false, @"C:\app.exe", RunKey, ApprovedKey);
        StartupRegistration.SetEnabled(false, @"C:\app.exe", RunKey, ApprovedKey);

        Assert.False(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    /// <summary>
    /// The whole point of the class: the command is still there, and the answer is still no.
    /// </summary>
    [Fact]
    public void AnEntryTaskManagerHasSwitchedOffDoesNotCountAsStarting()
    {
        StartupRegistration.SetEnabled(true, @"C:\app.exe", RunKey, ApprovedKey);
        WriteApproval(0x03, 0, 0, 0, 60, 101, 246, 91, 144, 207, 220, 8);

        Assert.NotNull(ReadCommand());
        Assert.False(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    [Fact]
    public void TurningItBackOnClearsWhatTaskManagerWrote()
    {
        StartupRegistration.SetEnabled(true, @"C:\app.exe", RunKey, ApprovedKey);
        WriteApproval(0x03, 0, 0, 0, 60, 101, 246, 91, 144, 207, 220, 8);

        StartupRegistration.SetEnabled(true, @"C:\app.exe", RunKey, ApprovedKey);

        // Without this the menu ticks and the application still does not start, which is
        // precisely the disagreement the tick is supposed to be reporting on.
        Assert.Null(ReadApproval());
        Assert.True(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    [Fact]
    public void TurningItOffLeavesNoApprovalBehindEither()
    {
        StartupRegistration.SetEnabled(true, @"C:\app.exe", RunKey, ApprovedKey);
        WriteApproval(0x03, 0, 0, 0, 60, 101, 246, 91, 144, 207, 220, 8);

        StartupRegistration.SetEnabled(false, @"C:\app.exe", RunKey, ApprovedKey);

        // An approval for an entry that no longer exists would be read back the next time
        // the entry is recreated, by the installer if not by us.
        Assert.Null(ReadApproval());
    }

    [Fact]
    public void AnApprovalSayingYesLeavesItStarting()
    {
        StartupRegistration.SetEnabled(true, @"C:\app.exe", RunKey, ApprovedKey);
        WriteApproval(0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.True(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    /// <summary>
    /// An approval on its own starts nothing: it is a verdict on a Run entry, not a
    /// substitute for one.
    /// </summary>
    [Fact]
    public void AnApprovalWithoutACommandStartsNothing()
    {
        WriteApproval(0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.False(StartupRegistration.IsEnabled(RunKey, ApprovedKey));
    }

    [Theory]
    // What Task Manager writes, in both of the pairs that turn up.
    [InlineData(false, new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(true, new byte[] { 0x03, 0, 0, 0, 60, 101, 246, 91, 144, 207, 220, 8 })]
    [InlineData(false, new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(true, new byte[] { 0x07, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    // Nothing recorded is the state nearly every Run entry is in, and it starts.
    [InlineData(false, null)]
    // A real value found in this key on a real machine, belonging to GalaxyClient.
    [InlineData(false, new byte[0])]
    // Only the first byte is read, so a truncated value is still answerable.
    [InlineData(true, new byte[] { 0x03 })]
    public void TheVerdictIsTheLowBitOfTheFirstByte(bool disapproved, byte[]? approval) =>
        Assert.Equal(disapproved, StartupRegistration.IsDisapproved(approval));
}
