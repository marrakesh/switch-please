using SwitchPlease.Core.Keys;

namespace SwitchPlease.Win32;

/// <summary>Which configured hotkey a keystroke matched, if any.</summary>
public enum HotkeyAction
{
    None = 0,
    ConvertWord,
    ConvertSelection,

    /// <summary>Puts back whatever the last correction changed.</summary>
    Undo,
}

/// <summary>Every action a hotkey can be bound to, in binding order.</summary>
public static class HotkeyActions
{
    public static ReadOnlySpan<HotkeyAction> All =>
        [HotkeyAction.ConvertWord, HotkeyAction.ConvertSelection, HotkeyAction.Undo];

    public const int Count = 3;

    /// <summary>Position of an action in the binding arrays.</summary>
    public static int IndexOf(HotkeyAction action) => (int)action - 1;

    public static HotkeyAction At(int index) => (HotkeyAction)(index + 1);
}

/// <summary>
/// A keystroke as seen by the hook, handed to the worker thread for interpretation.
/// A struct so that queueing one allocates nothing.
/// </summary>
/// <param name="Timestamp">
/// <see cref="System.Diagnostics.Stopwatch"/> ticks taken inside the callback, which lets
/// the worker report how long the event waited before it was processed.
/// </param>
public readonly record struct RawKeyEvent(
    ushort VirtualKey,
    ushort ScanCode,
    ModifierKeys Modifiers,
    bool IsKeyDown,
    HotkeyAction Hotkey,
    long Timestamp);
