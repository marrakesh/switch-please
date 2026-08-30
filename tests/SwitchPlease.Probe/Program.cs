using System.Diagnostics;

namespace SwitchPlease.Probe;

/// <summary>
/// Drives a running Switch Please from outside and reports whether it actually works.
///
/// The unit tests cover every decision the application makes. They cannot cover the part
/// that matters most: that a real keystroke reaches the real hook, that the correction comes
/// back out through SendInput, that the layout switches, and that the clipboard is put back
/// the way it was found. All of that is Windows, and none of it can be faked convincingly
/// enough to be worth faking.
///
/// So this types into a text box of its own. Its own, rather than Notepad's, for two
/// reasons: nothing it types can land in anything of the user's, and the result can be read
/// straight back instead of through whatever control the current Notepad is built from.
///
/// Every burst of synthesised input is preceded by a check that this window really is in the
/// foreground. If focus has moved, the run stops rather than typing into whatever took it.
///
/// Run it with Switch Please already running:
///
///   dotnet run --project tests/SwitchPlease.Probe
///
/// It exits 0 when everything passed and 1 otherwise, so it can be scripted.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (Process.GetProcessesByName("SwitchPlease").Length == 0)
        {
            Console.Error.WriteLine("Switch Please is not running. Start it first; this drives it, it does not launch it.");
            return 1;
        }

        using var session = new ProbeSession(args.Length > 0 ? args[0] : null);

        session.Run();

        Console.WriteLine(session.Report);

        return session.Passed ? 0 : 1;
    }
}
