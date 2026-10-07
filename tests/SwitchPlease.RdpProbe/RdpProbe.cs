#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property AllowUnsafeBlocks=true
#:property PublishAot=false

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SwitchPlease.RdpProbe;

/// <summary>
/// Measures what the switcher meets in a Remote Desktop window, on the client side, before
/// any of it is built on.
///
/// None of these questions can be answered by reading documentation, and each one decides
/// whether a correction typed into a remote session comes out right:
///
/// 1. Does a low-level keyboard hook see the keys at all while the client has focus? A
///    full-screen session installs a hook of its own, newer than ours and so called first,
///    and may swallow keys rather than pass them on.
/// 2. Does the layout Windows reports for the client's thread follow the layout of the
///    remote session? The switcher reads characters through the local one; the remote one
///    decides what appears on screen.
/// 3. Do characters sent as KEYEVENTF_UNICODE arrive in the remote session intact?
/// 4. Does a layout switch made locally -- ActivateKeyboardLayout, WM_INPUTLANGCHANGEREQUEST,
///    or the switching shortcut itself -- reach the remote session?
///
/// It logs every key the hook sees and every change of foreground window and layout, and
/// takes commands from a file, so it can be driven while the client keeps focus:
///
///   dotnet run tests/SwitchPlease.RdpProbe/RdpProbe.cs [output folder]
///
/// The folder defaults to %TEMP%\SwitchPlease.RdpProbe. Not the repository: the log is a
/// transcript of every key pressed while this runs. Append lines to commands.txt there:
///
///   note TEXT            a marker in the log
///   layouts              the installed layouts
///   rehook               installs the hook again, making it the newest in the chain
///   shot [NAME]          saves a picture of the foreground window to the folder
///   wait MS              pauses before the next command
///   unicode TEXT         types TEXT as KEYEVENTF_UNICODE, a character at a time
///   unicodeburst TEXT    the same in a single SendInput call
///   scan TEXT            presses the keys labelled TEXT on a US keyboard, by scan code
///   vk TEXT              the same by virtual key, with no scan code
///   back N               presses Backspace N times
///   chord NAME           altshift, ctrlshift or winspace
///   activate HKL         ActivateKeyboardLayout on the client's thread, attached to it
///   request HKL          posts WM_INPUTLANGCHANGEREQUEST to the client's window
///   quit
///
/// Every command that types waits up to 20 s for a remote desktop client to have focus and
/// does nothing if none does, so nothing it sends can land in a local window.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string folder = args.Length > 0
            ? args[0]
            : Path.Combine(Path.GetTempPath(), "SwitchPlease.RdpProbe");

        Directory.CreateDirectory(folder);

        string commands = Path.Combine(folder, "commands.txt");
        File.WriteAllText(commands, string.Empty);

        // Physical pixels from GetWindowRect, which is what the screenshots are cut by.
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessPerMonitorV2);

        using var log = new ProbeLog(Path.Combine(folder, "log.txt"));
        using var hook = new HookThread();
        var probe = new Probe(log, hook, folder);

        log.Write($"started; commands are read from {commands}");
        probe.Run("layouts");

        while (true)
        {
            probe.Tick();

            foreach (string command in TakeCommands(commands))
            {
                if (!probe.Run(command))
                {
                    return 0;
                }
            }

            Thread.Sleep(50);
        }
    }

    private static string[] TakeCommands(string path)
    {
        try
        {
            string[] lines = File.ReadAllLines(path);

            if (lines.Length > 0)
            {
                File.WriteAllText(path, string.Empty);
            }

            return lines;
        }
        catch (IOException)
        {
            // Whoever is appending has it open. The lines are still there on the next tick.
            return [];
        }
    }
}

internal sealed class Probe(ProbeLog log, HookThread hook, string folder)
{
    private const int FocusWaitMilliseconds = 20000;

    private string _lastForeground = string.Empty;
    private bool _reportedUnseenInput;

    /// <summary>Logs what the hook saw and anything about the foreground that changed.</summary>
    public void Tick()
    {
        Drain();

        var foreground = Foreground.Read();
        string described = foreground.Describe();

        if (described != _lastForeground)
        {
            log.Write("foreground " + described);
            _lastForeground = described;
        }

        // Input newer than the last key the hook saw means someone else's hook kept it.
        // A hint only: moving the mouse moves the same clock.
        var info = new NativeMethods.LASTINPUTINFO { Size = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return;
        }

        int unseen = (int)(info.Time - (uint)hook.LastCallbackTicks);

        if (unseen > 1500 && foreground.IsRemoteDesktop && !_reportedUnseenInput)
        {
            log.Write($"input {unseen} ms newer than the last key the hook saw (mouse movement counts too)");
            _reportedUnseenInput = true;
        }
        else if (unseen <= 0)
        {
            _reportedUnseenInput = false;
        }
    }

