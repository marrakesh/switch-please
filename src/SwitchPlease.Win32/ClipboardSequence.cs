namespace SwitchPlease.Win32;

/// <summary>
/// The clipboard's revision counter.
///
/// Windows bumps it on every change by anyone. Comparing it before and after asking the
/// focused window to copy is how the switcher can tell "the application has now answered"
/// from "the application put back exactly what was already there", which polling the
/// content cannot distinguish and which decides whether there is a selection at all.
/// </summary>
public static class ClipboardSequence
{
    public static uint Current => NativeMethods.GetClipboardSequenceNumber();
}
