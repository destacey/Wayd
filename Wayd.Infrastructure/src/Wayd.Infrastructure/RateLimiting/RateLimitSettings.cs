namespace Wayd.Infrastructure.RateLimiting;

/// <summary>
/// The names of the API's rate-limiting policies, for <c>RequireRateLimiting</c> or <c>[EnableRateLimiting]</c>.
/// Endpoints that name the same policy share each caller's window, so a policy groups only endpoints whose traffic
/// should count against one budget.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// The OAuth authorization endpoint: anonymous, and it makes Wayd fetch a URL the caller chooses, so it is held
    /// tighter than the token endpoint.
    /// </summary>
    public const string OAuthAuthorize = "oauth-authorize";

    /// <summary>The OAuth token endpoint: anonymous, and the place an authorization code or refresh token would be guessed.</summary>
    public const string OAuthToken = "oauth-token";

    /// <summary>
    /// Password sign-in and the identity-provider token exchange: anonymous, where passwords would be sprayed across
    /// accounts and where a flood would make Wayd validate tokens against an identity provider.
    /// </summary>
    public const string AuthSignIn = "auth-sign-in";

    /// <summary>Refresh-token rotation: anonymous but routine for every signed-in browser, so held loosely.</summary>
    public const string AuthRefresh = "auth-refresh";

    /// <summary>The list of identity providers the sign-in page asks for before anyone has signed in.</summary>
    public const string AuthProviders = "auth-providers";

    /// <summary>First-run setup, which checks the one-time bootstrap token and so is held tightest.</summary>
    public const string AuthSetup = "auth-setup";
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

    /// <summary>
    /// Requests per caller address to password sign-in and the token exchange, counted together. Sized for an office
    /// whose people all sign in from one address.
    /// </summary>
    public FixedWindowLimit AuthSignIn { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>Requests per caller address to the refresh-token endpoint.</summary>
    public FixedWindowLimit AuthRefresh { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    /// <summary>Requests per caller address to the identity provider list.</summary>
    public FixedWindowLimit AuthProviders { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>Requests per caller address to first-run setup.</summary>
    public FixedWindowLimit AuthSetup { get; set; } = new() { PermitLimit = 5, WindowSeconds = 900 };

    /// <summary>Every limit, so startup validation cannot miss one.</summary>
    internal IEnumerable<FixedWindowLimit> All() =>
        [OAuthAuthorize, OAuthToken, AuthSignIn, AuthRefresh, AuthProviders, AuthSetup];
}

/// <summary>A number of requests allowed in each fixed window of time.</summary>
public sealed class FixedWindowLimit
{
    /// <summary>How many requests are allowed in each window.</summary>
    public int PermitLimit { get; set; }

    /// <summary>How long each window lasts, in seconds.</summary>
    public int WindowSeconds { get; set; }
}
