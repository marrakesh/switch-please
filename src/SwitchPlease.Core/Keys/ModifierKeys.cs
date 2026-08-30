namespace SwitchPlease.Core.Keys;

/// <summary>Modifier state captured at the moment a key was pressed.</summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Win = 1 << 3,
    CapsLock = 1 << 4,

    /// <summary>Ctrl+Alt, which layouts such as Polish or German use as AltGr.</summary>
    AltGr = Control | Alt,
}
