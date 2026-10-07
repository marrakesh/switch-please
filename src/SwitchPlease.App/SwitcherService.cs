using System.Media;
using SwitchPlease.Core.Config;
using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Layouts;
using SwitchPlease.Win32;

namespace SwitchPlease.App;

/// <summary>
/// The worker that turns raw keystrokes into corrections.
///
/// Everything here runs on one background thread draining the hook's queue, which is what
/// lets the hook callback itself stay trivial. Nothing in this class is allowed to run
/// inside the callback.
/// </summary>
public sealed class SwitcherService : IDisposable
{
    private readonly KeyboardHook _hook;
    private readonly KeyboardLayoutService _layouts;

    // Consulted wherever text is scored. Falls back to saying nothing when Windows has no
    // dictionary for the language in question.
    private readonly WindowsSpellChecker _dictionary = new();

    // Rebuilt whenever the installed layouts change, so the languages the switcher reasons
    // about always match the ones the user has actually added to Windows.
    private LanguageCatalog _languages = LanguageCatalog.BuiltIn;
    private readonly TypingBuffer _buffer = new();

    // Everything the recorder does is arithmetic on the buffer, and all of it is tested in
    // Core. What is left here is the part only Windows can answer.
    private readonly TypingRecorder _recorder;

    // The resolver for the layout last seen. Worker thread only: nothing else records
    // keystrokes.
    private LayoutResolver? _resolver;

    // The layout a correction asked the focused window to switch to, waiting to be checked.
    // Written and taken under _bufferGate, because corrections start on either thread.
    private LayoutInfo? _layoutToVerify;

    // Every judgement about what a correction should do lives here, in Core, where it can be
    // tested without a keyboard hook. Rebuilt with the catalog when the layouts change.
    private ConversionPlanner _planner;

    // The buffer is normally touched only by the worker thread, but the tray menu can ask
    // for a conversion from the UI thread. Uncontended this costs nothing, and it is far
    // away from the hook callback, so it cannot affect input latency.
    private readonly Lock _bufferGate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SelectionReader _selection;

    private AppSettings _settings;
    private IWrongLayoutDetector _detector = DisabledDetector.Instance;

    // A snapshot rather than the list in the settings, which the interface thread edits. A
    // word learned here goes into the snapshot at once, so it is honoured from the very next
    // keystroke rather than once the interface has caught up.
    private WordExceptions _neverCorrect = WordExceptions.Empty;
    private Thread? _worker;
    private nint _lastWindow;
    private int _disposed;

    // What the last correction changed, so it can be put back. Guarded by _bufferGate.
    private CorrectionUndo? _undo;

    // Raised once per run rather than on every blocked keystroke: a window belonging to an
    // elevated process blocks every correction into it, and saying so each time would bury
    // the log in one repeated line.
    private int _reportedBlockedInput;

    // Whether the "a game has the screen" note has already been logged for the current
    // spell. Touched only under _bufferGate.
    private bool _reportedFullscreen;

    // Last reinstall count that was written to the log, so a recovery is reported once.
    private long _reportedReinstalls;
    private long _reportedFalseAlarms;

    private long _corrections;
    private long _lastEventTicks = Environment.TickCount64;
    private long _slowestHandleMilliseconds;
    private bool _reportedSlowHandling;

    /// <remarks>
    /// Takes nothing from the user interface. It used to be handed a way to run code on the
    /// interface thread, for the clipboard; that is what let a jammed clipboard stop the
    /// whole application, so the worker does its own now.
    /// </remarks>
    public SwitcherService(AppSettings settings)
    {
        _settings = settings;
        _selection = new SelectionReader(Log);
        _recorder = new TypingRecorder(_buffer);
        _hook = new KeyboardHook();
        _layouts = new KeyboardLayoutService();
        _languages = LanguageCatalog.FromLayouts(_layouts.DescribeLanguages());
        _planner = new ConversionPlanner(_layouts, _languages, _dictionary);

        ApplySettings(settings);
    }

    /// <summary>Diagnostic messages, raised on the worker thread.</summary>
    public event Action<string>? Logged;

    /// <summary>
    /// Raised the first time Windows refuses to deliver a correction, so the tray can
    /// explain the one cause the user can do something about.
    /// </summary>
    public event Action<InputBlockedException>? InputBlocked;

    /// <summary>Raised on the worker thread after a correction has been typed.</summary>
    public event Action? Corrected;

    /// <summary>
    /// Raised on the worker thread when undoing an automatic correction has put a word on the
    /// never-correct list, so the settings can be saved and the user told.
    /// </summary>
    public event Action<string>? WordLearned;

