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

    /// <summary>
    /// CSV import submissions, held to a number in flight per user: one reads and validates a whole file inside the
    /// request, so its cost is the file, not the request.
    /// </summary>
    public const string Imports = "imports";
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

    /// <summary>
    /// Requests per signed-in user to every endpoint, on top of any endpoint's own limit. A burst covers a page that
    /// loads many things at once; the steady rate is far above a person clicking and caps a runaway script or agent.
    /// </summary>
    public TokenBucketLimit PerUser { get; set; } = new() { TokenLimit = 200, TokensPerPeriod = 5, PeriodSeconds = 1 };

    /// <summary>
    /// What <see cref="PerUser"/> becomes for a user holding <c>Permissions.RateLimits.Elevated</c>, such as an admin or
    /// a service account. Kept a multiple of the standard bucket rather than unlimited, so a runaway script with the
    /// permission is still capped; the host refuses to start if it is smaller than the standard bucket.
    /// </summary>
    public TokenBucketLimit PerUserElevated { get; set; } = new() { TokenLimit = 1000, TokensPerPeriod = 25, PeriodSeconds = 1 };

    /// <summary>
    /// How long a user's tier is remembered before their permissions are read again, in seconds. A grant or revoke of
    /// the elevated permission therefore takes up to this long to apply. Zero reads the permissions on every request.
    /// </summary>
    public int TierCacheSeconds { get; set; } = 300;

    /// <summary>CSV import submissions per signed-in user in flight at once.</summary>
    public ConcurrencyLimit Imports { get; set; } = new() { PermitLimit = 2 };

    /// <summary>Every limit, so startup validation cannot miss one.</summary>
    internal IEnumerable<IRateLimit> All() =>
        [OAuthAuthorize, OAuthToken, AuthSignIn, AuthRefresh, AuthProviders, AuthSetup, PerUser, PerUserElevated, Imports];
}

/// <summary>A limit in <see cref="RateLimitSettings"/>, which the host refuses to start with unless it is valid.</summary>
public interface IRateLimit
{
    /// <summary>Whether every number in the limit is above zero; zero would refuse every request.</summary>
    bool IsValid();
}

/// <summary>A number of requests allowed in each fixed window of time.</summary>
public sealed class FixedWindowLimit : IRateLimit
{
    /// <summary>How many requests are allowed in each window.</summary>
    public int PermitLimit { get; set; }

    /// <summary>How long each window lasts, in seconds.</summary>
    public int WindowSeconds { get; set; }

    /// <inheritdoc />
    public bool IsValid() => PermitLimit > 0 && WindowSeconds > 0;
}

/// <summary>
/// A bucket of requests that refills at a steady rate: a caller may spend the whole bucket at once, then only as fast
/// as it refills.
/// </summary>
public sealed class TokenBucketLimit : IRateLimit
{
    /// <summary>How many requests the bucket holds, which is the largest burst allowed.</summary>
    public int TokenLimit { get; set; }

    /// <summary>How many requests are added back each period, up to <see cref="TokenLimit"/>.</summary>
    public int TokensPerPeriod { get; set; }

    /// <summary>How long each refill period lasts, in seconds.</summary>
    public int PeriodSeconds { get; set; }

    /// <inheritdoc />
    public bool IsValid() => TokenLimit > 0 && TokensPerPeriod > 0 && PeriodSeconds > 0;

    /// <summary>Whether this bucket is at least as large as <paramref name="other"/> and refills at least as fast.</summary>
    public bool IsAtLeast(TokenBucketLimit other) =>
        TokenLimit >= other.TokenLimit
        && (long)TokensPerPeriod * other.PeriodSeconds >= (long)other.TokensPerPeriod * PeriodSeconds;
}

/// <summary>A number of requests allowed in flight at once.</summary>
public sealed class ConcurrencyLimit : IRateLimit
{
    /// <summary>How many requests may run at the same time.</summary>
    public int PermitLimit { get; set; }

    /// <inheritdoc />
    public bool IsValid() => PermitLimit > 0;
}
