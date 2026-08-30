using System.Runtime.InteropServices;

namespace SwitchPlease.Probe;

/// <summary>
/// Synthesised keystrokes and keyboard layouts, from outside the application under test.
///
/// Keys go out as virtual keys with their real scan codes, never as
/// <c>KEYEVENTF_UNICODE</c>. The switcher works from scan codes, and unicode injection
/// carries none -- text sent that way would arrive on screen and be invisible to the thing
/// being tested. Nor is anything stamped with the switcher's own injection tag, because the
/// whole point is to look like a person.
/// </summary>
internal static class Input
{
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Escape = 0x1B;
    public const ushort Space = 0x20;
    public const ushort Alt = 0x12;

    private const int EnglishPrimaryLanguage = 0x09;

    private static readonly int[] CyrillicPrimaryLanguages =
    [
        0x19, // Russian
        0x22, // Ukrainian
        0x23, // Belarusian
    ];

    public static nint ForegroundWindow => GetForegroundWindow();

    public static void Press(ushort virtualKey)
    {
        Send(virtualKey, up: false);
        Wait.For(25);
        Send(virtualKey, up: true);
    }

    /// <summary>
    /// Presses whatever a binding describes: two taps of a modifier, or a key with modifiers
    /// held around it.
    /// </summary>
    public static void Press(Binding binding)
    {
        if (binding.DoubleTap)
        {
            DoubleTap(binding.VirtualKey);
            return;
        }

        var held = new List<ushort>(3);

        if ((binding.Modifiers & 1) != 0) held.Add(Shift);
        if ((binding.Modifiers & 2) != 0) held.Add(Control);
        if ((binding.Modifiers & 4) != 0) held.Add(Alt);

        foreach (ushort modifier in held)
        {
            Send(modifier, up: false);
        }

        Wait.For(25);
        Send(binding.VirtualKey, up: false);
        Wait.For(25);
        Send(binding.VirtualKey, up: true);
        Wait.For(25);

        // Released in reverse, the way a hand comes off a chord.
        for (int i = held.Count - 1; i >= 0; i--)
        {
            Send(held[i], up: true);
        }
    }

    /// <summary>Two taps inside the recogniser's window, with nothing in between.</summary>
    public static void DoubleTap(ushort virtualKey)
    {
        Send(virtualKey, up: false);
        Wait.For(30);
        Send(virtualKey, up: true);
        Wait.For(120);
        Send(virtualKey, up: false);
        Wait.For(30);
        Send(virtualKey, up: true);
    }

    /// <summary>
    /// Takes the foreground, working around the rule that stops a process stealing it.
    ///
    /// Windows only lets a window come to the front if its process is already in front or
    /// was the last to receive input, and a program launched from a script is neither. The
    /// documented way round it is to attach to the input queue of whoever is in front, ask
    /// while attached, and detach again -- which is what test harnesses do and what this is.
    /// </summary>
    public static bool BringToFront(nint window)
    {
        nint current = GetForegroundWindow();

        if (current == window)
        {
            return true;
        }

        uint theirs = current == 0 ? 0 : GetWindowThreadProcessId(current, out _);
        uint ours = GetCurrentThreadId();
        bool attached = theirs != 0 && theirs != ours && AttachThreadInput(theirs, ours, true);

        try
        {
            ShowWindow(window, ShowNormal);
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(theirs, ours, false);
            }
        }

        return GetForegroundWindow() == window;
    }

    public static nint ActiveLayout(nint window) =>
        GetKeyboardLayout(GetWindowThreadProcessId(window, out _));

    public static bool UseLatinLayout(nint window) => UseLanguage(window, [EnglishPrimaryLanguage]);

    public static bool UseCyrillicLayout(nint window) => UseLanguage(window, CyrillicPrimaryLanguages);

    /// <summary>Puts back whatever layout the window had before the run.</summary>
    public static void UseLayout(nint window, nint layout)
    {
        if (window == 0 || layout == 0)
        {
            return;
        }

        PostMessageW(window, WmInputLangChangeRequest, InputLangChangeForward, layout);
        Wait.For(250);
    }

    public static unsafe string DescribeLayout(nint window)
    {
        nint layout = ActiveLayout(window);

        const int Capacity = 64;
        char* name = stackalloc char[Capacity];

        int written = GetLocaleInfoW((uint)((ulong)layout & 0xFFFF), LocaleIso639LanguageName, name, Capacity);

        // The count includes the terminating null, which is not part of the name.
        return written > 1 ? $"{new string(name, 0, written - 1)} (0x{layout:X})" : $"0x{layout:X}";
    }

    private static bool UseLanguage(nint window, int[] primaryLanguages)
    {
        uint thread = GetWindowThreadProcessId(window, out _);

        foreach (nint layout in Installed())
        {
            if (Array.IndexOf(primaryLanguages, (int)((ulong)layout & 0x3FF)) < 0)
            {
                continue;
            }

            PostMessageW(window, WmInputLangChangeRequest, InputLangChangeForward, layout);
            Wait.For(350);

            if (GetKeyboardLayout(thread) == layout)
            {
                return true;
            }
        }

        return Array.IndexOf(primaryLanguages, (int)((ulong)GetKeyboardLayout(thread) & 0x3FF)) >= 0;
    }

    private static nint[] Installed()
    {
        uint count = GetKeyboardLayoutList(0, null);

        if (count == 0)
        {
            return [];
        }

        var layouts = new nint[count];
        uint copied = GetKeyboardLayoutList((int)count, layouts);

        // A layout removed between the two calls leaves zeros in the tail, and a zero handle
        // describes nothing.
        return copied >= count ? layouts : layouts[..(int)copied];
    }

    private static void Send(ushort virtualKey, bool up)
    {
        var input = new INPUT
        {
            Type = 1,
            Union = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    VirtualKey = virtualKey,
                    Scan = (ushort)MapVirtualKeyW(virtualKey, 0),
                    Flags = up ? 2u : 0u,
                    Time = 0,
                    ExtraInfo = 0,
                },
            },
        };

        if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1)
        {
            throw new InvalidOperationException(
                $"Windows refused the keystroke (error {Marshal.GetLastPInvokeError()}). "
                + "The usual cause is a focused window running as administrator.");
        }
    }

    private const int ShowNormal = 1;

    private const uint WmInputLangChangeRequest = 0x0050;
    private const nuint InputLangChangeForward = 0x0002;
    private const uint LocaleIso639LanguageName = 0x00000059;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;

        // The union is as wide as its largest member, which is the mouse one. Without the
        // padding the structure is too small and SendInput rejects every call.
        [FieldOffset(0)] private readonly Padding _padding;

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private readonly struct Padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool join);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint code, uint type);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint thread);

    [DllImport("user32.dll")]
    private static extern uint GetKeyboardLayoutList(int count, [Out] nint[]? list);

    [DllImport("user32.dll")]
    private static extern nint PostMessageW(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern unsafe int GetLocaleInfoW(uint locale, uint type, char* data, int size);
}