    public KeyboardLayoutService Layouts => _layouts;

    public IReadOnlyList<LayoutInfo> InstalledLayouts => _layouts.InstalledLayouts;

    public HookLatency Latency => _hook.GetLatency();

    /// <summary>
    /// How many times Windows dropped the keyboard hook and it had to be put back. Shown in
    /// the status window, because anything above zero means corrections were being missed
    /// with no sign of it anywhere else.
    /// </summary>
    public long HookReinstalls => _hook.Reinstalls;

    /// <summary>
    /// Notes in the log when the hooks have been rebuilt, and says which of the two things
    /// happened. Called on the tray's tick, because the hook thread has no business
    /// formatting messages.
    ///
    /// The distinction is the whole point. This line used to read "Windows had dropped the
    /// keyboard hook" every time, which was a guess dressed as a fact: the watchdog only ever
    /// knew that the callback had gone quiet while the machine was being used. It is now
    /// asked, and the answer decides the wording -- a hook that turned out to be installed
    /// all along is a different fault, and reinstalling it is not the cure.
    /// </summary>
    public void ReportHookRecovery()
    {
        long count = _hook.Reinstalls;

        if (count == _reportedReinstalls)
        {
            return;
        }

        long alarms = _hook.FalseAlarms;
        bool stillInstalled = alarms != _reportedFalseAlarms;

        _reportedReinstalls = count;
        _reportedFalseAlarms = alarms;

        Log(stillInstalled
            ? $"the hooks went quiet for over 5 s while the machine was in use, but were still "
                + $"installed; they have been rebuilt anyway ({alarms} of {count} this session)."
            : $"Windows had dropped the keyboard hook; it has been put back ({count} so far this session).");
    }

    /// <summary>Languages Windows can spell-check here, shown in the diagnostics view.</summary>
    public IReadOnlyList<string> DictionaryLanguages => _dictionary.SupportedLanguages();

    public bool IsRunning => _hook.IsRunning;

    /// <summary>Whether the thread that turns keystrokes into corrections is still alive.</summary>
    public bool WorkerRunning => _worker is { IsAlive: true };

    /// <summary>Keystrokes waiting to be handled. Anything but zero for long is a symptom.</summary>
    public int Queued => _hook.Queued;

