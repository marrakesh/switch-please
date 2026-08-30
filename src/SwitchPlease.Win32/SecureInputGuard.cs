using System.Reflection;
using System.Runtime.InteropServices;

namespace SwitchPlease.Win32;

/// <summary>
/// Answers "is the caret sitting in a password field right now?".
///
/// A switcher that records keystrokes has to be able to answer this. Excluding whole
/// applications by name covers the dedicated password managers, but the ordinary case --
/// a login form in a browser, a credential prompt inside an editor -- happens in
/// applications the user obviously wants the switcher working in.
///
/// Two probes, cheapest first:
///
/// 1. <c>EM_GETPASSWORDCHAR</c>, which every native edit control answers. Costs one
///    cross-process message, so it is affordable on each keystroke.
///
/// 2. MSAA's <c>STATE_SYSTEM_PROTECTED</c>, which is how browsers and other applications
///    with their own controls report a masked field. It goes through COM and costs
///    considerably more, so it is only used before actually rewriting something.
///
/// Every failure path answers "not a password field". Anything else would make the
/// switcher stop working the moment a probe hit an application that answers neither
/// question, which is a far more common outcome than the one being guarded against.
/// </summary>
public static class SecureInputGuard
{
    private static readonly Guid AccessibleInterfaceId = new("618736e0-3c3d-11cf-810c-00aa00389b71");

    /// <summary>
    /// How long a verdict may be reused. Keyed on the focused window, which in a browser
    /// stays the same across every field on the page, so the handle alone is not enough to
    /// notice the caret moving from the login box to the one beside it.
    /// </summary>
    private const int CacheLifetimeMilliseconds = 200;

    /// <summary>One remembered answer, for one of the two probes.</summary>
    private struct Answer
    {
        public nint Control;
        public int Asked;
        public bool Verdict;
    }

    // One slot per probe: they ask different questions of the same control, so a cheap "no"
    // must not be able to stand in for a thorough one.
    [ThreadStatic]
    private static Answer _cheap;

    [ThreadStatic]
    private static Answer _thorough;

    /// <summary>
    /// The cheap probe, run on every keystroke. Recognises native edit controls only; a
    /// browser login form answers no here and is caught by
    /// <see cref="IsPasswordFieldFocused"/> before anything is rewritten.
    ///
    /// Cached like the thorough one, and for a sharper reason: it is a synchronous
    /// cross-process message with a twenty millisecond ceiling, and a busy application that
    /// takes all twenty of them turns a fast typist into a backlog on the worker thread.
    /// </summary>
    public static bool LooksLikePasswordField(nint foregroundWindow) =>
        Ask(ref _cheap, FocusedControl(foregroundWindow), static control => MasksItsContent(control));

    /// <summary>
    /// The thorough probe. Used before a correction is applied, where the extra cost is
    /// paid once rather than per keystroke.
    /// </summary>
    public static bool IsPasswordFieldFocused(nint foregroundWindow) =>
        Ask(
            ref _thorough,
            FocusedControl(foregroundWindow),
            static control => MasksItsContent(control) || IsProtectedAccessibleObject(control));

    /// <summary>
    /// Answers from the remembered verdict where one still applies.
    ///
    /// Keyed on the focused control rather than the window, so moving from an ordinary field
    /// to the password box beside it is a fresh question even though the window has not
    /// changed. The lifetime is there for the case the key cannot cover: a control that turns
    /// its own masking on and off.
    /// </summary>
    private static bool Ask(ref Answer answer, nint control, Func<nint, bool> probe)
    {
        if (control == 0)
        {
            return false;
        }

        int now = Environment.TickCount;

        if (answer.Control == control && (uint)(now - answer.Asked) < CacheLifetimeMilliseconds)
        {
            return answer.Verdict;
        }

        bool verdict = probe(control);

        answer.Control = control;
        answer.Asked = now;
        answer.Verdict = verdict;

        return verdict;
    }

    /// <summary>
    /// The control with the caret in it, which is not the foreground window itself: focus
    /// belongs to a thread, so it has to be asked for through the foreground window's
    /// thread rather than read globally.
    /// </summary>
    private static nint FocusedControl(nint foregroundWindow)
    {
        if (foregroundWindow == 0)
        {
            return 0;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);

        if (threadId == 0)
        {
            return 0;
        }

        var info = new NativeMethods.GUITHREADINFO
        {
            Size = Marshal.SizeOf<NativeMethods.GUITHREADINFO>(),
        };

        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info))
        {
            return 0;
        }

        return info.Focus != 0 ? info.Focus : foregroundWindow;
    }

    /// <summary>
    /// Whether the control replaces what it shows with a mask character. A timeout is
    /// essential: this is a synchronous cross-process message, and a hung application would
    /// otherwise hang the switcher's worker along with it.
    /// </summary>
    private static bool MasksItsContent(nint control)
    {
        nint answered = NativeMethods.SendMessageTimeoutW(
            control,
            NativeMethods.EM_GETPASSWORDCHAR,
            0,
            0,
            NativeMethods.SMTO_ABORTIFHUNG,
            20,
            out nuint result);

        return answered != 0 && result != 0;
    }

    /// <summary>
    /// Asks the accessibility layer instead, which is the only route into a control the
    /// application draws itself. Late-bound through IDispatch rather than a declared
    /// interface: only two properties are needed, and getting a hand-written vtable subtly
    /// wrong here would crash the process rather than return a wrong answer.
    /// </summary>
    private static bool IsProtectedAccessibleObject(nint control)
    {
        object? accessible = null;

        try
        {
            if (NativeMethods.AccessibleObjectFromWindow(
                    control, NativeMethods.OBJID_CLIENT, in AccessibleInterfaceId, out accessible) != 0
                || accessible is null)
            {
                return false;
            }

            // accFocus names the child with the caret, or nothing when the control itself
            // holds it. Its absence is normal, not a failure.
            object child = Invoke(accessible, "accFocus") ?? NativeMethods.CHILDID_SELF;

            if (Invoke(accessible, "accState", child) is not int state)
            {
                return false;
            }

            return (state & NativeMethods.STATE_SYSTEM_PROTECTED) != 0;
        }
        catch (Exception)
        {
            // Applications that expose no accessibility information, or expose a broken
            // implementation of it, are the normal case rather than an error.
            return false;
        }
        finally
        {
            if (accessible is not null && Marshal.IsComObject(accessible))
            {
                Marshal.ReleaseComObject(accessible);
            }
        }
    }

    private static object? Invoke(object target, string member, params object[] arguments) =>
        target.GetType().InvokeMember(
            member,
            BindingFlags.GetProperty,
            binder: null,
            target,
            arguments.Length == 0 ? null : arguments);
}
