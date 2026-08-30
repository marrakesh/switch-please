using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace SwitchPlease.App;

/// <summary>
/// Asks GitHub whether there is a newer release than the one running.
///
/// Never on by default, and never silent. A tray utility that contacts a server without
/// being asked is doing something the user did not install it for, so this runs either from
/// the menu entry or because the user switched the startup check on themselves.
///
/// Nothing is sent but the request itself: no identifier, no version, no telemetry. The
/// comparison happens here, from a version number that was already in the binary.
/// </summary>
public static class UpdateCheck
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/marrakesh/switch-please/releases/latest";

    /// <summary>Kept short: this runs at startup, and a slow answer is not worth waiting for.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>What a check found.</summary>
    /// <param name="Available">A newer release exists.</param>
    /// <param name="Latest">Version published, as GitHub reports it.</param>
    /// <param name="Current">Version running.</param>
    /// <param name="Error">Why the check failed, or null when it did not.</param>
    /// <param name="PageUrl">Where to download it from.</param>
    public readonly record struct Result(
        bool Available,
        string Latest,
        string Current,
        string? Error,
        string PageUrl);

    public static async Task<Result> RunAsync(CancellationToken cancellationToken = default)
    {
        string current = CurrentVersion();

        try
        {
            using var client = new HttpClient { Timeout = Timeout };

            // GitHub rejects requests without one.
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SwitchPlease", current));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            await using var stream = await client.GetStreamAsync(LatestReleaseUrl, cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            string tag = document.RootElement.TryGetProperty("tag_name", out var element)
                ? element.GetString() ?? string.Empty
                : string.Empty;

            string page = document.RootElement.TryGetProperty("html_url", out var url)
                ? url.GetString() ?? AboutForm.SourceUrl
                : AboutForm.SourceUrl;

            string latest = tag.TrimStart('v', 'V');

            return new Result(IsNewer(latest, current), latest, current, null, page);
        }
        catch (Exception ex)
        {
            // No release yet, no network, rate limited, GitHub down: all the same to a user
            // who only wanted to know whether to download something.
            return new Result(false, string.Empty, current, ex.Message, AboutForm.SourceUrl);
        }
    }

    /// <summary>
    /// Compares two dotted version numbers. Anything unparseable counts as "not newer",
    /// because a release named something this cannot read is not a reason to tell someone
    /// their copy is out of date.
    /// </summary>
    internal static bool IsNewer(string latest, string current) =>
        Version.TryParse(Normalise(latest), out var published)
        && Version.TryParse(Normalise(current), out var running)
        && published > running;

    /// <summary>Drops any pre-release suffix, which Version cannot parse.</summary>
    internal static string Normalise(string value)
    {
        int cut = value.IndexOfAny(['-', '+', ' ']);

        return cut < 0 ? value : value[..cut];
    }

    private static string CurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
