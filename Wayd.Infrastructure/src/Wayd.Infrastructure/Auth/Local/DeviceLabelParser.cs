namespace Wayd.Infrastructure.Auth.Local;

/// <summary>
/// Turns a User-Agent header into a coarse "Browser on OS" label for the sessions list.
/// </summary>
/// <remarks>
/// Deliberately approximate and dependency-free. This label only helps a person recognise
/// their own devices; nothing authorizes on it, so a wrong guess costs a confusing row, not
/// access. User-Agent strings are client-controlled and widely spoofed — never treat this as
/// a fact about the client.
/// </remarks>
internal static class DeviceLabelParser
{
    private const int MaxLabelLength = 200;

    // Order matters: every Chromium browser also says "Chrome", and Chrome/Edge also say
    // "Safari", so the more specific token has to win.
    private static readonly (string Token, string Name)[] Browsers =
    [
        ("Edg/", "Edge"),
        ("OPR/", "Opera"),
        ("Firefox/", "Firefox"),
        ("Chrome/", "Chrome"),
        ("Safari/", "Safari"),
    ];

    // iPhone/iPad before Mac OS X: iOS user agents contain both.
    private static readonly (string Token, string Name)[] Platforms =
    [
        ("iPhone", "iPhone"),
        ("iPad", "iPad"),
        ("Android", "Android"),
        ("Windows", "Windows"),
        ("Mac OS X", "macOS"),
        ("CrOS", "ChromeOS"),
        ("Linux", "Linux"),
    ];

    public static string? Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var browser = Match(userAgent, Browsers);
        var platform = Match(userAgent, Platforms);

        var label = (browser, platform) switch
        {
            (not null, not null) => $"{browser} on {platform}",
            (not null, null) => browser,
            (null, not null) => platform,
            // Unrecognised, but a caller (CLI, script, integration) still deserves a row it
            // can identify, so fall back to the raw header rather than dropping it.
            _ => userAgent.Trim(),
        };

        return label.Length <= MaxLabelLength ? label : label[..MaxLabelLength];
    }

    private static string? Match(string userAgent, (string Token, string Name)[] candidates)
    {
        foreach (var (token, name) in candidates)
        {
            if (userAgent.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }
}
