using System.Runtime.InteropServices;

namespace SwitchPlease.Win32;

internal static class NativeMethods
{
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WH_MOUSE_LL = 14;

    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_MBUTTONDOWN = 0x0207;
    internal const int HC_ACTION = 0;

    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_QUIT = 0x0012;
    internal const int WM_INPUTLANGCHANGEREQUEST = 0x0050;

    /// <summary>Asks an edit control which character it masks its content with. Zero means it does not.</summary>
    internal const uint EM_GETPASSWORDCHAR = 0x00D2;

    internal const uint SMTO_ABORTIFHUNG = 0x0002;

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;

    internal const uint MAPVK_VK_TO_VSC = 0;
    internal const uint MAPVK_VSC_TO_VK_EX = 3;

    /// <summary>MSAA: the focused object masks what it displays, i.e. a password field.</summary>
    internal const int STATE_SYSTEM_PROTECTED = 0x20000000;

    internal const uint OBJID_CLIENT = 0xFFFFFFFC;

    internal const int CHILDID_SELF = 0;

    internal const uint INPUTLANGCHANGE_FORWARD = 0x0002;

    internal delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    internal delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        internal uint VirtualKeyCode;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        internal uint Size;
        internal uint Time;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        internal int Size;
        internal RECT Monitor;
        internal RECT Work;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GUITHREADINFO
    {
        internal int Size;
        internal uint Flags;
        internal nint Active;
        internal nint Focus;
        internal nint Capture;
        internal nint MenuOwner;
        internal nint MoveSize;
        internal nint Caret;
        internal RECT CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        internal nint Hwnd;
        internal uint Message;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal int PointX;
        internal int PointY;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int Dx;
        internal int Dy;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort VirtualKey;
        internal ushort Scan;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        internal uint Msg;
        internal ushort ParamL;
        internal ushort ParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal MOUSEINPUT Mouse;
        [FieldOffset(0)] internal KEYBDINPUT Keyboard;
        [FieldOffset(0)] internal HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint Type;
        internal InputUnion Union;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowsHookExW")]
    internal static extern nint SetWindowsMouseHookExW(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    internal static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    internal static extern nint DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessageW(uint idThread, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint cInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    internal static extern nint GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    internal static extern uint GetKeyboardLayoutList(int nBuff, [Out] nint[]? lpList);

    [DllImport("user32.dll")]
    internal static extern nint PostMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKeyExW(uint uCode, uint uMapType, nint dwhkl);

    /// <summary>
    /// Takes raw pointers rather than arrays on purpose. ToUnicodeEx is documented to be
    /// able to write more characters than cchBuff for keys that produce ligatures, and some
    /// installed layouts do have them. Handing it a pinned managed array means such an
    /// overrun corrupts the GC heap and kills the process; a stack buffer with slack
    /// absorbs it instead.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern unsafe int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte* lpKeyState,
        char* pwszBuff,
        int cchBuff,
        uint wFlags,
        nint dwhkl);

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int nVirtKey);

    /// <summary>
    /// The physical state of a key, as the input system sees it rather than as the calling
    /// thread's message queue has caught up with it. That distinction is the whole point of
    /// using it here: the hook thread never reads key messages, so <c>GetKeyState</c> is not
    /// a safe way to ask whether a modifier is held right now, while the high bit of this is.
    ///
    /// Its low bit is documented as meaningless, so the toggle state of Caps Lock still has
    /// to come from <c>GetKeyState</c>, which reports that one correctly from any thread.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int nVirtKey);

    internal const uint DESKTOP_READOBJECTS = 0x0001;

    /// <summary>
    /// The desktop the user is typing into, which is not always the one this process is on.
    /// A UAC prompt, the lock screen and Ctrl+Alt+Del all move the input to a desktop we
    /// cannot open, and the failure to open it is the answer.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint OpenInputDesktop(uint dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseDesktop(nint hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    internal static extern nint ActivateKeyboardLayout(nint hkl, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint SendMessageTimeoutW(
        nint hWnd,
        uint msg,
        nuint wParam,
        nint lParam,
        uint flags,
        uint timeoutMilliseconds,
        out nuint result);

    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();

    /// <summary>
    /// When Windows last saw input from any source. The one thing that can tell an idle
    /// machine apart from a hook that is no longer being called.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    /// <summary>
    /// With a null window this posts WM_TIMER to the calling thread's own queue, which is
    /// how a thread that has a message loop but no window gets a periodic callback.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern nuint SetTimer(nint window, nuint id, uint intervalMilliseconds, nint callback);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool KillTimer(nint window, nuint id);

    internal const uint WM_TIMER = 0x0113;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    internal static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    internal static extern nint GetDesktopWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern unsafe int GetClassNameW(nint hWnd, char* lpClassName, int nMaxCount);

    /// <summary>
    /// The documented way to ask "is something running that should not be interrupted".
    /// Covers exclusive-fullscreen Direct3D and presentation mode, which no amount of
    /// measuring window rectangles will reveal.
    /// </summary>
    [DllImport("shell32.dll")]
    internal static extern int SHQueryUserNotificationState(out int state);

    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    internal const int QUNS_BUSY = 2;
    internal const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    internal const int QUNS_PRESENTATION_MODE = 4;

    [DllImport("oleacc.dll")]
    internal static extern int AccessibleObjectFromWindow(
        nint hwnd,
        uint dwObjectID,
        in Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);

    /// <summary>Composition string currently being assembled by an input method editor.</summary>
    internal const int GCS_COMPSTR = 0x0008;

    [DllImport("imm32.dll")]
    internal static extern nint ImmGetContext(nint hWnd);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ImmGetCompositionStringW(nint hIMC, int dwIndex, nint lpBuf, int dwBufLen);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImmReleaseContext(nint hWnd, nint hIMC);

    /// <summary>
    /// Takes a raw buffer rather than a StringBuilder: marshalling one allocates twice per
    /// call and copies the characters back out again, and this is called for every layout
    /// every time the list is rebuilt.
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern unsafe int GetLocaleInfoW(uint locale, uint lcType, char* lpLCData, int cchData);

    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    /// <summary>Likewise: this one runs whenever the focused window changes.</summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern unsafe bool QueryFullProcessImageNameW(nint hProcess, uint dwFlags, char* lpExeName, ref uint lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint hObject);

    internal const uint LOCALE_SLOCALIZEDDISPLAYNAME = 0x00000002;
    internal const uint LOCALE_SISO639LANGNAME = 0x00000059;

    /// <summary>The BCP-47 name, e.g. "ru-RU", which is what the spell-checker expects.</summary>
    internal const uint LOCALE_SNAME = 0x0000005C;
}
