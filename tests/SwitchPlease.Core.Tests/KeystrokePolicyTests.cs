using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

public class KeystrokePolicyTests
{
    private const ushort A = 0x41;

    [Theory]
    [InlineData(VirtualKeys.Control)]
    [InlineData(VirtualKeys.LControl)]
    [InlineData(VirtualKeys.RControl)]
    [InlineData(VirtualKeys.Shift)]
    [InlineData(VirtualKeys.LShift)]
    [InlineData(VirtualKeys.RShift)]
    [InlineData(VirtualKeys.Menu)]
    [InlineData(VirtualKeys.LMenu)]
    public void ALoneModifierLeavesTheBufferAlone(ushort modifier)
    {
        // The regression that made a Ctrl-based hotkey do nothing: pressing Ctrl sets the
        // Control flag, and the chord rule below then read its own modifier as a shortcut
        // and cleared everything the user had typed.
        var effect = KeystrokePolicy.Classify(modifier, ModifierKeys.Control);

        Assert.Equal(KeystrokeEffect.Ignore, effect);
    }

    [Fact]
    public void DoubleTappingCtrlPreservesWhatWasTyped()
    {
        // Both presses and both releases of the hotkey, as the hook reports them.
        foreach (ushort key in (ushort[])[VirtualKeys.LControl, VirtualKeys.LControl])
        {
            Assert.Equal(KeystrokeEffect.Ignore, KeystrokePolicy.Classify(key, ModifierKeys.Control));
            Assert.Equal(KeystrokeEffect.Ignore, KeystrokePolicy.Classify(key, ModifierKeys.None));
        }
    }

    [Theory]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Alt)]
    [InlineData(ModifierKeys.Win)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift)]
    public void ARealShortcutStillResets(ModifierKeys modifiers)
    {
        Assert.Equal(KeystrokeEffect.Reset, KeystrokePolicy.Classify(A, modifiers));
    }

    [Fact]
    public void AltGrIsTypingRatherThanACommand()
    {
        // On Polish or German layouts AltGr reaches ordinary characters.
        Assert.Equal(KeystrokeEffect.Append, KeystrokePolicy.Classify(A, ModifierKeys.AltGr));
    }

    [Theory]
    [InlineData(VirtualKeys.Return)]
    [InlineData(VirtualKeys.Tab)]
    [InlineData(VirtualKeys.Escape)]
    [InlineData(VirtualKeys.Left)]
    [InlineData(VirtualKeys.Right)]
    [InlineData(VirtualKeys.Home)]
    [InlineData(VirtualKeys.End)]
    [InlineData(VirtualKeys.Delete)]
    public void KeysThatMoveTheCaretDropTheBuffer(ushort virtualKey)
    {
        Assert.Equal(KeystrokeEffect.Reset, KeystrokePolicy.Classify(virtualKey, ModifierKeys.None));
    }

    [Fact]
    public void BackspaceRemovesOneCharacter()
    {
        Assert.Equal(KeystrokeEffect.Backspace, KeystrokePolicy.Classify(VirtualKeys.Back, ModifierKeys.None));
    }

    [Theory]
    [InlineData(A, ModifierKeys.None)]
    [InlineData(A, ModifierKeys.Shift)]
    [InlineData(VirtualKeys.Space, ModifierKeys.None)]
    [InlineData(0x30, ModifierKeys.None)]
    public void OrdinaryTypingIsRecorded(ushort virtualKey, ModifierKeys modifiers)
    {
        Assert.Equal(KeystrokeEffect.Append, KeystrokePolicy.Classify(virtualKey, modifiers));
    }

    [Fact]
    public void ShiftedTypingIsStillTyping()
    {
        // Capital letters must not be mistaken for a shortcut.
        Assert.Equal(KeystrokeEffect.Append, KeystrokePolicy.Classify(A, ModifierKeys.Shift | ModifierKeys.CapsLock));
    }
}