    /// <returns>False when the command was quit.</returns>
    public bool Run(string line)
    {
        line = line.Trim();

        if (line.Length == 0)
        {
            return true;
        }

        if (line != "layouts")
        {
            log.Write("> " + line);
        }

        int space = line.IndexOf(' ', StringComparison.Ordinal);
        string verb = space < 0 ? line : line[..space];
        string argument = space < 0 ? string.Empty : line[(space + 1)..];

        try
        {
            if (verb == "quit")
            {
                return false;
            }

            RunCommand(verb, argument);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException or ExternalException)
        {
            log.Write("error: " + ex.Message);
        }

        Drain();
        return true;
    }

    private void RunCommand(string verb, string argument)
    {
        switch (verb)
        {
            case "note":
                return;

            case "layouts":
                log.Write("layouts: " + string.Join(", ", NativeMethods.Layouts().Select(h => $"{h:X8}")));
                return;

            case "rehook":
                hook.Reinstall();
                return;

            case "shot":
                Shot(argument);
                return;

            case "wait":
                Thread.Sleep(int.Parse(argument, CultureInfo.InvariantCulture));
                return;
        }

        if (!WaitForRemoteDesktop())
        {
            return;
        }

        nint window = Foreground.Read().Window;

        switch (verb)
        {
            case "unicode":
                Typist.Unicode(argument, burst: false);
                break;

            case "unicodeburst":
                Typist.Unicode(argument, burst: true);
                break;

            case "scan":
                Typist.UsKeys(argument, scanCodesOnly: true);
                break;

            case "vk":
                Typist.UsKeys(argument, scanCodesOnly: false);
                break;

            case "back":
                Typist.Backspaces(int.Parse(argument, CultureInfo.InvariantCulture));
                break;

            case "chord":
                Typist.Chord(argument);
                break;

            case "activate":
                Activate(window, ParseLayout(argument));
                break;

            case "request":
                Request(window, ParseLayout(argument));
                break;

            default:
                log.Write("unknown command");
                break;
        }
    }

    private void Drain()
    {
        while (hook.TryTake(out string? line))
        {
            log.Write(line);
        }
    }

    private bool WaitForRemoteDesktop()
    {
        var waited = Stopwatch.StartNew();

        while (waited.ElapsedMilliseconds < FocusWaitMilliseconds)
        {
            if (Foreground.Read().IsRemoteDesktop)
            {
                // Settle, then ask again: focus that only passed through the client is not
                // focus to type into.
                Thread.Sleep(300);
                return Foreground.Read().IsRemoteDesktop;
            }

            Thread.Sleep(100);
        }

        log.Write("skipped: no remote desktop client had focus within 20 s");
        return false;
    }

    private void Activate(nint window, nint layout)
    {
        uint target = NativeMethods.GetWindowThreadProcessId(window, out _);
        uint self = NativeMethods.GetCurrentThreadId();
        bool attached = NativeMethods.AttachThreadInput(self, target, true);

        try
        {
            nint previous = NativeMethods.ActivateKeyboardLayout(layout, 0);
            log.Write($"activate: attached={attached} previous={previous:X8} now={NativeMethods.GetKeyboardLayout(target):X8}");
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(self, target, false);
            }
        }
    }

    private void Request(nint window, nint layout)
    {
        bool posted = NativeMethods.PostMessageW(window, NativeMethods.WM_INPUTLANGCHANGEREQUEST, 0, layout);

        // Posted, so give the client a moment to act on it before asking.
        Thread.Sleep(200);

        uint thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        log.Write($"request: posted={posted} now={NativeMethods.GetKeyboardLayout(thread):X8}");
    }

    private void Shot(string name)
    {
        var foreground = Foreground.Read();

        if (!NativeMethods.GetWindowRect(foreground.Window, out var bounds))
        {
            log.Write("shot: the foreground window has no bounds");
            return;
        }

        int width = bounds.Right - bounds.Left;
        int height = bounds.Bottom - bounds.Top;

        using var bitmap = new Bitmap(width, height);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(width, height));
        }

        string file = Path.Combine(
            folder,
            $"shot-{(name.Length > 0 ? name : DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture))}.png");

        bitmap.Save(file, ImageFormat.Png);
        log.Write($"shot: {file} ({width}x{height}, {foreground.Process})");
    }

    private static nint ParseLayout(string text) =>
        (nint)long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

