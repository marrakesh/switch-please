using System.Runtime.InteropServices;
using SwitchPlease.Core.Keys;

namespace SwitchPlease.Win32;

/// <summary>
/// Replays corrected text into whatever window has focus.
///
/// Text goes out as KEYEVENTF_UNICODE rather than as key codes, so the characters that
/// arrive do not depend on which layout happens to be active at that instant. Every event
/// carries <see cref="KeyboardHook.InjectionTag"/> so our own hook ignores the echo.
/// </summary>
public static class InputSender
{
    private static readonly int StructSize = Marshal.SizeOf<NativeMethods.INPUT>();

    /// <summary>Deletes <paramref name="count"/> characters to the left of the caret.</summary>
    public static void SendBackspaces(int count)
    {
        if (count <= 0)
        {
            return;
        }

        var inputs = new NativeMethods.INPUT[count * 2];

        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyInput(VirtualKeys.Back, isKeyUp: false);
            inputs[(i * 2) + 1] = KeyInput(VirtualKeys.Back, isKeyUp: true);
        }

        Send(inputs);
    }

    /// <summary>Types <paramref name="text"/> literally, surrogate pairs included.</summary>
    public static void SendText(string text) => SendText(text, millisecondsPerCharacter: 0);

    /// <summary>
    /// Types <paramref name="text"/>, optionally one character at a time.
    /// </summary>
    /// <param name="millisecondsPerCharacter">
    /// Pause between characters. Zero sends the whole string in one call, which is the only
    /// sensible default: every millisecond spent here is a millisecond in which the user can
    /// type over what is being written.
    /// </param>
    public static void SendText(string text, int millisecondsPerCharacter)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (millisecondsPerCharacter > 0)
        {
            SendSlowly(text, millisecondsPerCharacter);
            return;
        }

        var inputs = new NativeMethods.INPUT[text.Length * 2];

        for (int i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeInput(text[i], isKeyUp: false);
            inputs[(i * 2) + 1] = UnicodeInput(text[i], isKeyUp: true);
        }

        Send(inputs);
    }

    /// <summary>
    /// One character per call, with a pause between them, so the correction appears to be
    /// typed rather than to arrive all at once.
    ///
    /// Surrogate pairs are sent together: half a pair is not a character, and an application
    /// handed one on its own will render a replacement glyph that never gets fixed.
    /// </summary>
    private static void SendSlowly(string text, int millisecondsPerCharacter)
    {
        int pause = Math.Clamp(millisecondsPerCharacter, 1, 100);

        for (int i = 0; i < text.Length; i++)
        {
            int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
            var inputs = new NativeMethods.INPUT[length * 2];

            for (int c = 0; c < length; c++)
            {
                inputs[c * 2] = UnicodeInput(text[i + c], isKeyUp: false);
                inputs[(c * 2) + 1] = UnicodeInput(text[i + c], isKeyUp: true);
            }

            Send(inputs);

            i += length - 1;

            if (i < text.Length - 1)
            {
                Thread.Sleep(pause);
            }
        }
    }

    /// <summary>Sends a key with modifiers held down around it, e.g. Ctrl+Insert.</summary>
    public static void SendChord(ushort modifierVirtualKey, ushort virtualKey)
    {
        Send(
        [
            KeyInput(modifierVirtualKey, isKeyUp: false),
            KeyInput(virtualKey, isKeyUp: false),
            KeyInput(virtualKey, isKeyUp: true),
            KeyInput(modifierVirtualKey, isKeyUp: true),
        ]);
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, StructSize);

        if (sent != inputs.Length)
        {
            throw new InputBlockedException(sent, inputs.Length, Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>
    /// Keys that live on the grey block or the numeric keypad's edge carry the extended
    /// flag in their scan code. Insert without it is interpreted as the numpad's 0, which
    /// types a digit instead of copying.
    /// </summary>
    private static bool IsExtended(ushort virtualKey) => virtualKey
        is VirtualKeys.Insert or VirtualKeys.Delete or VirtualKeys.Home or VirtualKeys.End
        or VirtualKeys.Prior or VirtualKeys.Next
        or VirtualKeys.Left or VirtualKeys.Up or VirtualKeys.Right or VirtualKeys.Down
        or VirtualKeys.RControl or VirtualKeys.RMenu;

    private static NativeMethods.INPUT KeyInput(ushort virtualKey, bool isKeyUp)
    {
        uint flags = isKeyUp ? NativeMethods.KEYEVENTF_KEYUP : 0;

        if (IsExtended(virtualKey))
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        return new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            Union = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT
                {
                    VirtualKey = virtualKey,
                    Scan = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = KeyboardHook.InjectionTag,
                },
            },
        };
    }

    private static NativeMethods.INPUT UnicodeInput(char value, bool isKeyUp)
    {
        uint flags = NativeMethods.KEYEVENTF_UNICODE;

        if (isKeyUp)
        {
            flags |= NativeMethods.KEYEVENTF_KEYUP;
        }

        return new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            Union = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT
                {
                    VirtualKey = 0,
                    Scan = value,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = KeyboardHook.InjectionTag,
                },
            },
        };
    }
}

/// <summary>
/// Raised when Windows refused to deliver synthesised input.
///
/// Almost always means the focused window belongs to an elevated process: user interface
/// privilege isolation blocks SendInput from a lower integrity level, and there is nothing
/// the switcher can do about it except say so. Distinct from a general failure so the tray
/// can explain that particular cause once instead of logging it silently forever.
/// </summary>
public sealed class InputBlockedException(uint delivered, int expected, int lastError)
    : Exception($"SendInput delivered {delivered} of {expected} events (error {lastError}).")
{
    public uint Delivered { get; } = delivered;

    public int Expected { get; } = expected;

    public int LastError { get; } = lastError;

    /// <summary>True when nothing at all got through, the signature of a blocked window.</summary>
    public bool Blocked => Delivered == 0;
}
