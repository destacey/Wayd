namespace Wayd.Infrastructure.RateLimiting;

/// <summary>The names of the API's rate-limiting policies, for <c>RequireRateLimiting</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// The OAuth authorization endpoint: anonymous, and it makes Wayd fetch a URL the caller chooses, so it is held
    /// tighter than the token endpoint.
    /// </summary>
    public const string OAuthAuthorize = "oauth-authorize";

    /// <summary>The OAuth token endpoint: anonymous, and the place an authorization code or refresh token would be guessed.</summary>
    public const string OAuthToken = "oauth-token";
}

/// <summary>The limits behind <see cref="RateLimitPolicies"/>, read from <see cref="SectionName"/> and applied without a restart.</summary>
public sealed class RateLimitSettings
{
    /// <summary>The configuration section the limits are read from.</summary>
    public const string SectionName = "RateLimitSettings";

    /// <summary>Requests per caller address to the OAuth authorization endpoint.</summary>
    public FixedWindowLimit OAuthAuthorize { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>Requests per caller address to the OAuth token endpoint.</summary>
    public FixedWindowLimit OAuthToken { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
}

/// <summary>A number of requests allowed in each fixed window of time.</summary>
public sealed class FixedWindowLimit
{
    /// <summary>How many requests are allowed in each window.</summary>
    public int PermitLimit { get; set; }

    /// <summary>How long each window lasts, in seconds.</summary>
    public int WindowSeconds { get; set; }
}