/// <summary>What has focus, and everything about it that bears on the questions above.</summary>
internal sealed record Foreground(
    nint Window,
    string Process,
    string WindowClass,
    string FocusClass,
    nint Layout,
    bool CoversMonitor,
    int NotificationState)
{
    private static readonly string[] RemoteDesktopClients = ["mstsc.exe", "msrdc.exe", "vmconnect.exe"];

    /// <summary>
    /// A client by process name, or any program hosting the Remote Desktop control, whose
    /// input window is believed to be IHWindowClass. The focus class is logged so that belief
    /// can be checked.
    /// </summary>
    public bool IsRemoteDesktop =>
        RemoteDesktopClients.Contains(Process, StringComparer.Ordinal) || FocusClass == "IHWindowClass";

    public string Describe() =>
        $"{Window:X} {Process} class={WindowClass} focus={FocusClass} layout={Layout:X8} "
        + $"coversMonitor={CoversMonitor} notifications={DescribeNotificationState(NotificationState)}";

    public static Foreground Read()
    {
        nint window = NativeMethods.GetForegroundWindow();
        uint thread = NativeMethods.GetWindowThreadProcessId(window, out uint processId);

        var gui = new NativeMethods.GUITHREADINFO { Size = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        string focus = NativeMethods.GetGUIThreadInfo(thread, ref gui) ? NativeMethods.ClassOf(gui.Focus) : "?";

        int state = NativeMethods.SHQueryUserNotificationState(out int reported) == 0 ? reported : 0;

        return new Foreground(
            window,
            NativeMethods.ProcessName(processId),
            NativeMethods.ClassOf(window),
            focus,
            NativeMethods.GetKeyboardLayout(thread),
            NativeMethods.CoversItsMonitor(window),
            state);
    }

    private static string DescribeNotificationState(int state) => state switch
    {
        1 => "not-present",
        2 => "busy",
        3 => "d3d-full-screen",
        4 => "presentation",
        5 => "accepts",
        6 => "quiet-time",
        7 => "app",
        _ => state.ToString(CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// The low-level keyboard hook, on a thread of its own with its own message loop, as the
/// switcher runs it. The callback only queues a line; the main thread writes it out.
/// </summary>
internal sealed class HookThread : IDisposable
{
    private const uint ReinstallMessage = 0x8001;

    private readonly ConcurrentQueue<string> _lines = new();
    private readonly ManualResetEventSlim _started = new();
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly Thread _thread;

    private uint _threadId;
    private nint _hook;
    private volatile int _lastCallbackTicks = Environment.TickCount;

    public HookThread()
    {
        _callback = Callback;
        _thread = new Thread(Pump) { IsBackground = true, Name = "RdpProbe.Hook" };
        _thread.Start();
        _started.Wait();
    }

    public int LastCallbackTicks => _lastCallbackTicks;

    public bool TryTake([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? line) =>
        _lines.TryDequeue(out line);

    public void Reinstall() => NativeMethods.PostThreadMessageW(_threadId, ReinstallMessage, 0, 0);

    public void Dispose()
    {
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(1));
        _started.Dispose();
    }

    private void Pump()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _hook = Install();

        // Creates the message queue before anyone is told the thread is ready, so a message
        // posted the moment the constructor returns is not lost.
        NativeMethods.PeekMessageW(out _, 0, 0, 0, 0);
        _started.Set();

        while (NativeMethods.GetMessageW(out var message, 0, 0, 0) > 0)
        {
            if (message.Message == ReinstallMessage)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = Install();
                _lines.Enqueue(_hook != 0 ? "hook reinstalled" : "hook could not be reinstalled");
                continue;
            }

            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessageW(ref message);
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
    }

    private nint Install() =>
        NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, _callback, NativeMethods.GetModuleHandleW(null), 0);

    private nint Callback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            _lastCallbackTicks = Environment.TickCount;

            string direction = (int)wParam switch
            {
                NativeMethods.WM_KEYDOWN => "down",
                NativeMethods.WM_KEYUP => "up",
                NativeMethods.WM_SYSKEYDOWN => "sysdown",
                NativeMethods.WM_SYSKEYUP => "sysup",
                _ => $"{wParam:X}",
            };

            string origin = (key.Flags & NativeMethods.LLKHF_INJECTED) == 0
                ? "keyboard"
                : key.ExtraInfo == Typist.Tag ? "probe" : "injected";

            _lines.Enqueue($"key {direction,-7} vk={key.VirtualKey:X2} sc={key.ScanCode:X4} flags={key.Flags:X2} {origin}");
        }

        return NativeMethods.CallNextHookEx(0, code, wParam, lParam);
    }
}

