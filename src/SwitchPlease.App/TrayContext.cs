using System.Text;
using SwitchPlease.App.Localization;
using SwitchPlease.Core.Localization;
using SwitchPlease.Core.Config;

namespace SwitchPlease.App;

/// <summary>
/// The tray icon and its menu. This is the whole user interface: the application has no
/// main window, and the UI thread exists mainly to own the icon and to service the
/// clipboard, which must be touched from an STA thread.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly SwitcherService _service;
    private readonly AppSettings _settings;
    private readonly Control _uiMarshal;
    private readonly System.Windows.Forms.Timer _iconTimer;

    // Runs only while the correction animation is playing, then stops itself. A second timer
    // rather than a faster shared one: the icon is otherwise refreshed once a second, and
    // waking the UI thread thirty times a second for the other 99% of the time to catch the
    // occasional correction is a poor trade in a program that sits in the tray all day.
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly List<string> _recentLog = [];

    private int _animationFrame = -1;

    private ToolStripMenuItem _enabledItem = null!;
    private ToolStripMenuItem _autoDetectItem = null!;
    private ToolStripMenuItem _startupItem = null!;
    private ToolStripMenuItem _soundItem = null!;
    private ToolStripMenuItem _diagnosticsItem = null!;
    private ToolStripMenuItem _convertWordItem = null!;
    private ToolStripMenuItem _convertLineItem = null!;
    private ToolStripMenuItem _undoItem = null!;
    private ToolStripMenuItem _excludeItem = null!;
    private ToolStripMenuItem _autoDetectHereItem = null!;
    private string _currentTag = "??";
    private Icon? _currentIcon;
    private int _disposed;

    /// <param name="firstRun">
    /// Whether to open the welcome panel. Decided before this runs, because saving settings
    /// is what stops it being a first run and this class does that on any change.
    /// </param>
    public TrayContext(AppSettings settings, bool firstRun = false)
    {
        _settings = settings;
        Localizer.Use(settings.Language);

        // A handle-owning control gives the worker thread something to Invoke onto when it
        // needs the clipboard.
        _uiMarshal = new Control();
        _uiMarshal.CreateControl();

        _service = new SwitcherService(settings);
        _service.Logged += OnLogged;

        // Posted, never waited on. Both of these are raised by the worker while it holds the
        // buffer lock, and the tray menu's own commands take that same lock: waiting for the
        // interface thread from inside the lock, while the interface thread waits for the
        // lock, is a deadlock that ends with Windows declaring the application hung.
        _service.InputBlocked += _ => PostToUi(ShowBlockedNotice);
        _service.Corrected += () => PostToUi(PlayCorrectionAnimation);

        _icon = new NotifyIcon
        {
            Visible = true,
            Text = "Switch Please",
            ContextMenuStrip = BuildMenu(),
        };

        _icon.DoubleClick += (_, _) => ToggleEnabled();

        UpdateIcon();

        _iconTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _iconTimer.Tick += (_, _) => OnTick();
        _iconTimer.Start();

        _animationTimer = new System.Windows.Forms.Timer { Interval = 45 };
        _animationTimer.Tick += (_, _) => AdvanceAnimation();

        StartService();

        if (firstRun)
        {
            ShowWelcome();
        }

        if (settings.CheckForUpdates)
        {
            _ = CheckForUpdatesAsync(announceWhenCurrent: false);
        }
    }

    /// <summary>
    /// Opens the welcome panel over the tray, and writes the settings file when it closes.
    ///
    /// Not modal. It sits beside the icon it is describing, and the point is that the user
    /// can go and click that icon while it is still up.
    ///
    /// Writing the file is what makes this happen once: its absence is the definition of a
    /// first run, and nothing else writes it until the user changes something, which they
    /// may never do.
    /// </summary>
    private void ShowWelcome()
    {
        var welcome = new WelcomeForm(_settings);

        welcome.FormClosed += (_, _) =>
        {
            welcome.Dispose();

            try
            {
                SettingsStore.Save(_settings);
            }
            catch (Exception ex)
            {
                OnLogged($"could not save settings: {ex.Message}");
            }
        };

        welcome.Show();
    }

    private static UiStrings Text => Localizer.Text;

    private void StartService()
    {
        try
        {
            _service.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(Text.ErrorHookFailed, ex.Message),
                "Switch Please",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            ExitThread();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _enabledItem = new ToolStripMenuItem(Text.MenuEnabled, null, (_, _) => ToggleEnabled())
        {
            Checked = _settings.Enabled,
        };

        _autoDetectItem = new ToolStripMenuItem(Text.MenuAutoDetect, null, (_, _) => ToggleAutoDetect())
        {
            Checked = _settings.AutoDetectEnabled,
        };

        _soundItem = new ToolStripMenuItem(Text.MenuSound, null, (_, _) => ToggleSound())
        {
            Checked = _settings.PlaySoundOnConvert,
        };

        _startupItem = new ToolStripMenuItem(Text.MenuStartup, null, (_, _) => ToggleStartup())
        {
            Checked = StartupRegistration.IsEnabled(),
        };

        _diagnosticsItem = new ToolStripMenuItem(Text.MenuDiagnostics, null, (_, _) => ToggleDiagnostics())
        {
            Checked = _settings.DiagnosticsEnabled,
        };

        _convertWordItem = new ToolStripMenuItem(
            ConvertWordCaption(), null, (_, _) => _service.ConvertLastWordNow());

        _convertLineItem = new ToolStripMenuItem(
            ConvertLineCaption(), null, (_, _) => _service.ConvertLineNow());

        _undoItem = new ToolStripMenuItem(UndoCaption(), null, (_, _) => _service.UndoLastNow());

        _excludeItem = new ToolStripMenuItem(Text.MenuExcludeUnknown, null, (_, _) => ToggleExclusion())
        {
            Enabled = false,
        };

        _autoDetectHereItem = new ToolStripMenuItem(
            Text.MenuExcludeUnknown, null, (_, _) => ToggleAutoDetectHere())
        {
            Enabled = false,
        };

        menu.Items.Add(_enabledItem);
        menu.Items.Add(_autoDetectItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_convertWordItem);
        menu.Items.Add(_convertLineItem);
        menu.Items.Add(_undoItem);
        menu.Items.Add(new ToolStripMenuItem(Text.MenuHotkeys, null, (_, _) => EditHotkeys()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_soundItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(_diagnosticsItem);
        menu.Items.Add(_autoDetectHereItem);
        menu.Items.Add(_excludeItem);
        menu.Items.Add(BuildLanguageMenu());
        menu.Items.Add(new ToolStripMenuItem(Text.MenuSettings, null, (_, _) => EditSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Text.MenuStatus, null, (_, _) => ShowDiagnostics()));
        menu.Items.Add(new ToolStripMenuItem(Text.MenuRefreshLayouts, null, (_, _) => _service.RefreshLayouts()));
        menu.Items.Add(new ToolStripMenuItem(Text.MenuOpenSettings, null, (_, _) => OpenSettingsFolder()));
        menu.Items.Add(new ToolStripMenuItem(
            Text.MenuCheckUpdates, null, (_, _) => _ = CheckForUpdatesAsync(announceWhenCurrent: true)));
        menu.Items.Add(new ToolStripMenuItem(Text.MenuAbout, null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Text.MenuExit, null, (_, _) => ExitApplication()));

        // The application typing was last seen in is only known once some has happened, and
        // it changes as the user moves around, so the entry naming it is filled in each time
        // the menu opens rather than when it is built. The startup tick is refreshed here for
        // the same reason: the Run key is the record, and an installer, an uninstaller or
        // regedit can change it while the application is running.
        menu.Opening += (_, _) =>
        {
            RefreshExclusionItem();
            _startupItem.Checked = StartupRegistration.IsEnabled();
        };

        return menu;
    }

    private ToolStripMenuItem BuildLanguageMenu()
    {
        var languages = new ToolStripMenuItem(Text.MenuLanguage);

        var auto = new ToolStripMenuItem(Text.MenuLanguageAuto, null, (_, _) => ChangeLanguage(Localizer.Auto))
        {
            Checked = Localizer.Selected == Localizer.Auto,
        };

        languages.DropDownItems.Add(auto);
        languages.DropDownItems.Add(new ToolStripSeparator());

        foreach (var language in Localizer.Available)
        {
            string tag = language.Tag;

            languages.DropDownItems.Add(new ToolStripMenuItem(
                language.Name, null, (_, _) => ChangeLanguage(tag))
            {
                Checked = Localizer.Selected == tag,
            });
        }

        return languages;
    }

    private void ChangeLanguage(string tagOrAuto)
    {
        Localizer.Use(tagOrAuto);
        _settings.Language = Localizer.Selected;

        // Menu captions are baked in when the items are created, so the whole menu is
        // rebuilt rather than walked and patched.
        var previous = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = BuildMenu();
        previous?.Dispose();

        _currentTag = string.Empty;
        ApplyAndSave();
    }

    private string ConvertWordCaption() => string.Format(Text.MenuConvertWord, _settings.ConvertWordHotkey);

    private string ConvertLineCaption() => string.Format(Text.MenuConvertLine, _settings.ConvertSelectionHotkey);

    private string UndoCaption() => string.Format(Text.MenuUndo, _settings.UndoHotkey);

    /// <summary>
    /// Points the exclusion entry at whatever the user was last typing in, and flips it
    /// between excluding and re-including depending on where that application already is.
    /// </summary>
    private void RefreshExclusionItem()
    {
        string process = _service.LastActiveProcess;

        if (process.Length == 0)
        {
            _excludeItem.Text = Text.MenuExcludeUnknown;
            _excludeItem.Enabled = false;
            return;
        }

        bool excluded = _settings.IsExcluded(process);

        _excludeItem.Text = string.Format(excluded ? Text.MenuInclude : Text.MenuExclude, process);
        _excludeItem.Checked = excluded;
        _excludeItem.Enabled = true;

        // Automatic correction is wanted in the browser and unwanted in the editor, and the
        // moment to say so is while you are in one of them. Excluded applications get no say:
        // the switcher is not in them at all.
        _autoDetectHereItem.Text = string.Format(Text.MenuAutoDetectHere, process);
        _autoDetectHereItem.Checked = _settings.AutoDetectIn(process);
        _autoDetectHereItem.Enabled = !excluded;
    }

    /// <summary>
    /// Turns automatic correction on or off for the application the user was last in.
    ///
    /// An entry that agrees with the global setting is removed rather than stored, so the
    /// list holds only genuine exceptions and an application goes back to following the
    /// general rule when it is set back.
    /// </summary>
    private void ToggleAutoDetectHere()
    {
        string process = _service.LastActiveProcess;

        if (process.Length == 0 || _settings.IsExcluded(process))
        {
            return;
        }

        bool wanted = !_settings.AutoDetectIn(process);

        if (wanted == _settings.AutoDetectEnabled)
        {
            _settings.AutoDetectPerApplication.Remove(process);
        }
        else
        {
            _settings.AutoDetectPerApplication[process] = wanted;
        }

        ApplyAndSave();
    }

    private void ToggleExclusion()
    {
        string process = _service.LastActiveProcess;

        if (process.Length == 0)
        {
            return;
        }

        int at = _settings.ExcludedProcesses.FindIndex(
            p => string.Equals(p, process, StringComparison.OrdinalIgnoreCase));

        if (at >= 0)
        {
            _settings.ExcludedProcesses.RemoveAt(at);
        }
        else
        {
            _settings.ExcludedProcesses.Add(process);
        }

        ApplyAndSave();
    }

    private void EditSettings()
    {
        using var form = new SettingsForm(_settings);

        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        form.ApplyTo(_settings);
        ApplyAndSave();
    }

    /// <summary>
    /// Says once that Windows is refusing the corrections, and why. A balloon rather than a
    /// message box: the user is in the middle of typing into the very window that caused it,
    /// and stealing focus to explain would be worse than the problem.
    /// </summary>
    private void ShowBlockedNotice()
    {
        _icon.BalloonTipTitle = Text.BlockedTitle;
        _icon.BalloonTipText = Text.BlockedBody;
        _icon.BalloonTipIcon = ToolTipIcon.Warning;
        _icon.ShowBalloonTip(10_000);
    }

    private async Task CheckForUpdatesAsync(bool announceWhenCurrent)
    {
        var result = await UpdateCheck.RunAsync().ConfigureAwait(true);

        if (result.Error is not null)
        {
            OnLogged($"update check failed: {result.Error}");

            if (announceWhenCurrent)
            {
                MessageBox.Show(string.Format(Text.UpdateFailed, result.Error), Text.UpdateTitle);
            }

            return;
        }

        if (!result.Available)
        {
            OnLogged($"update check: running the newest version ({result.Current})");

            if (announceWhenCurrent)
            {
                MessageBox.Show(string.Format(Text.UpdateCurrent, result.Current), Text.UpdateTitle);
            }

            return;
        }

        var answer = MessageBox.Show(
            string.Format(Text.UpdateAvailable, result.Latest, result.Current),
            Text.UpdateTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = result.PageUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            OnLogged($"could not open the download page: {ex.Message}");
        }
    }

    private void EditHotkeys()
    {
        // The dialog captures raw key events, including the hotkey currently in force.
        _service.SuspendHotkeys(true);

        try
        {
            using var form = new HotkeySettingsForm(
                _settings.ConvertWordHotkey,
                _settings.ConvertSelectionHotkey,
                _settings.UndoHotkey);

            if (form.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _settings.ConvertWordHotkey = form.WordHotkey;
            _settings.ConvertSelectionHotkey = form.SelectionHotkey;
            _settings.UndoHotkey = form.UndoHotkey;
            _convertWordItem.Text = ConvertWordCaption();
            _convertLineItem.Text = ConvertLineCaption();
            _undoItem.Text = UndoCaption();
            _currentTag = string.Empty;
            ApplyAndSave();
        }
        finally
        {
            _service.SuspendHotkeys(false);
        }
    }

    private static void ShowAbout()
    {
        using var about = new AboutForm();
        about.ShowDialog();
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _enabledItem.Checked = _settings.Enabled;
        ApplyAndSave();
    }

    private void ToggleAutoDetect()
    {
        _settings.AutoDetectEnabled = !_settings.AutoDetectEnabled;
        _autoDetectItem.Checked = _settings.AutoDetectEnabled;
        ApplyAndSave();
    }

    private void ToggleSound()
    {
        _settings.PlaySoundOnConvert = !_settings.PlaySoundOnConvert;
        _soundItem.Checked = _settings.PlaySoundOnConvert;
        ApplyAndSave();
    }

    private void ToggleDiagnostics()
    {
        _settings.DiagnosticsEnabled = !_settings.DiagnosticsEnabled;
        _diagnosticsItem.Checked = _settings.DiagnosticsEnabled;
        ApplyAndSave();
    }

    /// <summary>
    /// Unlike its neighbours this one saves nothing: <see cref="StartupRegistration"/> is
    /// where autostart is recorded and where the menu is ticked from. Mirroring it into the
    /// settings file would add a copy that an install, or an edit made outside the
    /// application, could leave contradicting the registry.
    /// </summary>
    private void ToggleStartup()
    {
        bool enable = !StartupRegistration.IsEnabled();

        try
        {
            StartupRegistration.SetEnabled(enable, Environment.ProcessPath ?? string.Empty);
            _startupItem.Checked = enable;
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Text.ErrorStartup, ex.Message), "Switch Please");
        }
    }

    private void ApplyAndSave()
    {
        _service.ApplySettings(_settings);
        UpdateIcon();

        try
        {
            SettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            OnLogged($"could not save settings: {ex.Message}");
        }
    }

    private void ShowDiagnostics()
    {
        var latency = _service.Latency;
        var layouts = _service.InstalledLayouts;
        var active = _service.Layouts.GetActiveLayout();

        var report = new StringBuilder();
        report.AppendLine(string.Format(
            Text.StatusHook, _service.IsRunning ? Text.StatusInstalled : Text.StatusNotInstalled));

        if (_service.HookReinstalls > 0)
        {
            report.AppendLine(string.Format(Text.StatusHookRecovered, _service.HookReinstalls));
        }
        report.AppendLine(string.Format(
            Text.StatusProcessing, _settings.Enabled ? Text.StatusOn : Text.StatusOff));
        report.AppendLine(string.Format(
            Text.StatusAutoDetect, _settings.AutoDetectEnabled ? Text.StatusOn : Text.StatusOff));
        report.AppendLine();
        report.AppendLine(Text.StatusLayouts);

        foreach (var layout in layouts)
        {
            string marker = layout.Handle == active?.Handle ? Text.StatusActiveMarker : string.Empty;
            report.AppendLine($"  {layout}{marker}");
        }

        if (active is not null && layouts.Count > 1)
        {
            report.AppendLine();
            report.AppendLine(Text.StatusMaps);

            foreach (var other in layouts)
            {
                if (other.Handle == active.Handle)
                {
                    continue;
                }

                int mapped = _service.Layouts.GetMap(active, other).MappedCharacters;
                report.AppendLine(string.Format(Text.StatusMapLine, active.ShortTag, other.ShortTag, mapped));
            }
        }

        report.AppendLine();

        var dictionaries = _service.DictionaryLanguages;
        report.AppendLine(string.Format(
            Text.StatusDictionaries,
            dictionaries.Count == 0 ? Text.StatusDictionariesNone : string.Join(", ", dictionaries)));
        report.AppendLine(string.Format(Text.StatusLanguages, _service.DescribeLanguages()));
        report.AppendLine();

        report.AppendLine(string.Format(
            Text.StatusWorker,
            _service.WorkerRunning ? Text.StatusWorkerRunning : Text.StatusWorkerStopped));
        report.AppendLine(string.Format(
            Text.StatusQueue, _service.Queued, _service.SinceLastEvent.TotalSeconds));
        report.AppendLine(string.Format(Text.StatusCorrections, _service.Corrections));
        report.AppendLine(string.Format(Text.StatusSlowest, _service.SlowestHandleMilliseconds));
        report.AppendLine();

        if (_settings.DiagnosticsEnabled && latency.Samples > 0)
        {
            report.AppendLine(Text.StatusLatencyTitle);
            report.AppendLine(string.Format(Text.StatusLatencySamples, latency.Samples));
            report.AppendLine(string.Format(Text.StatusLatencyAverage, latency.AverageMicroseconds));
            report.AppendLine(string.Format(Text.StatusLatencyMaximum, latency.MaximumMicroseconds));
            report.AppendLine(string.Format(Text.StatusLatencyDropped, latency.Dropped));
            report.AppendLine();
            report.AppendLine(Text.StatusLatencyLimit);
        }
        else
        {
            report.AppendLine(Text.StatusLatencyDisabled);
        }

        lock (_recentLog)
        {
            if (_recentLog.Count > 0)
            {
                report.AppendLine();
                report.AppendLine(Text.StatusRecentEvents);

                foreach (string line in _recentLog)
                {
                    report.AppendLine($"  {line}");
                }
            }
        }

        using var window = new StatusForm(Text.StatusTitle, report.ToString());
        window.ShowDialog();
    }

    private static void OpenSettingsFolder()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = SettingsStore.DirectoryPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Text.ErrorOpenFolder, ex.Message), "Switch Please");
        }
    }

    /// <summary>
    /// Starts the icon's typewriter sweep. Restarting it from the beginning is deliberate:
    /// several corrections in quick succession should look like one continuous run rather
    /// than queue up.
    /// </summary>
    private void PlayCorrectionAnimation()
    {
        _animationFrame = 0;
        _animationTimer.Stop();
        _animationTimer.Start();
        DrawIcon();
    }

    private void AdvanceAnimation()
    {
        _animationFrame++;

        if (_animationFrame > TrayIconFactory.AnimationFrames)
        {
            _animationTimer.Stop();
            _animationFrame = -1;
        }

        DrawIcon();
    }

    private void OnTick()
    {
        // Which application the user is in, so the menu can offer to exclude the one they
        // were just looking at. Asked here rather than when the menu opens, because opening
        // it moves focus to the taskbar.
        _service.NoteForegroundApplication();
        _service.ReportHookRecovery();

        // Adding a keyboard layout is how the user tells this application about a new
        // language, and Windows broadcasts nothing a background process can subscribe to.
        // The check is a single call against a list two entries long.
        try
        {
            _service.RefreshLayoutsIfChanged();
        }
        catch (Exception ex)
        {
            OnLogged($"could not refresh layouts: {ex.Message}");
        }

        UpdateIcon();
    }

    private void UpdateIcon()
    {
        string tag = _service.Layouts.GetActiveLayout()?.ShortTag ?? "??";
        bool active = _settings.Enabled && _service.IsRunning;
        string key = $"{tag}|{active}|{Localizer.Effective.Tag}";

        if (key == _currentTag)
        {
            return;
        }

        _currentTag = key;
        DrawIcon(tag, active);

        // A tray tooltip is length-limited, so this stays terse rather than spelling both
        // commands out in full.
        _icon.Text = active
            ? string.Format(
                Text.TooltipActive, tag, _settings.ConvertWordHotkey, _settings.ConvertSelectionHotkey)
            : Text.TooltipDisabled;
    }

    private void DrawIcon()
    {
        DrawIcon(
            _service.Layouts.GetActiveLayout()?.ShortTag ?? "??",
            _settings.Enabled && _service.IsRunning);
    }

    private void DrawIcon(string tag, bool active)
    {
        var previous = _currentIcon;
        _currentIcon = TrayIconFactory.Create(tag, active, _animationFrame);
        _icon.Icon = _currentIcon;
        previous?.Dispose();
    }

    /// <summary>
    /// Queues <paramref name="action"/> on the interface thread without waiting for it.
    ///
    /// Never Invoke here. The worker raises these events holding a lock that this thread
    /// also takes, so blocking on the interface thread from inside that lock deadlocks the
    /// pair of them, and an interface thread that stops answering is one Windows closes.
    /// </summary>
    private void PostToUi(Action action)
    {
        if (_uiMarshal.IsDisposed)
        {
            return;
        }

        try
        {
            _uiMarshal.BeginInvoke(action);
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
        catch (InvalidOperationException)
        {
            // The handle went away between the check and the call.
        }
    }

    private void OnLogged(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss} {message}";

        lock (_recentLog)
        {
            _recentLog.Add(line);

            if (_recentLog.Count > 20)
            {
                _recentLog.RemoveAt(0);
            }
        }

        if (!_settings.DiagnosticsEnabled)
        {
            return;
        }

        // Twenty lines in a dialog is not enough to chase a misfire that happened minutes
        // ago, so diagnostics mode also keeps them on disk.
        try
        {
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            RollLogIfLarge();
            File.AppendAllText(SettingsStore.LogPath, line + Environment.NewLine);
        }
        catch (Exception)
        {
            // Logging must never be the reason a correction fails.
        }
    }

    /// <summary>
    /// Starts a new log once the current one passes the configured size, keeping exactly one
    /// previous file.
    ///
    /// Left to itself this file grows for as long as diagnostics stay on -- and it is a
    /// record of what a keyboard hook saw, which is the last thing that should be quietly
    /// accumulating on someone's disk without a ceiling.
    /// </summary>
    private void RollLogIfLarge()
    {
        long limit = Math.Max(_settings.LogMaximumBytes, 64 * 1024);
        var log = new FileInfo(SettingsStore.LogPath);

        if (!log.Exists || log.Length < limit)
        {
            return;
        }

        string previous = SettingsStore.LogPath + ".1";

        File.Delete(previous);
        File.Move(SettingsStore.LogPath, previous);
    }

    private void ExitApplication()
    {
        _icon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        // Called twice on a normal exit: once by Application.Run when the message loop
        // ends, once by the using block in Main.
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _iconTimer.Stop();
            _iconTimer.Dispose();
            _animationTimer.Stop();
            _animationTimer.Dispose();
            _service.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            _currentIcon?.Dispose();
            _uiMarshal.Dispose();
        }

        base.Dispose(disposing);
    }
}
