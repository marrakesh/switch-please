using System.Text;
using SwitchPlease.Core.Conversion;
using SwitchPlease.Core.Keys;
using SwitchPlease.Core.Detection;
using SwitchPlease.Core.Layouts;

namespace SwitchPlease.Win32;

/// <summary>
/// Answers layout questions by asking Windows, not by consulting a built-in table.
///
/// Every character mapping is derived by taking a scan code and asking ToUnicodeEx what
/// each installed layout would produce for it. That is why this works for any pair of
/// layouts the user has installed -- Russian, Ukrainian, German, Greek -- without anyone
/// hand-writing a transliteration table, and why it stays correct if the user is on a
/// Dvorak or typewriter variant.
/// </summary>
public sealed class KeyboardLayoutService : ILayoutResolver
{
    /// <summary>
    /// Scan codes of the printable block of a standard keyboard, set 1. Deliberately
    /// excludes the numpad, whose characters do not differ between layouts.
    /// </summary>
    private static readonly ushort[] PrintableScanCodes = BuildPrintableScanCodes();

    private readonly Lock _gate = new();
    private readonly Dictionary<(nint Source, nint Target), LayoutMap> _maps = [];

    private IReadOnlyList<LayoutInfo> _layouts = [];

    // The list exactly as Windows reported it, before deduplication. Comparing against the
    // deduplicated set would report a change on every poll, because collapsing duplicate
    // text services is precisely what removes handles from it.
    private nint[] _rawHandles = [];

    public KeyboardLayoutService() => Refresh();

    public IReadOnlyList<LayoutInfo> InstalledLayouts => _layouts;

    /// <summary>Re-reads the installed layouts and drops cached maps. Call on WM_INPUTLANGCHANGE.</summary>
    public void Refresh()
    {
        nint[] handles = ReadLayoutHandles();
        var layouts = DescribeAll(handles);

        lock (_gate)
        {
            _rawHandles = handles;
            _layouts = layouts;
            _maps.Clear();
        }
    }