/// <summary>Synthesised input, every event tagged so the hook can tell it apart.</summary>
internal static class Typist
{
    public const nuint Tag = 0x5244_5001;

    private const int MillisecondsBetweenKeys = 25;

    /// <summary>The US layout, so that "scan q" means the key labelled Q wherever it is.</summary>
    private static readonly nint UsLayout = 0x0409_0409;

    public static void Unicode(string text, bool burst)
    {
        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);

        foreach (char c in text)
        {
            inputs.Add(Key(0, c, NativeMethods.KEYEVENTF_UNICODE));
            inputs.Add(Key(0, c, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP));
        }

        if (burst)
        {
            Send([.. inputs]);
            return;
        }

        for (int i = 0; i < inputs.Count; i += 2)
        {
            Send([inputs[i], inputs[i + 1]]);
            Thread.Sleep(MillisecondsBetweenKeys);
        }
    }

    public static void UsKeys(string text, bool scanCodesOnly)
    {
        foreach (char c in text)
        {
            short packed = NativeMethods.VkKeyScanExW(c, UsLayout);

            if (packed == -1)
            {
                continue;
            }

            ushort virtualKey = (ushort)(packed & 0xFF);
            ushort scanCode = (ushort)NativeMethods.MapVirtualKeyExW(virtualKey, NativeMethods.MAPVK_VK_TO_VSC, UsLayout);
            bool shift = (packed & 0x100) != 0;

            if (shift)
            {
                Press(NativeMethods.VK_LSHIFT, 0x2A, up: false, scanCodesOnly);
            }

            Press(virtualKey, scanCode, up: false, scanCodesOnly);
            Press(virtualKey, scanCode, up: true, scanCodesOnly);

            if (shift)
            {
                Press(NativeMethods.VK_LSHIFT, 0x2A, up: true, scanCodesOnly);
            }

            Thread.Sleep(MillisecondsBetweenKeys);
        }
    }

    public static void Backspaces(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Press(NativeMethods.VK_BACK, 0x0E, up: false, scanCodesOnly: false);
            Press(NativeMethods.VK_BACK, 0x0E, up: true, scanCodesOnly: false);
            Thread.Sleep(15);
        }
    }

    /// <summary>A layout-switching shortcut, pressed in order and released in reverse.</summary>
    public static void Chord(string name)
    {
        (ushort VirtualKey, ushort ScanCode, bool Extended)[] keys = name switch
        {
            "altshift" => [(NativeMethods.VK_LMENU, 0x38, false), (NativeMethods.VK_LSHIFT, 0x2A, false)],
            "ctrlshift" => [(NativeMethods.VK_LCONTROL, 0x1D, false), (NativeMethods.VK_LSHIFT, 0x2A, false)],
            "winspace" => [(NativeMethods.VK_LWIN, 0x5B, true), (NativeMethods.VK_SPACE, 0x39, false)],
            _ => throw new ArgumentException("chord is altshift, ctrlshift or winspace", nameof(name)),
        };

        foreach (var key in keys)
        {
            Send([Key(key.VirtualKey, key.ScanCode, key.Extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0)]);
            Thread.Sleep(30);
        }

        foreach (var key in keys.Reverse())
        {
            uint flags = NativeMethods.KEYEVENTF_KEYUP | (key.Extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0);
            Send([Key(key.VirtualKey, key.ScanCode, flags)]);
            Thread.Sleep(30);
        }
    }

    private static void Press(ushort virtualKey, ushort scanCode, bool up, bool scanCodesOnly)
    {
        uint flags = (up ? NativeMethods.KEYEVENTF_KEYUP : 0) | (scanCodesOnly ? NativeMethods.KEYEVENTF_SCANCODE : 0);
        Send([Key(scanCodesOnly ? (ushort)0 : virtualKey, scanCode, flags)]);
    }

    private static NativeMethods.INPUT Key(ushort virtualKey, ushort scanCode, uint flags) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KEYBDINPUT
            {
                VirtualKey = virtualKey,
                ScanCode = scanCode,
                Flags = flags,
                ExtraInfo = Tag,
            },
        },
    };

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"SendInput delivered {sent} of {inputs.Length} events (error {Marshal.GetLastPInvokeError()})");
        }
    }
}

