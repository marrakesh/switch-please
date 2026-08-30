namespace SwitchPlease.Core.Layouts;

/// <summary>
/// Character-for-character translation between two layouts, derived from the keys that
/// produce each character rather than from a hard-coded table. Characters with no
/// counterpart pass through unchanged, which is what keeps digits and shared punctuation
/// intact when a word is converted.
/// </summary>
public sealed class LayoutMap
{
    private readonly Dictionary<char, char> _map;

    private LayoutMap(LayoutInfo source, LayoutInfo target, Dictionary<char, char> map)
    {
        Source = source;
        Target = target;
        _map = map;
    }

    public LayoutInfo Source { get; }

    public LayoutInfo Target { get; }

    public int MappedCharacters => _map.Count;

    /// <summary>
    /// Translates <paramref name="text"/> into the target layout. Returns the input
    /// unchanged when nothing in it maps, so callers can cheaply detect a no-op.
    /// </summary>
    public string Convert(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var map = _map;
        bool changed = false;

        foreach (char c in text)
        {
            if (map.ContainsKey(c))
            {
                changed = true;
                break;
            }
        }

        if (!changed)
        {
            return text;
        }

        return string.Create(text.Length, (text, map), static (span, state) =>
        {
            var (input, table) = state;
            for (int i = 0; i < input.Length; i++)
            {
                span[i] = table.TryGetValue(input[i], out char mapped) ? mapped : input[i];
            }
        });
    }

    /// <summary>Fraction of characters in <paramref name="text"/> that have a counterpart.</summary>
    public double CoverageOf(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int hits = 0;
        foreach (char c in text)
        {
            if (_map.ContainsKey(c))
            {
                hits++;
            }
        }

        return (double)hits / text.Length;
    }

    public sealed class Builder(LayoutInfo source, LayoutInfo target)
    {
        private readonly Dictionary<char, char> _map = new();

        /// <summary>
        /// Records that <paramref name="from"/> and <paramref name="to"/> sit on the same
        /// physical key. The first mapping for a character wins: layouts occasionally put
        /// the same character on two keys and the earlier scan code is the canonical one.
        /// </summary>
        public Builder Add(char from, char to)
        {
            if (from == '\0' || to == '\0' || from == to)
            {
                return this;
            }

            _map.TryAdd(from, to);
            return this;
        }

        public LayoutMap Build() => new(source, target, _map);
    }
}
