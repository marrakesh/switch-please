using SwitchPlease.Core.Keys;
using Xunit;

namespace SwitchPlease.Core.Tests;

public class TypingBufferTests
{
    [Fact]
    public void LastWordIgnoresTrailingWhitespace()
    {
        var buffer = Fill("hello world ");

        var range = buffer.GetLastWord();

        Assert.Equal("world", buffer.GetText(range));
    }

    [Fact]
    public void LastWordStopsAtTheePrecedingSpace()
    {
        var buffer = Fill("one two three");

        var range = buffer.GetLastWord();

        Assert.Equal("three", buffer.GetText(range));
    }

    [Fact]
    public void LastWordIsEmptyWhenOnlyWhitespaceWasTyped()
    {
        var buffer = Fill("   ");

        Assert.True(buffer.GetLastWord().IsEmpty);
    }

    [Fact]
    public void BackspaceRemovesTheLastStroke()
    {
        var buffer = Fill("word");

        buffer.Backspace();

        Assert.Equal("wor", buffer.GetText(buffer.GetAll()));
    }

    [Fact]
    public void BackspaceOnEmptyBufferIsHarmless()
    {
        var buffer = new TypingBuffer();

        buffer.Backspace();

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void ReplaceKeepsScanCodesAndSwapsCharacters()
    {
        var buffer = Fill("ab");
        var original = buffer.Strokes[0];

        buffer.Replace(0, original with { Character = 'ф' });

        Assert.Equal(original.ScanCode, buffer.Strokes[0].ScanCode);
        Assert.Equal('ф', buffer.Strokes[0].Character);
    }

    [Fact]
    public void CapacityIsBoundedAndKeepsTheMostRecentText()
    {
        var buffer = new TypingBuffer(16);

        foreach (char c in "abcdefghijklmnopqrstuvwxyz")
        {
            buffer.Append(new KeyStroke(0, 0, ModifierKeys.None, c));
        }

        Assert.True(buffer.Count <= 16);
        Assert.EndsWith("z", buffer.GetText(buffer.GetAll()), StringComparison.Ordinal);
    }

    private static TypingBuffer Fill(string text)
    {
        var buffer = new TypingBuffer();

        foreach (char c in text)
        {
            buffer.Append(new KeyStroke(0, 0, ModifierKeys.None, c));
        }

        return buffer;
    }
}
