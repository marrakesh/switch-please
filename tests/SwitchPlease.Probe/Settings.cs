using System.Text.Json;

namespace SwitchPlease.Probe;

/// <summary>How a command is bound, in the terms this program can press it with.</summary>
/// <param name="VirtualKey">The key itself, or the modifier for a double tap.</param>
/// <param name="Modifiers">Shift 1, Control 2, Alt 4, as the application stores them.</param>
/// <param name="DoubleTap">Tapped twice rather than held with a key.</param>
internal readonly record struct Binding(ushort VirtualKey, int Modifiers, bool DoubleTap);

/// <summary>
/// The few things the probe needs to read out of the application's settings file.
///
/// Read as raw JSON rather than by referencing the application, so this stays a program that
/// drives Switch Please from outside exactly as a person would, with no shared types to keep
/// the two accidentally in step.
/// </summary>
internal static class Settings
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SwitchPlease",
        "settings.json");

    /// <summary>
    /// How undo is bound, or null when it is not bound at all -- which is how it ships.
    /// </summary>
    public static Binding? Undo() => Read("UndoHotkey");

    private static Binding? Read(string name)
    {
        try
        {
            if (!File.Exists(Path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(Path));

            if (!document.RootElement.TryGetProperty(name, out var hotkey)
                || !hotkey.TryGetProperty("VirtualKey", out var key))
            {
                return null;
            }

            ushort virtualKey = (ushort)key.GetInt32();

            // Virtual key zero is how the application spells "not bound": no keyboard can
            // produce it, so a binding holding it never matches.
            if (virtualKey == 0)
            {
                return null;
            }

            int modifiers = hotkey.TryGetProperty("Modifiers", out var m) ? m.GetInt32() : 0;
            bool doubleTap = hotkey.TryGetProperty("Kind", out var kind) && kind.GetInt32() == 1;

            return new Binding(virtualKey, modifiers, doubleTap);
        }
        catch (Exception)
        {
            // A settings file that cannot be read is not this program's problem to report.
            return null;
        }
    }
}
