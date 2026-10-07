namespace SwitchPlease.Core.Indicator;

/// <summary>Which way round the layout indicator is drawn.</summary>
public enum BadgeShade
{
    /// <summary>A dark tile with light lettering, for a light background.</summary>
    Dark,

    /// <summary>A light tile with dark lettering, for a dark background.</summary>
    Light,
}

/// <summary>
/// Picks the indicator's shade from what is on screen where it is about to go.
///
/// Neutral rather than a brand colour, and the opposite of its background: a grey tag is the
/// least it can be and still be read, and judging the background itself rather than the
/// Windows theme is what makes it right in a dark editor on a light desktop, or a white page
/// in a dark browser.
/// </summary>
public static class BadgeShades
{
    /// <param name="pixels">The background, as 0xAARRGGBB values.</param>
    public static BadgeShade For(ReadOnlySpan<uint> pixels)
    {
        if (pixels.IsEmpty)
        {
            return BadgeShade.Dark;
        }

        double total = 0;

        foreach (uint pixel in pixels)
        {
            total += Luminance(pixel);
        }

        return total / pixels.Length >= 0.5 ? BadgeShade.Dark : BadgeShade.Light;
    }

    /// <summary>Perceived brightness, 0 to 1, by the Rec. 709 weights.</summary>
    private static double Luminance(uint pixel)
    {
        double red = (pixel >> 16) & 0xFF;
        double green = (pixel >> 8) & 0xFF;
        double blue = pixel & 0xFF;

        return ((0.2126 * red) + (0.7152 * green) + (0.0722 * blue)) / 255;
    }
}
