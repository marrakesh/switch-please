using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using SwitchPlease.Core.Config;
using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Threading;

namespace SwitchPlease.Win32;

/// <summary>
/// The WH_KEYBOARD_LL hook, living on a thread of its own. It also carries a WH_MOUSE_LL
/// hook on the same thread, purely to notice clicks: a click moves the caret without
/// producing a keystroke, and the recorded text has to be abandoned when that happens.
///
/// Two rules govern everything in this class:
///
/// 1. A low-level hook runs on the thread that installed it, and that thread must pump
///    messages. We give it a dedicated thread with its own GetMessage loop rather than
///    borrowing the UI thread, so a busy settings window can never stall input.
///
/// 2. The callback must return inside LowLevelHooksTimeout (300 ms by default) or Windows
///    quietly stops calling it. So the callback only compares a few integers and writes one
///    struct into a lock-free queue: no allocation, no I/O, no locks, nothing that can be
///    paused by the garbage collector for long.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    /// <summary>
    /// Stamped into every keystroke we synthesise, so the callback can recognise its own
    /// echo and ignore it instead of treating a correction as fresh typing.
    /// </summary>
    public const nuint InjectionTag = 0x5350_0001;

    private const int QueueCapacity = 1024;

    /// <summary>How often the hook thread checks that it is still being called.</summary>
    private const uint WatchdogIntervalMilliseconds = 2000;

    // Bit positions for the raw left/right modifier state we track from the key stream.
    private const int BitLeftShift = 0;
    private const int BitRightShift = 1;
    private const int BitLeftControl = 2;
    private const int BitRightControl = 3;
    private const int BitLeftAlt = 4;
    private const int BitRightAlt = 5;
    private const int BitLeftWin = 6;
    private const int BitRightWin = 7;

    /// <summary>Every bit above, so the whole held-key state can be replaced at once.</summary>
    private const int HeldBits = 0xFF;

    private const ModifierKeys RelevantModifiers =
        ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Win;

    private readonly SpscRingBuffer<RawKeyEvent> _queue = new(QueueCapacity);
    private readonly ManualResetEventSlim _signal = new(false);
    private readonly ManualResetEventSlim _installed = new(false);

    // Held in a field for the lifetime of the hook: if this delegate is collected while
    // Windows still holds the function pointer, the process dies on the next keystroke.
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly NativeMethods.LowLevelMouseProc _mouseCallback;

    private Thread? _thread;
    private uint _threadId;
    private nint _hookHandle;
    private nint _mouseHookHandle;
    private volatile int _clicked;
    private volatile bool _disposed;
    private Exception? _installError;

    private int _modifierBits;
    private volatile int _enabled = 1;

    private readonly HookWatchdog _watchdog = new();

    // Stamped by both callbacks. Read by the watchdog on the same thread, so a plain int is
    // enough, but it is written from the callback and read from the timer handler, which are
    // the same thread only because both are the hook thread. Volatile says so out loud.
    private volatile int _lastCallbackTicks;

    private nuint _watchdogTimer;
    private long _reinstalls;

    // One object holding every binding, swapped wholesale when settings change. A single
    // volatile read in the callback then gives a consistent set: with a field per hotkey the
    // callback could see half of one configuration and half of the next.
    private volatile HotkeyBindings _bindings = HotkeyBindings.Empty;

    // Diagnostics, written only by the callback thread and read for reporting.
    private long _callbackTicksMax;
    private long _callbackTicksTotal;
    private long _callbackCount;
    private volatile int _diagnostics;

    public KeyboardHook()
    {
        _callback = HookCallback;
        _mouseCallback = MouseCallback;
        SetHotkeys(Hotkey.ConvertWord, Hotkey.ConvertSelection, Hotkey.None);
    }

    /// <summary>
    /// Reports whether a mouse button has been pressed since the last call, and clears the
    /// flag.
    ///
    /// A click moves the caret without producing a single keystroke, so without this the
    /// recorded text would still be believed to sit in front of the cursor. Correcting then
    /// would delete whatever the user had clicked into instead -- worse than doing nothing.
    /// </summary>
    public bool TakeClick() => Interlocked.Exchange(ref _clicked, 0) != 0;

    public bool IsRunning => _thread is { IsAlive: true } && _hookHandle != 0;

    /// <summary>
    /// How many times this session the hook had to be put back after Windows dropped it.
    /// Anything above zero is worth showing the user: it means corrections were being missed.
    /// </summary>
    public long Reinstalls => Interlocked.Read(ref _reinstalls);

    /// <summary>
    /// Keystrokes waiting for the worker. Steadily above zero means the worker is falling
    /// behind; stuck above zero means it has stopped.
    /// </summary>
    public int Queued => _queue.Count;

    /// <summary>When false the hook stays installed but reports nothing and suppresses nothing.</summary>
    public bool Enabled
    {
        get => _enabled != 0;
        set => _enabled = value ? 1 : 0;
    }

    public bool DiagnosticsEnabled
    {
        get => _diagnostics != 0;
        set => _diagnostics = value ? 1 : 0;
    }

    /// <param name="convertWord">Bound to <see cref="HotkeyAction.ConvertWord"/>.</param>
    /// <param name="convertSelection">Bound to <see cref="HotkeyAction.ConvertSelection"/>.</param>
    /// <param name="undo">Bound to <see cref="HotkeyAction.Undo"/>. May be unset.</param>
    /// <param name="tapWindowMilliseconds">Longest gap between the two presses of a double tap.</param>
    /// <param name="tapHoldMilliseconds">Longest either press of a double tap may last.</param>
    public void SetHotkeys(
        Hotkey convertWord,
        Hotkey convertSelection,
        Hotkey undo,
        int tapWindowMilliseconds = 500,
        int tapHoldMilliseconds = 400)
    {
        // Clamped rather than validated: these come from a hand-edited settings file, and a
        // typo there should not leave the hotkey impossible to trigger.
        uint window = (uint)Math.Clamp(tapWindowMilliseconds, 120, 2000);
        uint hold = (uint)Math.Clamp(tapHoldMilliseconds, 80, 2000);

        _bindings = HotkeyBindings.For([convertWord, convertSelection, undo], window, hold);
    }

    /// <summary>
    /// One immutable snapshot of every hotkey binding.
    ///
    /// Parallel arrays rather than an array of objects, because the callback walks them on
    /// every keystroke and an array of ints is one cache line rather than three chased
    /// pointers.
    /// </summary>
    private sealed class HotkeyBindings(int[] chords, DoubleTapTracker?[] taps)
    {
        internal static HotkeyBindings Empty { get; } = For([], 500, 400);

        internal readonly int[] Chords = chords;

        internal readonly DoubleTapTracker?[] Taps = taps;

        internal static HotkeyBindings For(Hotkey[] hotkeys, uint window, uint hold)
        {
            var chords = new int[HotkeyActions.Count];
            var taps = new DoubleTapTracker?[HotkeyActions.Count];

            for (int i = 0; i < HotkeyActions.Count; i++)
            {
                var hotkey = i < hotkeys.Length ? hotkeys[i] : Hotkey.None;

                chords[i] = Pack(hotkey);
                taps[i] = hotkey is { Kind: HotkeyKind.DoubleTap, IsSet: true }
                    ? new DoubleTapTracker(hotkey.VirtualKey, window, hold)
                    : null;
            }

            return new HotkeyBindings(chords, taps);
        }
    }

    /// <summary>Installs the hook and blocks until it is live (or the attempt fails).</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_thread is not null)
        {
            return;
        }

        // Warm the callback so the very first keystroke does not pay for JIT.
        PrepareCallback();

        ResyncHeldModifiers();

        // Seeded, not left at zero: otherwise the first watchdog tick sees a callback that
        // has "not run since the epoch" and rebuilds a hook that was installed a second ago.
        _lastCallbackTicks = Environment.TickCount;

        _thread = new Thread(ThreadMain)
        {
            Name = "SwitchPlease.KeyboardHook",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _installed.Wait(TimeSpan.FromSeconds(5));

        if (_installError is not null)
        {
            throw new InvalidOperationException("Could not install the keyboard hook.", _installError);
        }
    }

    /// <summary>
    /// Blocks until a keystroke is available or the token fires. Called only from the
    /// single worker thread that drains this hook.
    /// </summary>
    public bool TryRead(out RawKeyEvent keyEvent, CancellationToken cancellationToken)
    {
        while (!_disposed && !cancellationToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out keyEvent))
            {
                return true;
            }

            try
            {
                _signal.Reset();

                // Re-check after the reset: the producer may have signalled in between.
                if (_queue.TryDequeue(out keyEvent))
                {
                    return true;
                }

                _signal.Wait(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                // Shutdown raced ahead of this reader; there is nothing left to read.
                break;
            }
        }

        keyEvent = default;
        return false;
    }

    public HookLatency GetLatency()
    {
        long count = Interlocked.Read(ref _callbackCount);
        long total = Interlocked.Read(ref _callbackTicksTotal);
        long max = Interlocked.Read(ref _callbackTicksMax);

        static double ToMicroseconds(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

        return new HookLatency(
            count,
            count == 0 ? 0 : ToMicroseconds(total / count),
            ToMicroseconds(max),
            _queue.DroppedCount);
    }

    public void ResetLatency()
    {
        Interlocked.Exchange(ref _callbackCount, 0);
        Interlocked.Exchange(ref _callbackTicksTotal, 0);
        Interlocked.Exchange(ref _callbackTicksMax, 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _signal.Dispose();
        _installed.Dispose();
    }

    private void ThreadMain()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        try
        {
            _hookHandle = InstallHook();
            _mouseHookHandle = InstallMouseHook();
        }
        catch (Exception ex)
        {
            _installError = ex;
            _installed.Set();
            return;
        }

        _installed.Set();

        // A thread timer rather than a background thread with a sleep: a low-level hook
        // belongs to the message queue of the thread that installed it, so putting it back
        // has to happen here and nowhere else.
        _watchdogTimer = NativeMethods.SetTimer(0, 0, WatchdogIntervalMilliseconds, 0);

        while (!_disposed)
        {
            int result = NativeMethods.GetMessageW(out var message, 0, 0, 0);

            if (result is 0 or -1)
            {
                break;
            }

            if (message.Message == NativeMethods.WM_TIMER && message.Hwnd == 0)
            {
                ReinstallIfDropped();
                continue;
            }

            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessageW(ref message);
        }

        if (_watchdogTimer != 0)
        {
            NativeMethods.KillTimer(0, _watchdogTimer);
            _watchdogTimer = 0;
        }

        RemoveHooks();
    }

    /// <summary>
    /// Puts the hooks back if Windows has silently taken them away.
    ///
    /// It does that to any low-level hook whose callback overruns LowLevelHooksTimeout, and
    /// it says nothing: no error, no notification, and the handle stays as valid-looking as
    /// before. The switcher then sits in the tray with its icon showing, doing nothing.
    ///
    /// Runs on the hook thread, which is the only thread allowed to install these.
    /// </summary>
    private void ReinstallIfDropped()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return;
        }

        if (_watchdog.LooksDropped(
                (uint)Environment.TickCount, (uint)_lastCallbackTicks, info.Time))
        {
            RemoveHooks();
        }

        // Each hook is put back on its own, and a handle left at zero is reason enough to try
        // again on the next tick without waiting for the watchdog to agree.
        //
        // That second half is the important one. Installing the keyboard hook and then
        // failing on the mouse hook -- which Windows does around session locks and desktop
        // switches -- used to leave the pair half alive for good: the live keyboard hook kept
        // stamping the callback clock, so the watchdog was satisfied for ever and nothing
        // noticed the missing mouse hook. Click detection stayed dead, the typing record
        // stopped being dropped when the caret moved, and the next correction sent its
        // backspaces wherever the user had clicked.
        bool restored = Restore(ref _hookHandle, InstallHook);
        restored |= Restore(ref _mouseHookHandle, InstallMouseHook);

        if (restored)
        {
            // Whatever happened between the hook going quiet and being put back was not seen
            // here, and a release that fell into that gap would otherwise be believed held
            // for the rest of the session.
            ResyncHeldModifiers();

            _lastCallbackTicks = Environment.TickCount;
            Interlocked.Increment(ref _reinstalls);
        }
    }

    /// <summary>
    /// Installs one hook if it is missing. Reports whether it had to.
    /// </summary>
    private static bool Restore(ref nint handle, Func<nint> install)
    {
        if (handle != 0)
        {
            return false;
        }

        try
        {
            handle = install();
            return handle != 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Windows refused. The handle stays at zero, so the next tick tries again.
            handle = 0;
            return false;
        }
    }

    private void RemoveHooks()
    {
        if (_hookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }

        if (_mouseHookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = 0;
        }
    }

    /// <summary>
    /// The mouse hook shares this thread's message loop. Its callback is even more
    /// restricted than the keyboard one: pointer movement arrives hundreds of times a
    /// second, so anything but a button press returns immediately without touching state.
    /// </summary>
    private nint InstallMouseHook()
    {
        nint module = NativeMethods.GetModuleHandleW(null);
        nint handle = NativeMethods.SetWindowsMouseHookExW(NativeMethods.WH_MOUSE_LL, _mouseCallback, module, 0);

        if (handle == 0)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }

        return handle;
    }

    private nint MouseCallback(int nCode, nint wParam, nint lParam)
    {
        // Every pointer movement lands here, which makes this the liveliest proof available
        // that the hooks are still being called.
        _lastCallbackTicks = Environment.TickCount;

        if (nCode == NativeMethods.HC_ACTION)
        {
            int message = (int)wParam;

            if (message is NativeMethods.WM_LBUTTONDOWN
                or NativeMethods.WM_RBUTTONDOWN
                or NativeMethods.WM_MBUTTONDOWN)
            {
                _clicked = 1;
            }
        }

        return NativeMethods.CallNextHookEx(0, nCode, wParam, lParam);
    }

    private nint InstallHook()
    {
        nint module = NativeMethods.GetModuleHandleW(null);
        nint handle = NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, _callback, module, 0);

        if (handle == 0)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }

        return handle;
    }

    private unsafe nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        long started = _diagnostics != 0 ? Stopwatch.GetTimestamp() : 0;

        _lastCallbackTicks = Environment.TickCount;

        if (nCode != NativeMethods.HC_ACTION)
        {
            return NativeMethods.CallNextHookEx(0, nCode, wParam, lParam);
        }

        ref readonly var data = ref Unsafe.AsRef<NativeMethods.KBDLLHOOKSTRUCT>(lParam.ToPointer());

        int message = (int)wParam;
        bool isKeyDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        ushort virtualKey = (ushort)data.VirtualKeyCode;

        // Believing a modifier is held after the user let go of it is the one error here
        // with a visible consequence: every key after it is recorded as its shifted
        // character, and the next correction comes out in capitals. So whenever we think
        // something is down, check with the input system before this event is applied --
        // which costs nothing while ordinary text is being typed, because then there is
        // nothing to check.
        if (_modifierBits != 0)
        {
            ResyncHeldModifiers();
        }

        // Modifier state is tracked even while disabled so it is correct the moment we
        // are switched back on.
        TrackModifier(virtualKey, isKeyDown);

        bool suppress = false;

        if (_enabled != 0 && data.ExtraInfo != InjectionTag)
        {
            var modifiers = CurrentModifiers();
            var hotkey = HotkeyAction.None;
            var bindings = _bindings;
            var taps = bindings.Taps;

            // Every tracker must see every event, including releases and keys that are not
            // its own, so none may be skipped once another has matched. Hence the call
            // before the test rather than inside it.
            for (int i = 0; i < taps.Length; i++)
            {
                var tracker = taps[i];
                bool tapped = tracker is not null && tracker.Feed(virtualKey, isKeyDown, data.Time);

                if (tapped && hotkey == HotkeyAction.None)
                {
                    hotkey = HotkeyActions.At(i);
                }
            }

            if (hotkey == HotkeyAction.None && isKeyDown)
            {
                int probe = (((int)(modifiers & RelevantModifiers)) << 16) | virtualKey;
                int[] chords = bindings.Chords;

                for (int i = 0; i < chords.Length; i++)
                {
                    if (probe == chords[i])
                    {
                        hotkey = HotkeyActions.At(i);
                        suppress = true;
                        break;
                    }
                }
            }

            // Only a chord is swallowed. A double tap is recognised on the modifier's
            // release, and suppressing that release would leave the application believing
            // the modifier is still held down.

            _queue.TryEnqueue(new RawKeyEvent(
                virtualKey,
                (ushort)data.ScanCode,
                modifiers,
                isKeyDown,
                hotkey,
                started));

            _signal.Set();
        }

        if (started != 0)
        {
            RecordLatency(Stopwatch.GetTimestamp() - started);
        }

        return suppress ? 1 : NativeMethods.CallNextHookEx(0, nCode, wParam, lParam);
    }

    /// <summary>
    /// Applies one key event to the record of which modifiers are held.
    ///
    /// A keyboard says which of the two Shift keys was pressed, so most events name a side.
    /// Synthesised input does not have to: the on-screen keyboard, remote desktop clients and
    /// automation tools all send the neutral VK_SHIFT, and those used to be ignored outright,
    /// which left the switcher recording their keystrokes unshifted. A press that names no
    /// side is taken as the left one -- a guess, but a press seen only through the neutral
    /// code is released through it too, and a neutral release clears both sides.
    /// </summary>
    private void TrackModifier(ushort virtualKey, bool isKeyDown)
    {
        int mask = virtualKey switch
        {
            VirtualKeys.LShift => 1 << BitLeftShift,
            VirtualKeys.RShift => 1 << BitRightShift,
            VirtualKeys.LControl => 1 << BitLeftControl,
            VirtualKeys.RControl => 1 << BitRightControl,
            VirtualKeys.LMenu => 1 << BitLeftAlt,
            VirtualKeys.RMenu => 1 << BitRightAlt,
            VirtualKeys.LWin => 1 << BitLeftWin,
            VirtualKeys.RWin => 1 << BitRightWin,
            VirtualKeys.Shift => (1 << BitLeftShift) | (1 << BitRightShift),
            VirtualKeys.Control => (1 << BitLeftControl) | (1 << BitRightControl),
            VirtualKeys.Menu => (1 << BitLeftAlt) | (1 << BitRightAlt),
            _ => 0,
        };

        if (mask == 0)
        {
            return;
        }

        int bits = _modifierBits;

        // Setting the lowest bit of the mask sets exactly one side; clearing the whole mask
        // clears both, because leaving either of them set is the failure that matters.
        _modifierBits = isKeyDown ? bits | (mask & -mask) : bits & ~mask;
    }

    /// <summary>
    /// Replaces what we believe is held with what the input system says is held.
    ///
    /// Needed because the key stream this class watches has gaps in it and none of them
    /// announce themselves. Windows stops calling a low-level hook that answers too slowly;
    /// the secure desktop behind a UAC prompt, Ctrl+Alt+Del or the lock screen delivers
    /// nothing to hooks at all. Either can swallow the release of a key whose press we saw,
    /// and nothing puts that right on its own: a Shift released inside one of those gaps
    /// stays "held" until the user next happens to press and release Shift. Until then every
    /// keystroke is recorded as a capital -- invisible on screen, because the application got
    /// the real one, and revealed only when a correction is typed back in shouting.
    /// </summary>
    private void ResyncHeldModifiers()
    {
        int bits = 0;

        if (IsPhysicallyDown(VirtualKeys.LShift)) bits |= 1 << BitLeftShift;
        if (IsPhysicallyDown(VirtualKeys.RShift)) bits |= 1 << BitRightShift;
        if (IsPhysicallyDown(VirtualKeys.LControl)) bits |= 1 << BitLeftControl;
        if (IsPhysicallyDown(VirtualKeys.RControl)) bits |= 1 << BitRightControl;
        if (IsPhysicallyDown(VirtualKeys.LMenu)) bits |= 1 << BitLeftAlt;
        if (IsPhysicallyDown(VirtualKeys.RMenu)) bits |= 1 << BitRightAlt;
        if (IsPhysicallyDown(VirtualKeys.LWin)) bits |= 1 << BitLeftWin;
        if (IsPhysicallyDown(VirtualKeys.RWin)) bits |= 1 << BitRightWin;

        _modifierBits = (_modifierBits & ~HeldBits) | bits;
    }

    /// <summary>
    /// Whether a key is down right now, asked of the input system rather than of this
    /// thread's message queue -- the hook thread reads no key messages, so GetKeyState
    /// would answer for a keyboard it has never seen.
    /// </summary>
    private static bool IsPhysicallyDown(ushort virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private ModifierKeys CurrentModifiers()
    {
        int bits = _modifierBits;
        var modifiers = ModifierKeys.None;

        if ((bits & ((1 << BitLeftShift) | (1 << BitRightShift))) != 0) modifiers |= ModifierKeys.Shift;
        if ((bits & ((1 << BitLeftControl) | (1 << BitRightControl))) != 0) modifiers |= ModifierKeys.Control;
        if ((bits & ((1 << BitLeftAlt) | (1 << BitRightAlt))) != 0) modifiers |= ModifierKeys.Alt;
        if ((bits & ((1 << BitLeftWin) | (1 << BitRightWin))) != 0) modifiers |= ModifierKeys.Win;
        // Asked of Windows rather than counted from the key stream, because counting was
        // wrong in both directions and each error stuck for the rest of the session. Windows
        // toggles Caps Lock once per press however many key-downs the press produces, so a
        // repeat while the key is held flipped our count and not its state; and a press that
        // never reaches us at all -- on the lock screen, or where Windows is set to clear
        // Caps Lock with Shift instead of with the key -- flipped its state and not our
        // count. The low bit of GetKeyState is the toggle, and unlike the held state it is
        // reported correctly to a thread that reads no key messages.
        if ((NativeMethods.GetKeyState(VirtualKeys.Capital) & 1) != 0)
        {
            modifiers |= ModifierKeys.CapsLock;
        }

        return modifiers;
    }

    /// <summary>
    /// Called only from the hook thread, but the counters are read and reset from another,
    /// so every update goes through Interlocked. A plain increment racing with the reset in
    /// ResetLatency would lose it, and on a 32-bit or weakly ordered machine a plain read of
    /// a long can tear outright.
    /// </summary>
    private void RecordLatency(long elapsed)
    {
        Interlocked.Increment(ref _callbackCount);
        Interlocked.Add(ref _callbackTicksTotal, elapsed);

        long observed = Interlocked.Read(ref _callbackTicksMax);

        while (elapsed > observed)
        {
            long previous = Interlocked.CompareExchange(ref _callbackTicksMax, elapsed, observed);

            if (previous == observed)
            {
                break;
            }

            observed = previous;
        }
    }

    private void PrepareCallback()
    {
        try
        {
            var method = typeof(KeyboardHook).GetMethod(
                nameof(HookCallback),
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method is not null)
            {
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
            }
        }
        catch (Exception)
        {
            // Pre-JIT is an optimisation; tiered compilation handles it either way.
        }
    }

    /// <summary>
    /// Chord probes are always non-negative, so -1 is a value no keystroke can produce and
    /// serves as "this hotkey is neither a chord nor bound at all".
    /// </summary>
    private const int NoChord = -1;

    private static int Pack(Hotkey hotkey) => hotkey.Kind == HotkeyKind.DoubleTap || !hotkey.IsSet
        ? NoChord
        : (((int)(hotkey.Modifiers & RelevantModifiers)) << 16) | hotkey.VirtualKey;
}

/// <summary>Measured cost of the hook callback itself, in microseconds.</summary>
public readonly record struct HookLatency(long Samples, double AverageMicroseconds, double MaximumMicroseconds, long Dropped);
