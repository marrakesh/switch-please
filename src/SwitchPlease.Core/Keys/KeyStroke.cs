namespace SwitchPlease.Core.Keys;

/// <summary>
/// One recorded key press. Keeps the scan code as well as the resolved character:
/// the scan code is what lets us ask "what would this key have produced in the other
/// layout?" without relying on a hard-coded transliteration table.
/// </summary>
public readonly record struct KeyStroke(
    ushort ScanCode,
    ushort VirtualKey,
    ModifierKeys Modifiers,
    char Character)
{
    public bool IsWhitespace => Character is ' ' or '\t' or '\r' or '\n';
}
