using SwitchPlease.App.Localization;

namespace SwitchPlease.App;

internal static class Program
{
    /// <summary>
    /// Two copies of a keyboard hook fighting over the same keystrokes would double every
    /// correction, so a second instance refuses to start.
    /// </summary>
    private const string InstanceMutexName = @"Local\SwitchPlease.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isFirst);

        if (!isFirst)
        {
            Localizer.Use(SettingsStore.Load().Language);

            MessageBox.Show(
                Localizer.Text.ErrorAlreadyRunning,
                "Switch Please",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        ApplicationConfiguration.Initialize();

        // Follow the Windows light or dark setting. Without this every window is painted
        // light on a machine set to dark, which on a utility that only ever shows small
        // dialogs is exactly when it is most jarring.
        Application.SetColorMode(SystemColorMode.System);

        var settings = SettingsStore.Load();

        // Asked before anything can write the file, and answered by the tray once its icon
        // exists: the welcome panel points at that icon, so it cannot come first.
        bool firstRun = WelcomeForm.IsFirstRun();

        // Stated rather than assumed: without this, whether an exception on the interface
        // thread reaches ThreadException depends on configuration, and the one time this
        // application did die the log it was supposed to leave behind was not there.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) => ReportCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);

        // The update check is the one thing here that runs as a task, and a task's exception
        // is otherwise swallowed and never seen by anyone.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportCrash(e.Exception);
            e.SetObserved();
        };

        using var context = new TrayContext(settings, firstRun);
        Application.Run(context);
    }

    private static void ReportCrash(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            File.AppendAllText(
                SettingsStore.LogPath,
                $"{DateTime.Now:u} {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Nothing useful left to do if even logging fails.
        }

        MessageBox.Show(
            string.Format(Localizer.Text.ErrorCrash, exception.Message, SettingsStore.LogPath),
            "Switch Please",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