    /// <summary>How long ago the worker last handled a keystroke.</summary>
    public TimeSpan SinceLastEvent =>
        TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastEventTicks));

    /// <summary>Corrections made this session, by hotkey or automatically.</summary>
    public long Corrections => Interlocked.Read(ref _corrections);

    /// <summary>The longest a single keystroke has taken to handle, in milliseconds.</summary>
    public long SlowestHandleMilliseconds => Interlocked.Read(ref _slowestHandleMilliseconds);

    /// <summary>
    /// Executable name of the application the user was last in, e.g. "chrome.exe".
    ///
    /// Not "whatever has focus right now": by the time the tray menu is open the foreground
    /// window is the taskbar, and asking then names the shell. Tracked as the user moves
    /// around instead.
    ///
    /// Updated from two places, and it needs both. Typing in a window is the strongest
    /// signal that it is the one meant, but an application worth excluding is often one the
    /// user has not typed a word into yet -- which is the whole reason for excluding it.
    /// </summary>
    public string LastActiveProcess { get; private set; } = string.Empty;

    /// <summary>
    /// Notes which application has focus, ignoring the shell and this program itself.
    /// Called on the tray's tick.
    /// </summary>
    public void NoteForegroundApplication()
    {
        nint window = ForegroundWindowInfo.Handle;

        if (window == 0 || ForegroundWindowInfo.IsShellSurface(window))
        {
            return;
        }

        string process = ForegroundWindowInfo.GetProcessName(window);

        if (process.Length > 0 && !string.Equals(process, OwnProcessName, StringComparison.OrdinalIgnoreCase))
        {
            LastActiveProcess = process;
        }
    }

    /// <summary>Our own executable, which is never a candidate for exclusion.</summary>
    private static readonly string OwnProcessName =
        Path.GetFileName(Environment.ProcessPath ?? string.Empty).ToLowerInvariant();

    public void Start()
    {
        _hook.Start();

        _worker = new Thread(WorkerLoop)
        {
            Name = "SwitchPlease.Worker",
            IsBackground = true,
        };

        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();

        Log($"Started. Layouts: {string.Join(", ", _layouts.InstalledLayouts.Select(l => l.ShortTag))}");
        Log($"Languages modelled: {DescribeLanguages()}");
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;

        _hook.Enabled = settings.Enabled;
        _hook.SetHotkeys(
            settings.ConvertWordHotkey,
            settings.ConvertSelectionHotkey,
            settings.UndoHotkey,
            settings.DoubleTapWindowMilliseconds,
            settings.DoubleTapHoldMilliseconds);

        // Start measuring from zero each time it is switched on, so the reported average
        // describes the session the user is actually looking at.
        bool wasMeasuring = _hook.DiagnosticsEnabled;
        _hook.DiagnosticsEnabled = settings.DiagnosticsEnabled;

        if (settings.DiagnosticsEnabled && !wasMeasuring)
        {
            _hook.ResetLatency();
        }

        // Built whether or not automatic correction is on globally: it can be switched on
        // for a single application, and whether it applies is decided per keystroke by
        // AutoDetectIn. A detector built only for the global switch left those applications
        // with one that never fires.
        _detector = new HeuristicWrongLayoutDetector(
            settings.AutoDetectSensitivity,
            _dictionary,
            _languages,
            settings.MinimumAutoWordLength);

        _neverCorrect = new WordExceptions(settings.NeverCorrectWords);
    }

    /// <summary>
    /// Re-reads installed layouts, e.g. after the user adds or removes one.
    ///
    /// Called from the tray's thread while the worker is running. The expensive part --
    /// asking Windows what every key types under every layout -- is done first and outside
    /// the lock; only the swap happens inside it, so the worker cannot find itself holding
    /// the old catalog and the new planner at the same time.
    /// </summary>
    public void RefreshLayouts()
    {
        _layouts.Refresh();

        var languages = LanguageCatalog.FromLayouts(_layouts.DescribeLanguages());
        var planner = new ConversionPlanner(_layouts, languages, _dictionary);

        lock (_bufferGate)
        {
            _languages = languages;
            _planner = planner;

            // The detector holds a reference to the old catalog.
            ApplySettings(_settings);
        }

        Log($"Layouts refreshed: {string.Join(", ", _layouts.InstalledLayouts.Select(l => l.ShortTag))}");
        Log($"Languages modelled: {DescribeLanguages()}");
    }

    /// <summary>
    /// Re-reads the layouts if Windows now reports a different set than we were built from.
    ///
    /// Adding a layout is how a user tells this application about a new language, and until
    /// this ran it took a trip through the tray menu for the switcher to notice. There is no
    /// notification to subscribe to, so the tray's existing once-a-second tick asks instead.
    /// </summary>
    /// <returns>True when the list had changed and was reloaded.</returns>
    public bool RefreshLayoutsIfChanged()
    {
        if (!_layouts.InstalledLayoutsChanged())
        {
            return false;
        }

        RefreshLayouts();
        return true;
    }

    /// <summary>Which languages the switcher can judge, and on what evidence.</summary>
    public string DescribeLanguages() => string.Join(", ", _languages.Profiles.Select(p =>
    {
        string evidence = p.HasStatisticalModel ? "model" : "layout";

        if (_dictionary.HasDictionary(p.LanguageTag))
        {
            evidence += "+dictionary";
        }

        return $"{p.Name} ({evidence})";
    }));

    /// <summary>Converts the last typed word, as if the hotkey had been pressed.</summary>
    public void ConvertLastWordNow()
    {
        lock (_bufferGate)
        {
            ConvertLastWord(automatic: false);
        }

        VerifyLayoutSwitch();
    }

    /// <summary>Converts everything typed since the last Enter, click or focus change.</summary>
    public void ConvertLineNow()
    {
        lock (_bufferGate)
        {
            ConvertWholeBuffer();
        }

        VerifyLayoutSwitch();
    }

    /// <summary>
    /// Stops the hook reacting while the user is choosing a new hotkey, so that pressing
    /// the current one during capture does not also rewrite whatever they were typing.
    /// </summary>
    public void SuspendHotkeys(bool suspended) => _hook.Enabled = !suspended && _settings.Enabled;

    /// <summary>
    /// Safe to call more than once, which it is: WinForms disposes the application context
    /// when the message loop ends, and again when the using block in Main unwinds.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        _worker?.Join(TimeSpan.FromSeconds(2));
        _hook.Dispose();
        _shutdown.Dispose();
    }

    private void WorkerLoop()
    {
        try
        {
            var token = _shutdown.Token;

            while (_hook.TryRead(out var keyEvent, token))
            {
                long started = Environment.TickCount64;

                try
                {
                    Handle(keyEvent);
                }
                catch (InputBlockedException ex)
                {
                    // Not an error in this program: the focused window is elevated and
                    // Windows will not let a correction into it.
                    ReportBlocked(ex);
                    _buffer.Clear();
                }
                catch (Exception ex)
                {
                    // One bad keystroke must never take the worker down: the switcher would
                    // go silently dead while the tray icon still claimed it was running.
                    Log($"Error handling key {keyEvent.VirtualKey:X2}: {ex.Message}");
                    _buffer.Clear();
                }
                finally
                {
                    NoteHandled(started);
                }
            }
        }
        catch (Exception ex)
        {
            // An escaping exception on a background thread takes the whole process with it,
            // which the user sees as a crash dialog on exit rather than a clean shutdown.
            Log($"worker stopped: {ex.Message}");
        }
    }

    private void Handle(in RawKeyEvent keyEvent)
    {
        if (keyEvent.Hotkey != HotkeyAction.None)
        {
            // A chord fires on key down, a double tap on the release that completes it.
            // The hook tags exactly the one event that triggered, so filtering by key
            // state here would silently swallow every double tap.
            lock (_bufferGate)
            {
                DropBufferIfCaretMoved();

                // The exclusion list has to be honoured here as well as while recording.
                // Only the typing path used to check it, which left the selection hotkey
                // free to fire inside a password manager -- where it would ask the focused
                // window to copy, and put whatever was selected there on the clipboard.
                if (!IsBlockedWindow(ForegroundWindowInfo.Handle, "hotkey")
                    && !IsPlayingOrPresenting("hotkey"))
                {
                    RunHotkey(keyEvent.Hotkey);
                }
            }

            VerifyLayoutSwitch();
            return;
        }

        if (!keyEvent.IsKeyDown)
        {
            return;
        }

        lock (_bufferGate)
        {
            DropBufferIfCaretMoved();
            Record(keyEvent);
        }

        VerifyLayoutSwitch();
    }

    /// <summary>
    /// Reports a layout that would not switch.
    ///
    /// Run after the buffer lock has been released, and after the corrected text has already
    /// been delivered, so the wait costs the user nothing. It exists because the documented
    /// way to switch a window's layout is a request the window may simply ignore -- Electron
    /// and UWP applications routinely do -- and the symptom is text that came out right
    /// followed by a next word that went in wrong again.
    ///
    /// Called from both threads: the worker after every keystroke, and the tray after each of
    /// its menu commands. The request is taken under the lock that set it, so neither can pick
    /// up half of the other's, and the waiting happens outside it.
    /// </summary>
    private void VerifyLayoutSwitch()
    {
        LayoutInfo? pending;

        lock (_bufferGate)
        {
            pending = _layoutToVerify;
            _layoutToVerify = null;
        }

        if (pending is not { } layout)
        {
            return;
        }

        const int LimitMilliseconds = 150;
        const int StepMilliseconds = 15;

        for (int waited = 0; waited < LimitMilliseconds; waited += StepMilliseconds)
        {
            if (KeyboardLayoutService.HasLayout(layout))
            {
                return;
            }

            Thread.Sleep(StepMilliseconds);
        }

        Log($"layout not confirmed switched to {layout.ShortTag}");
    }

    /// <summary>
    /// Forgets what was typed when the caret has moved somewhere we cannot account for.
    /// A mouse click is the case keystrokes alone cannot reveal, and acting on a stale
    /// buffer would delete text the user never typed.
    /// </summary>
    private void DropBufferIfCaretMoved()
    {
        if (_hook.TakeClick() && _buffer.Count > 0)
        {
            Log("buffer dropped: mouse click moved the caret");
            Forget();
        }
    }

    private void Record(in RawKeyEvent keyEvent)
    {
        // Focus moved: the caret is somewhere else now, so what we recorded no longer
        // describes the text in front of the user.
        nint window = ForegroundWindowInfo.Handle;

        if (window != _lastWindow)
        {
            _lastWindow = window;
            Forget();
        }

        string process = ForegroundWindowInfo.GetProcessName(window);

        if (process.Length > 0)
        {
            // Recorded even when excluded, so the menu entry offering to exclude an
            // application can name one that is already on the list and let it be removed.
            LastActiveProcess = process;
        }

        if (IsExcluded(window))
        {
            Forget();
            return;
        }

        // The cheap probe only, because this runs on every keystroke. It recognises native
        // edit controls; the thorough one runs before anything is rewritten.
        if (_settings.RespectPasswordFields && SecureInputGuard.LooksLikePasswordField(window))
        {
            Forget();
            return;
        }

        if (_settings.PauseInFullscreenApps && FullscreenGuard.ShouldStandAside(window))
        {
            Forget();
            return;
        }

        // Classified here, before handing the key to the recorder, so that the two expensive
        // questions below are only asked of a key that could produce a character. The
        // recorder classifies it again, which costs nothing: the policy is a switch.
        LayoutInfo? activeLayout = null;

        if (KeystrokePolicy.Classify(keyEvent.VirtualKey, keyEvent.Modifiers) == KeystrokeEffect.Append)
        {
            activeLayout = _layouts.GetActiveLayout();

            if (activeLayout is null)
            {
                return;
            }

            // While an input method editor is assembling a character the keys pressed and
            // the text produced are different things. Recording it would be meaningless and
            // correcting it would destroy the composition.
            if (KeyboardLayoutService.IsComposing(window))
            {
                Forget();
                return;
            }
        }

        // One layout for the whole keystroke: the scan code and the character it produces
        // have to come from the same one, or they describe different keys.
        var outcome = _recorder.Record(
            keyEvent.VirtualKey,
            keyEvent.ScanCode,
            keyEvent.Modifiers,
            activeLayout is null ? NoLayout.Instance : ResolverFor(activeLayout));

        // Anything the user types makes the last correction no longer the thing in front of
        // the caret, so there is nothing left to put back. Only a key that changed something
        // counts: the two Shift presses of the undo gesture itself come through here as
        // Ignored, and treating those as typing would make the hotkey unable to reverse its
        // own correction.
        if (outcome != RecordingOutcome.Ignored)
        {
            _undo = null;
        }

        if (outcome == RecordingOutcome.WordEnded && _settings.AutoDetectIn(process))
        {
            TryAutoConvert();
        }
    }

    /// <summary>
    /// Drops the record of what was typed, and with it the offer to undo.
    ///
    /// The two belong together. Undo works by erasing a known number of characters at the
    /// caret, so it is only meaningful while the caret is still where the correction left it.
    /// Once that stops being true -- a different window, a password field, a game -- keeping
    /// the offer around means the tray menu can be used to delete whatever happens to be
    /// under the caret instead.
    /// </summary>
    private void Forget()
    {
        _recorder.Discard();
        _undo = null;
    }

    /// <summary>
    /// What each key types under one particular layout.
    ///
    /// Bound to a layout rather than looking the active one up per call. Asking twice for a
    /// single keystroke left a window in which the layout could change between the two
    /// questions, and the stroke would then be recorded with a scan code from one layout and
    /// a character from another -- a pair that replays as neither reading when the word is
    /// later converted.
    /// </summary>
    private sealed class LayoutResolver(nint handle) : ICharacterResolver
    {
        public nint Handle { get; } = handle;

        public ushort ScanCodeFor(ushort virtualKey) =>
            KeyboardLayoutService.ScanCodeFor(virtualKey, Handle);

        public char Resolve(ushort scanCode, ModifierKeys modifiers) =>
            KeyboardLayoutService.ResolveCharacter(scanCode, modifiers, Handle);
    }

    /// <summary>Stands in where the recorder will not ask: a key that types nothing.</summary>
    private sealed class NoLayout : ICharacterResolver
    {
        public static NoLayout Instance { get; } = new();

        public ushort ScanCodeFor(ushort virtualKey) => 0;

        public char Resolve(ushort scanCode, ModifierKeys modifiers) => '\0';
    }

    /// <summary>
    /// A resolver for <paramref name="layout"/>, reusing the last one. Kept so that an
    /// ordinary keystroke allocates nothing; a new one appears only when the layout changes,
    /// and only the worker thread touches this.
    /// </summary>
    private LayoutResolver ResolverFor(LayoutInfo layout) =>
        _resolver is { } existing && existing.Handle == layout.Handle
            ? existing
            : _resolver = new LayoutResolver(layout.Handle);

    private void RunHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ConvertWord:
                ConvertLastWord(automatic: false);
                break;

            case HotkeyAction.ConvertSelection:
                ConvertSelection();
                break;

            case HotkeyAction.Undo:
                Undo();
                break;
        }
    }

    private void TryAutoConvert()
    {
        var range = _buffer.GetLastWord();

        if (range.IsEmpty)
        {
            return;
        }

        if (_neverCorrect.Contains(_buffer.GetText(range)))
        {
            Log("auto: left alone, the word is on the never-correct list");
            return;
        }

        var plan = BuildPlan(range);

        if (plan is null)
        {
            return;
        }

        if (plan.SwitchesLayout)
        {
            var context = new StrokeRange(0, range.Start);

            var verdict = _detector.Evaluate(
                plan.Intended,
                plan.Converted,
                _buffer.GetText(context),
                context.IsEmpty ? string.Empty : _planner.Render(_buffer, context, plan.TargetLayout));

            Log($"auto: {Redact(plan.Original)} -> {Redact(plan.Converted)} [{verdict.Reason}]");

            if (verdict.ShouldConvert)
            {
                ApplyAfterDelay(plan);
                return;
            }

            // The other layout did not win, but the capitals can still be wrong: "пРИВЕТ"
            // typed on the Russian keyboard is Russian, just upside down.
            if (!plan.FixesCapsLock || _layouts.GetActiveLayout() is not { } active
                || _planner.BuildCapsLockFix(_buffer, range, active) is not { } fix)
            {
                return;
            }

            plan = fix;
        }

        // A Caps Lock slip on its own needs no detector: the Shift held inside it is the
        // evidence, and it is far stronger than letter statistics. The guards still apply,
        // so a code or a path typed with Caps Lock on is left as it is.
        if (TextGuards.IsIneligible(plan.Converted, _settings.MinimumAutoWordLength, out string reason))
        {
            Log($"auto: Caps Lock left on, word left alone [{reason}]");
            return;
        }

        Log($"auto: {Redact(plan.Original)} -> {Redact(plan.Converted)} [Caps Lock left on]");
        ApplyAfterDelay(plan);
    }

    private void ApplyAfterDelay(ConversionPlan plan)
    {
        // The keystroke that ended the word is still in flight to the application.
        Thread.Sleep(Math.Clamp(_settings.CorrectionDelayMilliseconds, 0, 200));

        Apply(plan, automatic: true);
    }

    private void ConvertLastWord(bool automatic)
    {
        var range = _buffer.GetLastWord();

        if (range.IsEmpty)
        {
            Log("hotkey: nothing buffered to convert");
            return;
        }

        // Pressing the hotkey again on a word this program just corrected puts it back.
        if (!automatic && _undo is { } previous && previous.Reverses(_buffer, range))
        {
            Undo();
            return;
        }

        var plan = BuildPlan(range);

        if (plan is null || plan.Converted == plan.Original)
        {
            Log("hotkey: nothing would change");
            return;
        }

        Log($"{(automatic ? "auto" : "hotkey")}: {Redact(plan.Original)} -> {Redact(plan.Converted)}");
        Apply(plan, automatic);
    }

    /// <summary>
    /// Asks the planner what this correction should do. Everything the service adds is the
    /// active layout, which is a question only Windows can answer.
    /// </summary>
    private ConversionPlan? BuildPlan(StrokeRange range)
    {
        var activeLayout = _layouts.GetActiveLayout();

        return activeLayout is null
            ? null
            : _planner.Build(_buffer, range, activeLayout, Log);
    }

    private void Apply(ConversionPlan plan, bool automatic)
    {
        if (IsProtectedField())
        {
            Log("left alone: the caret is in a password field");
            return;
        }

        InputSender.SendBackspaces(plan.EraseCount);
        InputSender.SendText(plan.Converted + plan.Suffix, _settings.TypewriterMillisecondsPerCharacter);

        // Keep typing in the layout the user evidently meant. Whether it took is checked
        // after the buffer lock is released: the documented route is a posted message, so the
        // answer is not available yet and waiting for it here would hold up the worker.
        if (plan.SwitchesLayout && !KeyboardLayoutService.SetActiveLayout(plan.TargetLayout))
        {
            _layoutToVerify = plan.TargetLayout;
        }

        if (plan.FixesCapsLock)
        {
            InputSender.TurnOffCapsLock();
        }

        RewriteBuffer(plan);

        // Enough to type the original back over what is now on screen. The caret is exactly
        // where it started, so the same erase count applies in reverse.
        _undo = CorrectionUndo.ForWord(plan) with { Automatic = automatic };

        Announce();
    }

    /// <summary>
    /// Puts back what the last correction changed.
    ///
    /// Pressing the hotkey again already reverses a word, because the buffer keeps the
    /// original scan codes. This exists for the two cases where that does not work: a
    /// correction the switcher made on its own, which the user never asked for and has no
    /// obvious way to reverse, and a converted selection, which leaves nothing in the buffer
    /// to convert back.
    /// </summary>
    public void UndoLastNow()
    {
        lock (_bufferGate)
        {
            Undo();
        }
    }

    private void Undo()
    {
        if (_undo is null)
        {
            Log("undo: nothing to put back");
            return;
        }

        var undo = _undo;

        // One shot. The text is only where we left it until the user types anything else,
        // and a second undo would erase whatever that was.
        _undo = null;

        if (IsProtectedField())
        {
            return;
        }

        try
        {
            InputSender.SendBackspaces(undo.Wrote.Length);
            InputSender.SendText(undo.Restore);
        }
        catch (InputBlockedException ex)
        {
            ReportBlocked(ex);
            return;
        }

        undo.RestoreBuffer(_buffer);

        Log($"undo: {Redact(undo.Wrote)} -> {Redact(undo.Restore)}");

        if (undo.Automatic)
        {
            Learn(undo.Original);
        }
    }

    /// <summary>
    /// Puts a word whose automatic correction was just undone on the never-correct list.
    ///
    /// Straight away, on the first undo. The user has just said, as plainly as they can,
    /// that the word was right; correcting it again tomorrow and making them say it again is
    /// the behaviour that gets automatic correction switched off for good.
    /// </summary>
    private void Learn(string word)
    {
        if (_neverCorrect.Contains(word) || WordExceptions.Normalize(word).Length == 0)
        {
            return;
        }

        _neverCorrect = _neverCorrect.With(word);
        Log($"remembered {Redact(word)}: automatic correction will leave it alone");

        // Raised while the buffer lock is held, like Corrected; the tray posts it.
        WordLearned?.Invoke(WordExceptions.Normalize(word));
    }

    /// <summary>
    /// Updates our record so it matches what is now on screen. The scan codes are kept, so
    /// pressing the hotkey a second time converts the word straight back.
    /// </summary>
    private void RewriteBuffer(ConversionPlan plan)
    {
        var strokes = _buffer.Strokes;

        for (int i = 0; i < plan.Word.Length; i++)
        {
            int index = plan.Word.Start + i;
            var stroke = strokes[index];
            _buffer.Replace(index, stroke with { Character = plan.Converted[i] });
        }
    }

    private void ConvertSelection()
    {
        if (IsProtectedField())
        {
            Log("selection: the caret is in a password field");
            return;
        }

        string? selected = ReadSelection();

        if (string.IsNullOrWhiteSpace(selected))
        {
            // Nothing highlighted: fall back to the whole line we have been recording.
            ConvertWholeBuffer();
            return;
        }

        // Only while Caps Lock is still on. The characters alone cannot tell "пРИВЕТ" from
        // "mRNA"; that the key which turns letters round is down right now can.
        bool capsLockSlip = InputSender.IsCapsLockOn && CapsLockSlip.LooksInverted(selected);
        string converted;
        LayoutInfo? target;

        if (capsLockSlip)
        {
            (converted, target) = _planner.FixCapsLockSelection(selected);
        }
        else if (_planner.ChooseMapFor(selected) is { } map)
        {
            (converted, target) = (map.Convert(selected), map.Target);
        }
        else
        {
            Log("no other keyboard layout to switch to");
            return;
        }

        if (string.Equals(converted, selected, StringComparison.Ordinal))
        {
            Log("selection: nothing would change");
            return;
        }

        Log($"selection: {Redact(selected)} -> {Redact(converted)}{(capsLockSlip ? " [Caps Lock left on]" : string.Empty)}");

        // The selection is still highlighted, so typing over it replaces it.
        InputSender.SendText(converted, _settings.TypewriterMillisecondsPerCharacter);

        if (target is not null && !KeyboardLayoutService.SetActiveLayout(target))
        {
            _layoutToVerify = target;
        }

        if (capsLockSlip)
        {
            InputSender.TurnOffCapsLock();
        }

        _buffer.Clear();

        // Nothing is left in the buffer to convert back, so undo is the only way out of a
        // selection the switcher read the wrong way.
        _undo = CorrectionUndo.ForSelection(converted, selected);

        Announce();
    }

    private void ConvertWholeBuffer()
    {
        var all = _buffer.GetAll();

        if (all.IsEmpty)
        {
            Log("selection: nothing selected and nothing buffered");
            return;
        }

        var plan = BuildPlan(all);

        if (plan is null || plan.Converted == plan.Original)
        {
            return;
        }

        Log($"line: {Redact(plan.Original)} -> {Redact(plan.Converted)}");
        Apply(plan, automatic: false);
    }

    private string? ReadSelection()
    {
        try
        {
            return _selection.Read();
        }
        catch (InputBlockedException ex)
        {
            ReportBlocked(ex);
            return null;
        }
    }

    /// <summary>
    /// Whether the switcher stays out of the application owning <paramref name="window"/>.
    /// The rule itself lives with the settings, where it is tested alongside the
    /// per-application ones it has to take precedence over.
    /// </summary>
    private bool IsExcluded(nint window) =>
        _settings.ExcludedProcesses.Count > 0
        && _settings.IsExcluded(ForegroundWindowInfo.GetProcessName(window));

    /// <summary>
    /// Whether the switcher should keep out of <paramref name="window"/> entirely.
    /// </summary>
    private bool IsBlockedWindow(nint window, string what)
    {
        if (IsExcluded(window))
        {
            Log($"{what}: ignored, this application is on the exclusion list");
            Forget();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a game or a presentation has the screen, in which case the switcher does
    /// nothing at all. Reported once per spell rather than per keystroke: the condition
    /// lasts as long as the game is in front, and saying so each time would fill the log.
    /// </summary>
    private bool IsPlayingOrPresenting(string what)
    {
        if (!_settings.PauseInFullscreenApps || !FullscreenGuard.ShouldStandAside(ForegroundWindowInfo.Handle))
        {
            _reportedFullscreen = false;
            return false;
        }

        if (!_reportedFullscreen)
        {
            _reportedFullscreen = true;
            Log($"{what}: ignored, a full-screen application has the screen");
        }

        Forget();
        return true;
    }

    private bool IsProtectedField() =>
        _settings.RespectPasswordFields
        && SecureInputGuard.IsPasswordFieldFocused(ForegroundWindowInfo.Handle);

    private void Announce()
    {
        if (_settings.PlaySoundOnConvert)
        {
            SystemSounds.Asterisk.Play();
        }

        // Raised while the buffer lock is held, so whoever handles it must not block. The
        // tray posts to its own thread rather than waiting for it.
        Interlocked.Increment(ref _corrections);
        Corrected?.Invoke();
    }

    /// <summary>
    /// Says once that corrections are not getting through, and why. The condition lasts as
    /// long as the elevated window has focus, so repeating it would fill the log with one
    /// line and tell the user nothing they did not learn the first time.
    /// </summary>
    private void ReportBlocked(InputBlockedException exception)
    {
        if (Interlocked.Exchange(ref _reportedBlockedInput, 1) != 0)
        {
            return;
        }

        Log("Windows refused to deliver the correction. This happens when the focused window "
            + "belongs to a program running as administrator: it will not accept synthesised "
            + "input from one that is not.");

        InputBlocked?.Invoke(exception);
    }

    /// <summary>
    /// Hides the text itself unless the user has explicitly asked for it in the log.
    ///
    /// The log of a keyboard hook is a transcript of everything typed while it was on, and
    /// leaving one on disk by default is not a reasonable thing to do to someone who only
    /// switched on latency measurement. The length is enough to follow what happened.
    /// </summary>
    private string Redact(string value)
    {
        if (!_settings.LogTextContent)
        {
            return $"<{value.Length} chars>";
        }

        return $"\"{Trim(value)}\"";
    }

    private static string Trim(string value) =>
        value.Length <= 40 ? value : string.Concat(value.AsSpan(0, 37), "...");

    /// <summary>
    /// Records how long one keystroke took, and complains once if it took absurdly long.
    ///
    /// There is no deadline to enforce: the two things that can hang here are SendInput and
    /// the clipboard, and neither can be abandoned partway without leaving the user's text in
    /// a worse state than not trying. What can be done is to notice and say so, because the
    /// alternative is a queue that quietly stops draining and a switcher that looks fine.
    /// </summary>
    private void NoteHandled(long startedTicks)
    {
        const long ComplainAboveMilliseconds = 2000;

        long elapsed = Environment.TickCount64 - startedTicks;

        Interlocked.Exchange(ref _lastEventTicks, Environment.TickCount64);

        if (elapsed > Interlocked.Read(ref _slowestHandleMilliseconds))
        {
            Interlocked.Exchange(ref _slowestHandleMilliseconds, elapsed);
        }

        if (elapsed < ComplainAboveMilliseconds || _reportedSlowHandling)
        {
            return;
        }

        _reportedSlowHandling = true;
        Log($"a single keystroke took {elapsed} ms to handle. Something outside this program "
            + "is holding on to the clipboard or refusing synthesised input.");
    }

    private void Log(string message) => Logged?.Invoke(message);
}
