namespace SwitchPlease.Core.Keys;

/// <summary>
/// Rolling record of what the user has typed since the last hard reset (focus change,
/// mouse click, Enter). Word boundaries are kept inline as whitespace strokes so that
/// "fix the last word" and "fix everything I just typed" both read off the same buffer.
/// </summary>
public sealed class TypingBuffer
{
    private readonly List<KeyStroke> _strokes;
    private readonly int _capacity;

    public TypingBuffer(int capacity = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 8);
        _capacity = capacity;
        _strokes = new List<KeyStroke>(capacity);
    }

    public int Count => _strokes.Count;

    public IReadOnlyList<KeyStroke> Strokes => _strokes;

    public void Append(in KeyStroke stroke)
    {
        if (_strokes.Count == _capacity)
        {
            // Drop the oldest quarter in one shot rather than shifting on every key.
            _strokes.RemoveRange(0, _capacity / 4);
        }

        _strokes.Add(stroke);
    }

    /// <summary>Undo the last stroke, mirroring a Backspace the user pressed.</summary>
    public void Backspace()
    {
        if (_strokes.Count > 0)
        {
            _strokes.RemoveAt(_strokes.Count - 1);
        }
    }

    public void Clear() => _strokes.Clear();

    /// <summary>
    /// The last whitespace-delimited token, ignoring trailing whitespace. Returns an
    /// empty range when there is nothing to convert.
    /// </summary>
    public StrokeRange GetLastWord()
    {
        int end = _strokes.Count;
        while (end > 0 && _strokes[end - 1].IsWhitespace)
        {
            end--;
        }

        if (end == 0)
        {
            return StrokeRange.Empty;
        }

        int start = end;
        while (start > 0 && !_strokes[start - 1].IsWhitespace)
        {
            start--;
        }

        return new StrokeRange(start, end - start);
    }

    /// <summary>Everything currently buffered, trailing whitespace included.</summary>
    public StrokeRange GetAll() =>
        _strokes.Count == 0 ? StrokeRange.Empty : new StrokeRange(0, _strokes.Count);

    public string GetText(in StrokeRange range)
    {
        if (range.Length == 0)
        {
            return string.Empty;
        }

        return string.Create(range.Length, (this, range), static (span, state) =>
        {
            var (buffer, r) = state;
            for (int i = 0; i < r.Length; i++)
            {
                span[i] = buffer._strokes[r.Start + i].Character;
            }
        });
    }

    /// <summary>
    /// Swaps one stroke in place, used after a conversion so the buffer matches what is now
    /// on screen while keeping the original scan codes.
    /// </summary>
    public void Replace(int index, in KeyStroke stroke)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _strokes.Count);

        _strokes[index] = stroke;
    }
}

/// <summary>A slice of <see cref="TypingBuffer"/>.</summary>
public readonly record struct StrokeRange(int Start, int Length)
{
    public static StrokeRange Empty => new(0, 0);

    public bool IsEmpty => Length == 0;

    public int End => Start + Length;
}
