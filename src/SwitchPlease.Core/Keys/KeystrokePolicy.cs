namespace SwitchPlease.Core.Keys;

/// <summary>What a keystroke does to the record of what the user has typed.</summary>
public enum KeystrokeEffect
{
    /// <summary>Changes nothing: a modifier on its own, or a key that types no character.</summary>
    Ignore,

    /// <summary>Adds the character it produced.</summary>
    Append,

    /// <summary>Removes the previous character.</summary>
    Backspace,

    /// <summary>
    /// Abandons the record: the caret has moved somewhere we cannot account for, or the
    /// keystroke was a command rather than typing.
    /// </summary>
    Reset,
}

/// <summary>
/// Decides how each keystroke affects the typing buffer.
///
/// This lives apart from the switcher because getting it wrong is both easy and invisible.
/// Treating a lone Ctrl press as the start of a shortcut, for instance, wipes the buffer
/// the moment the user reaches for a Ctrl-based hotkey, and the correction then silently
/// does nothing at all.
/// </summary>
public static class KeystrokePolicy
{
    public static KeystrokeEffect Classify(ushort virtualKey, ModifierKeys modifiers)
    {
        if (virtualKey == VirtualKeys.Back)
        {
            return KeystrokeEffect.Backspace;
        }

        // A modifier held down on its own types nothing and commands nothing. It must be
        // checked before the chord rule below, which would otherwise see the modifier the
        // key itself just set and mistake it for a shortcut in progress.
        if (VirtualKeys.IsModifier(virtualKey))
        {
            return KeystrokeEffect.Ignore;
        }

        // Enter and Tab move on to somewhere a correction could not be placed.
        if (virtualKey is VirtualKeys.Return or VirtualKeys.Tab)
        {
            return KeystrokeEffect.Reset;
        }

        if (VirtualKeys.IsBufferReset(virtualKey))
        {
            return KeystrokeEffect.Reset;
        }

        // Ctrl, Alt or Win together with a real key is a command. AltGr is the exception:
        // on layouts such as Polish or German it is how ordinary characters are reached.
        var command = modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Win);

        if (command != ModifierKeys.None && command != ModifierKeys.AltGr)
        {
            return KeystrokeEffect.Reset;
        }

        return KeystrokeEffect.Append;
    }
}
