namespace SwitchPlease.Core.Keys;

/// <summary>Win32 virtual-key codes the switcher cares about.</summary>
public static class VirtualKeys
{
    public const ushort Back = 0x08;
    public const ushort Tab = 0x09;
    public const ushort Return = 0x0D;
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Menu = 0x12;
    public const ushort Pause = 0x13;
    public const ushort Capital = 0x14;
    public const ushort Escape = 0x1B;
    public const ushort Space = 0x20;
    public const ushort Prior = 0x21;
    public const ushort Next = 0x22;
    public const ushort End = 0x23;
    public const ushort Home = 0x24;
    public const ushort Left = 0x25;
    public const ushort Up = 0x26;
    public const ushort Right = 0x27;
    public const ushort Down = 0x28;
    public const ushort Insert = 0x2D;
    public const ushort Delete = 0x2E;
    public const ushort LWin = 0x5B;
    public const ushort RWin = 0x5C;
    public const ushort ScrollLock = 0x91;
    public const ushort LShift = 0xA0;
    public const ushort RShift = 0xA1;
    public const ushort LControl = 0xA2;
    public const ushort RControl = 0xA3;
    public const ushort LMenu = 0xA4;
    public const ushort RMenu = 0xA5;

    /// <summary>Keys that end the current word without deleting anything.</summary>
    public static bool IsWordBreak(ushort virtualKey) => virtualKey is Space or Return or Tab;

    /// <summary>
    /// Keys that move the caret somewhere we can no longer reason about, so the recorded
    /// buffer stops matching the screen and must be dropped.
    /// </summary>
    public static bool IsBufferReset(ushort virtualKey) => virtualKey
        is Escape or Prior or Next or End or Home
        or Left or Up or Right or Down
        or Insert or Delete;

    /// <summary>
    /// Collapses the left/right variants of a modifier onto the neutral code, so that
    /// tapping either Shift key counts as the same key.
    /// </summary>
    public static ushort NormalizeModifier(ushort virtualKey) => virtualKey switch
    {
        LShift or RShift => Shift,
        LControl or RControl => Control,
        LMenu or RMenu => Menu,
        _ => virtualKey,
    };

    public static bool IsModifier(ushort virtualKey) =>
        NormalizeModifier(virtualKey) is Shift or Control or Menu or LWin or RWin;

    public static string NameOf(ushort virtualKey) => virtualKey switch
    {
        Shift => "Shift",
        Control => "Ctrl",
        Menu => "Alt",
        LWin or RWin => "Win",
        Back => "Backspace",
        Tab => "Tab",
        Return => "Enter",
        Pause => "Pause/Break",
        Capital => "CapsLock",
        Escape => "Esc",
        Space => "Space",
        ScrollLock => "ScrollLock",
        Insert => "Insert",
        Delete => "Delete",
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        _ => $"0x{virtualKey:X2}",
    };
}
