namespace SwitchPlease.Core.Conversion;

/// <summary>
/// Links and email addresses inside text that is being converted: they are kept exactly as
/// typed, and left out when judging which layout the rest of the text reads best in.
///
/// "pfgjkyb ghjabkm yf https://freelancehunt.com/" is Russian typed on the US layout with a
/// link pasted or typed correctly after it. Converting the line used to turn the link into
/// "реезыЖ..акууд...", because every character was run through the layout table.
///
/// Only what is a link as it stands counts: a scheme followed by "://", a leading "www.", or
/// a complete address with an "@" and a domain. Typed in the wrong layout, none of them
/// survive -- "https://" arrives on the Russian keyboard as "реезыЖ..", and "@" as a quote --
/// so a link typed on the wrong keyboard is not one of these, and is converted like any other
/// word.
///
/// The whole whitespace-delimited token is kept, brackets and trailing punctuation included.
/// A comma after a link could equally be a "б" typed on the wrong keyboard; nothing tells the
/// two apart, and a link is too easy to break to guess.
/// </summary>
public static class Links
{
    /// <summary>
    /// Marks the characters of <paramref name="text"/> that belong to a link or an email
    /// address, or returns null when there are none.
    /// </summary>
    public static bool[]? Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        bool[]? marked = null;
        int start = 0;

        while (start < text.Length)
        {
            if (char.IsWhiteSpace(text[start]))
            {
                start++;
                continue;
            }

            int end = start;

            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            if (IsLink(text.AsSpan(start, end - start)))
            {
                marked ??= new bool[text.Length];
                marked.AsSpan(start, end - start).Fill(true);
            }

            start = end;
        }

        return marked;
    }

    /// <summary>
    /// <paramref name="text"/> with the characters <paramref name="links"/> marks taken out,
    /// leaving the whitespace around them, so "yf https://x.com/ gj" is judged as "yf  gj".
    /// </summary>
    public static string Remove(string text, bool[]? links)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (links is null)
        {
            return text;
        }

        return string.Concat(text.Where((_, i) => !links[i]));
    }

    /// <summary>
    /// <paramref name="converted"/> with the links put back as they read in
    /// <paramref name="typed"/>, the same text before conversion.
    /// </summary>
    public static string Restore(string converted, string typed, bool[]? links)
    {
        ArgumentNullException.ThrowIfNull(converted);
        ArgumentNullException.ThrowIfNull(typed);

        if (links is null)
        {
            return converted;
        }

        return string.Create(converted.Length, (converted, typed, links), static (span, state) =>
        {
            var (to, from, kept) = state;

            for (int i = 0; i < span.Length; i++)
            {
                span[i] = kept[i] ? from[i] : to[i];
            }
        });
    }

    private static bool IsLink(ReadOnlySpan<char> token)
    {
        token = token.TrimStart("(<[\"'«");

        // A scheme: "https://", "ftp://", "file://".
        int scheme = token.IndexOf("://", StringComparison.Ordinal);

        if (scheme > 0 && char.IsAsciiLetter(token[scheme - 1]))
        {
            return true;
        }

        if (token.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && token.Length > 4)
        {
            return true;
        }

        return IsEmailAddress(token.TrimEnd(")>]\"'».,;:!?"));
    }

    /// <summary>
    /// "name@example.com": one "@", a name before it, and a domain after it with at least one
    /// dot and a top-level part of two letters or more. Strict on purpose. Russian typed on
    /// the US layout turns every quotation mark into an "@", and "@ghbdtn@" must still be
    /// converted to "привет" in quotes.
    /// </summary>
    private static bool IsEmailAddress(ReadOnlySpan<char> token)
    {
        int at = token.IndexOf('@');

        if (at <= 0 || token[(at + 1)..].Contains('@'))
        {
            return false;
        }

        foreach (char c in token[..at])
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '%' or '+' or '-'))
            {
                return false;
            }
        }

        var domain = token[(at + 1)..];
        int lastDot = domain.LastIndexOf('.');

        if (lastDot <= 0 || domain.Length - lastDot - 1 < 2)
        {
            return false;
        }

        foreach (char c in domain[..lastDot])
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-'))
            {
                return false;
            }
        }

        foreach (char c in domain[(lastDot + 1)..])
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }

        return true;
    }
}