    public LayoutInfo? GetActiveLayout()
    {
        nint window = NativeMethods.GetForegroundWindow();

        if (window == 0)
        {
            return null;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(window, out _);
        nint hkl = NativeMethods.GetKeyboardLayout(threadId);

        return FindOrDescribe(hkl);
    }

    /// <summary>
    /// Switches the focused window to <paramref name="layout"/>, so that typing carries on
    /// in the language the user evidently meant.
    ///
    /// Two mechanisms, because neither works everywhere. WM_INPUTLANGCHANGEREQUEST is the
    /// documented one, but it is only a request: an application has to handle it, and
    /// Electron, UWP and a good part of what people type into simply drop it, leaving the
    /// text corrected and the keyboard still wrong. ActivateKeyboardLayout changes the
    /// calling thread's layout, so attaching to the target thread first makes it change
    /// theirs -- which works regardless of what messages they handle, but needs input
    /// attachment that Windows refuses across integrity levels.
    ///
    /// So: try attachment first, check whether it took, and fall back to the message.
    /// </summary>
    /// <returns>False when neither route could be confirmed to have worked.</returns>
    public static bool SetActiveLayout(LayoutInfo layout)
    {
        nint window = NativeMethods.GetForegroundWindow();

        if (window == 0)
        {
            return false;
        }

        uint targetThread = NativeMethods.GetWindowThreadProcessId(window, out _);
        uint thisThread = NativeMethods.GetCurrentThreadId();

        if (targetThread != 0 && targetThread != thisThread && Activate(targetThread, thisThread, layout.Handle))
        {
            return true;
        }

        NativeMethods.PostMessageW(
            window,
            NativeMethods.WM_INPUTLANGCHANGEREQUEST,
            NativeMethods.INPUTLANGCHANGE_FORWARD,
            layout.Handle);

        // Posted, so it has not happened yet, and there is nothing useful to report from
        // here. Whether it took is answered by HasLayout, which the caller asks once it is
        // no longer holding anything.
        return false;
    }

    /// <summary>
    /// Whether the focused window is on <paramref name="layout"/> now.
    ///
    /// Split out from <see cref="SetActiveLayout"/> so the answer can be looked for after the
    /// caller has released its locks. This used to poll for it in place, which meant sleeping
    /// for up to a sixth of a second while the typing buffer was held -- on the machine this
    /// was written for the message route is the one that always works, so that sleep was the
    /// normal path rather than the exceptional one.
    /// </summary>
    public static bool HasLayout(LayoutInfo layout)
    {
        nint window = NativeMethods.GetForegroundWindow();

        if (window == 0)
        {
            return false;
        }

        uint thread = NativeMethods.GetWindowThreadProcessId(window, out _);

        return thread != 0 && NativeMethods.GetKeyboardLayout(thread) == layout.Handle;
    }

    private static bool Activate(uint targetThread, uint thisThread, nint layoutHandle)
    {
        if (!NativeMethods.AttachThreadInput(thisThread, targetThread, true))
        {
            return false;
        }

        try
        {
            NativeMethods.ActivateKeyboardLayout(layoutHandle, 0);

            return NativeMethods.GetKeyboardLayout(targetThread) == layoutHandle;
        }
        finally
        {
            NativeMethods.AttachThreadInput(thisThread, targetThread, false);
        }
    }

    /// <summary>
    /// Whether Windows now reports a different set of installed layouts than the one this
    /// service was built from.
    ///
    /// Polled rather than pushed on purpose. A layout being added or removed produces no
    /// broadcast a background process can rely on -- WM_INPUTLANGCHANGE only reaches the
    /// thread whose own layout changed -- and this costs one cheap call against a list that
    /// is almost always two entries long.
    /// </summary>
    public bool InstalledLayoutsChanged()
    {
        nint[] current = ReadLayoutHandles();
        nint[] known;

        lock (_gate)
        {
            known = _rawHandles;
        }

        if (current.Length != known.Length)
        {
            return true;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != known[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every character the layout can type, both shifted and not. This is where the alphabet
    /// of a language comes from when the application carries no model of it: the layout is
    /// the definitive statement of which letters that language uses.
    /// </summary>
    public static string TypableCharacters(nint layoutHandle)
    {
        var characters = new StringBuilder(PrintableScanCodes.Length * 2);

        foreach (ushort scanCode in PrintableScanCodes)
        {
            foreach (var modifiers in (ReadOnlySpan<ModifierKeys>)[ModifierKeys.None, ModifierKeys.Shift])
            {
                char c = ResolveCharacter(scanCode, modifiers, layoutHandle);

                if (c != ' ')
                {
                    characters.Append(c);
                }
            }
        }

        return characters.ToString();
    }

    /// <summary>
    /// Describes the installed layouts as languages, so the detection side can be built from
    /// what the user has installed rather than from a list fixed in the source.
    /// </summary>
    public IReadOnlyList<LayoutLanguage> DescribeLanguages()
    {
        var result = new List<LayoutLanguage>();

        foreach (var layout in _layouts)
        {
            string tag = QueryLocale((uint)layout.LanguageId, NativeMethods.LOCALE_SNAME);

            if (tag.Length == 0)
            {
                tag = layout.CultureName;
            }

            result.Add(new LayoutLanguage(tag, layout.CultureName, TypableCharacters(layout.Handle)));
        }

        return result;
    }

    public LayoutMap GetMap(LayoutInfo source, LayoutInfo target)
    {
        var key = (source.Handle, target.Handle);

        lock (_gate)
        {
            if (_maps.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var built = BuildMap(source, target);

        lock (_gate)
        {
            _maps[key] = built;
        }

        return built;
    }

    /// <summary>
    /// Whether an input method editor is assembling a character in this window right now, as
    /// happens throughout Chinese, Japanese and Korean input.
    ///
    /// Asking about the composition rather than about the layout is deliberate. ImmIsIME
    /// looked like the obvious test but reports whether the input locale *supports* an
    /// editor, and answers yes even for a plain US layout, which would have silenced the
    /// switcher entirely. What actually matters is the state: while a composition is in
    /// progress the keys pressed are not the text produced, and interfering would corrupt it.
    /// </summary>
    public static bool IsComposing(nint window)
    {
        if (window == 0)
        {
            return false;
        }

        nint context = NativeMethods.ImmGetContext(window);

        if (context == 0)
        {
            return false;
        }

        try
        {
            return NativeMethods.ImmGetCompositionStringW(context, NativeMethods.GCS_COMPSTR, 0, 0) > 0;
        }
        finally
        {
            NativeMethods.ImmReleaseContext(window, context);
        }
    }

    public char ResolveCharacter(in KeyStroke stroke, LayoutInfo layout) =>
        ResolveCharacter(stroke.ScanCode, stroke.Modifiers, layout.Handle);

    /// <summary>
    /// The scan code for a virtual key. Needed because not every key event carries one:
    /// the on-screen keyboard, remote desktop clients and automation tools all inject
    /// keystrokes with a scan code of zero, and the whole conversion path is built on scan
    /// codes rather than virtual keys.
    /// </summary>
    public static ushort ScanCodeFor(ushort virtualKey, nint layoutHandle) =>
        (ushort)NativeMethods.MapVirtualKeyExW(virtualKey, NativeMethods.MAPVK_VK_TO_VSC, layoutHandle);

    /// <summary>
    /// What a physical key produces under a given layout. The virtual key is re-derived
    /// from the scan code because the one recorded at typing time belongs to whichever
    /// layout was active then, and would be wrong for any other layout.
    /// </summary>
    public static unsafe char ResolveCharacter(ushort scanCode, ModifierKeys modifiers, nint layoutHandle)
    {
        uint virtualKey = NativeMethods.MapVirtualKeyExW(scanCode, NativeMethods.MAPVK_VSC_TO_VK_EX, layoutHandle);

        if (virtualKey == 0)
        {
            return '\0';
        }

        byte* state = stackalloc byte[256];
        new Span<byte>(state, 256).Clear();

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            state[VirtualKeys.Shift] = 0x80;
            state[VirtualKeys.LShift] = 0x80;
        }

        if ((modifiers & ModifierKeys.CapsLock) != 0)
        {
            state[VirtualKeys.Capital] = 0x01;
        }

        if ((modifiers & ModifierKeys.AltGr) == ModifierKeys.AltGr)
        {
            state[VirtualKeys.Control] = 0x80;
            state[VirtualKeys.LControl] = 0x80;
            state[VirtualKeys.Menu] = 0x80;
            state[VirtualKeys.RMenu] = 0x80;
        }

        // The slack past ReportedLength is what keeps a ToUnicodeEx overrun from becoming a
        // crash. See the note on the P/Invoke declaration.
        const int ReportedLength = 16;
        const int AllocatedLength = 64;

        char* buffer = stackalloc char[AllocatedLength];
        new Span<char>(buffer, AllocatedLength).Clear();

        int count = NativeMethods.ToUnicodeEx(
            virtualKey, scanCode, state, buffer, ReportedLength, 0, layoutHandle);

        if (count < 0)
        {
            // A dead key. It left state behind that would corrupt the next lookup, so press
            // it a second time to consume it, and treat it as unmapped. Whatever the second
            // press produces is the dead key's own character, which is of no use here.
            _ = NativeMethods.ToUnicodeEx(virtualKey, scanCode, state, buffer, ReportedLength, 0, layoutHandle);
            return '\0';
        }

        return count == 1 ? buffer[0] : '\0';
    }

    private static LayoutMap BuildMap(LayoutInfo source, LayoutInfo target)
    {
        var builder = new LayoutMap.Builder(source, target);

        foreach (ushort scanCode in PrintableScanCodes)
        {
            foreach (var modifiers in (ReadOnlySpan<ModifierKeys>)[ModifierKeys.None, ModifierKeys.Shift])
            {
                char from = ResolveCharacter(scanCode, modifiers, source.Handle);
                char to = ResolveCharacter(scanCode, modifiers, target.Handle);

                builder.Add(from, to);
            }
        }

        return builder.Build();
    }

    private LayoutInfo? FindOrDescribe(nint hkl)
    {
        foreach (var layout in _layouts)
        {
            if (layout.Handle == hkl)
            {
                return layout;
            }
        }

        return hkl == 0 ? null : Describe(hkl);
    }

    private static nint[] ReadLayoutHandles()
    {
        uint count = NativeMethods.GetKeyboardLayoutList(0, null);

        if (count == 0)
        {
            return [];
        }

        var handles = new nint[count];
        uint copied = NativeMethods.GetKeyboardLayoutList((int)count, handles);

        // A layout removed between the two calls leaves the tail of the array as zeros, and
        // a zero handle describes nothing. Trust the second count, not the first.
        return copied >= count ? handles : handles[..(int)copied];
    }

    private static List<LayoutInfo> DescribeAll(nint[] handles)
    {
        var described = new List<LayoutInfo>(handles.Length);

        foreach (nint handle in handles)
        {
            described.Add(Describe(handle));
        }

        return LayoutCatalog.Deduplicate(described, PrintableScanCodes);
    }

    private static LayoutInfo Describe(nint handle)
    {
        // The low word of an HKL is the input language identifier, which is what Windows
        // shows in its own language bar. Some text-service handles put a neutral value
        // there (primary language zero); for those the high word carries the real language.
        int languageId = (int)((ulong)handle & 0xFFFF);

        if ((languageId & 0x3FF) == 0)
        {
            languageId = (int)(((ulong)handle >> 16) & 0xFFFF);
        }

        string culture = QueryLocale((uint)languageId, NativeMethods.LOCALE_SISO639LANGNAME);
        string display = QueryLocale((uint)languageId, NativeMethods.LOCALE_SLOCALIZEDDISPLAYNAME);

        if (culture.Length == 0)
        {
            culture = $"{languageId:X4}";
        }

        if (display.Length == 0)
        {
            display = culture;
        }

        return new LayoutInfo(handle, languageId, culture, display);
    }

    private static unsafe string QueryLocale(uint localeId, uint infoType)
    {
        const int Capacity = 128;

        char* buffer = stackalloc char[Capacity];
        int written = NativeMethods.GetLocaleInfoW(localeId, infoType, buffer, Capacity);

        // The count includes the terminating null, which is not part of the name.
        return written > 1 ? new string(buffer, 0, written - 1) : string.Empty;
    }

    private static ushort[] BuildPrintableScanCodes()
    {
        var codes = new List<ushort>(64);

        void AddRange(ushort first, ushort last)
        {
            for (ushort code = first; code <= last; code++)
            {
                codes.Add(code);
            }
        }

        AddRange(0x02, 0x0D); // digit row, through - and =
        AddRange(0x10, 0x1B); // Q row, through [ and ]
        AddRange(0x1E, 0x28); // A row, through ; and '
        codes.Add(0x29);      // the key left of 1
        codes.Add(0x2B);      // backslash
        AddRange(0x2C, 0x35); // Z row, through . and /

        return [.. codes];
    }
}