internal sealed class ProbeLog(string path) : IDisposable
{
    private readonly StreamWriter _writer = new(path, append: true, Encoding.UTF8) { AutoFlush = true };

    public void Write(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        _writer.WriteLine(line);
        Console.WriteLine(line);
    }

    public void Dispose() => _writer.Dispose();
}

internal static unsafe class NativeMethods
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    public const uint LLKHF_INJECTED = 0x10;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x1;
    public const uint KEYEVENTF_KEYUP = 0x2;
    public const uint KEYEVENTF_UNICODE = 0x4;
    public const uint KEYEVENTF_SCANCODE = 0x8;
    public const uint MAPVK_VK_TO_VSC = 0;

    public const ushort VK_BACK = 0x08;
    public const ushort VK_SPACE = 0x20;
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_LSHIFT = 0xA0;
    public const ushort VK_LCONTROL = 0xA2;
    public const ushort VK_LMENU = 0xA4;

    public const nint DpiAwarenessPerMonitorV2 = -4;

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint Window;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int Size;
        public uint Flags;
        public nint Active;
        public nint Focus;
        public nint Capture;
        public nint MenuOwner;
        public nint MoveSize;
        public nint Caret;
        public RECT CaretBounds;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int Size;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint Size;
        public uint Time;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    /// <summary>Only here so the union, and with it INPUT, has the size Windows expects.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint Type;
        public InputUnion Data;
    }

    public static nint[] Layouts()
    {
        int count = GetKeyboardLayoutList(0, null);
        var layouts = new nint[count];
        int copied = GetKeyboardLayoutList(count, layouts);

        return layouts[..copied];
    }

    public static string ClassOf(nint window)
    {
        if (window == 0)
        {
            return "-";
        }

        const int Capacity = 128;

        char* buffer = stackalloc char[Capacity];
        int written = GetClassNameW(window, buffer, Capacity);

        return written > 0 ? new string(buffer, 0, written) : "?";
    }

    public static string ProcessName(uint processId)
    {
        nint handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);

        if (handle == 0)
        {
            return "?";
        }

        try
        {
            const int Capacity = 260;

            char* buffer = stackalloc char[Capacity];
            uint size = Capacity;

            return QueryFullProcessImageNameW(handle, 0, buffer, ref size)
                ? Path.GetFileName(new string(buffer, 0, (int)size)).ToLowerInvariant()
                : "?";
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>The same test the switcher's full-screen guard makes.</summary>
    public static bool CoversItsMonitor(nint window)
    {
        if (!GetWindowRect(window, out var bounds))
        {
            return false;
        }

        var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfoW(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), ref info))
        {
            return false;
        }

        return bounds.Left <= info.Monitor.Left
            && bounds.Top <= info.Monitor.Top
            && bounds.Right >= info.Monitor.Right
            && bounds.Bottom >= info.Monitor.Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookExW(int hookId, LowLevelKeyboardProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern int GetMessageW(out MSG message, nint window, uint first, uint last);

    [DllImport("user32.dll")]
    public static extern bool PeekMessageW(out MSG message, nint window, uint first, uint last, uint remove);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll")]
    public static extern nint DispatchMessageW(ref MSG message);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessageW(uint threadId, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandleW(string? name);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    public static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    public static extern int GetKeyboardLayoutList(int count, [Out] nint[]? layouts);

    [DllImport("user32.dll")]
    public static extern nint ActivateKeyboardLayout(nint layout, uint flags);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(nint window, char* buffer, int capacity);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint window, out RECT bounds);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfoW(nint monitor, ref MONITORINFO info);

    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("kernel32.dll")]
    public static extern nint OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageNameW(nint process, uint flags, char* buffer, ref uint size);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, NativeMethods.INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyExW(uint code, uint mapType, nint layout);

    [DllImport("user32.dll")]
    public static extern short VkKeyScanExW(char character, nint layout);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(nint context);
}
