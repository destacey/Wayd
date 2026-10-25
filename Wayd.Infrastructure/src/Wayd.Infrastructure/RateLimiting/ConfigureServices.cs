using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.RateLimiting;

/// <summary>
/// Registers the API's rate limits: one per signed-in user across every endpoint, and the policies an endpoint opts
/// into by naming one, with <c>RequireRateLimiting</c> on a minimal endpoint or <c>[EnableRateLimiting]</c> on a
/// controller action. A request must pass both.
/// </summary>
/// <remarks>
/// A request an endpoint policy refuses still spends from the user's bucket, twice: the middleware takes a token, then
/// on the endpoint's refusal tries again through its waiting path and takes another, and a token bucket returns
/// neither. A caller retrying a refused import therefore drains its per-user budget twice as fast. That is the
/// framework's behaviour, and avoiding it would mean chaining the per-user limiter into every policy by hand.
/// </remarks>
internal static class ConfigureServices
{
    /// <summary>
    /// Registers the limits and their settings, which must give every limit positive numbers or the host does not
    /// start.
    /// </summary>
    internal static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RateLimitSettings>()
            .Bind(config.GetSection(RateLimitSettings.SectionName))
            .Validate(
                s => s.All().All(l => l.IsValid()),
                $"Every limit in '{RateLimitSettings.SectionName}' needs every number above zero.")
            .ValidateOnStart();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => new ValueTask(Reject(context, cancellationToken));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(PerUser);

            options.AddPolicy(RateLimitPolicies.OAuthAuthorize, context => PerClientAddress(context, s => s.OAuthAuthorize));
            options.AddPolicy(RateLimitPolicies.OAuthToken, context => PerClientAddress(context, s => s.OAuthToken));
            options.AddPolicy(RateLimitPolicies.AuthSignIn, context => PerClientAddress(context, s => s.AuthSignIn));
            options.AddPolicy(RateLimitPolicies.AuthRefresh, context => PerClientAddress(context, s => s.AuthRefresh));
            options.AddPolicy(RateLimitPolicies.AuthProviders, context => PerClientAddress(context, s => s.AuthProviders));
            options.AddPolicy(RateLimitPolicies.AuthSetup, context => PerClientAddress(context, s => s.AuthSetup));
            options.AddPolicy(RateLimitPolicies.Imports, ImportsPerUser);
        });
    }

    /// <summary>
    /// Applies the limits. It must run after authorization, because that is where a request's user becomes known:
    /// the API has no default authentication scheme, so each authorization policy authenticates with its own.
    /// </summary>
    internal static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app) => app.UseRateLimiter();

    /// <summary>
    /// Gives every endpoint marked <see cref="CsvImportAttribute"/> the <see cref="RateLimitPolicies.Imports"/> policy,
    /// so an import added later is limited without anyone remembering to.
    /// </summary>
    internal static TBuilder LimitImportSubmissions<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint =>
        {
            if (endpoint.Metadata.OfType<CsvImportAttribute>().Any())
                endpoint.Metadata.Add(new EnableRateLimitingAttribute(RateLimitPolicies.Imports));
        });
        return builder;
    }

    /// <summary>
    /// Sets <c>Retry-After</c> when the limit knows the wait, then writes the endpoint's own refusal, or problem
    /// details when it has none.
    /// </summary>
    private static async Task Reject(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        TimeSpan? retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : null;
        if (retryAfter is { } known)
            http.Response.Headers.RetryAfter = RateLimitRejection.Seconds(known).ToString(CultureInfo.InvariantCulture);

        LogRejection(http);

        if (http.GetEndpoint()?.Metadata.GetMetadata<RateLimitRejection>() is { } rejection)
        {
            await rejection.Write(http, retryAfter, cancellationToken);
            return;
        }

        var written = await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = RateLimitRejection.Describe(retryAfter),
            },
        });
        if (!written)
        {
            http.Response.ContentType = "text/plain; charset=utf-8";
            await http.Response.WriteAsync(RateLimitRejection.Describe(retryAfter), cancellationToken);
        }
    }

    /// <summary>
    /// Records who was refused and where, which the request log cannot: a refused request never reaches it, and the
    /// rate-limiting metrics count refusals by policy, not by caller.
    /// </summary>
    /// <remarks>
    /// The refusal does not say which limit made it, so the endpoint's policy is named alongside: a refusal on an
    /// endpoint with no policy was the per-user limit's, and one on an endpoint with a policy was either.
    /// </remarks>
    private static void LogRejection(HttpContext http)
    {
        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ConfigureServices).Namespace!);
        if (!logger.IsEnabled(LogLevel.Warning))
            return;

        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var caller = UserIdOf(http.User) is { } userId ? $"user {userId}" : $"address {ClientAddressKey.For(http.Connection.RemoteIpAddress)}";
        logger.LogWarning(
            "Rate limit refused {Method} {Path} for {Caller}; endpoint policy {Policy}.",
            http.Request.Method, http.Request.Path.Value, caller, policy ?? "none");
    }

    /// <summary>
    /// A token bucket per signed-in user, whatever credential they called with, so minting more tokens buys no more
    /// room. An anonymous request is not limited here; the anonymous endpoints carry per-address policies instead.
    /// </summary>
    /// <remarks>The limit is part of the key for the reason given on <see cref="PerClientAddress"/>.</remarks>
    private static RateLimitPartition<string> PerUser(HttpContext context)
    {
        if (UserIdOf(context.User) is not { } userId)
            return RateLimitPartition.GetNoLimiter(string.Empty);

        var limit = Settings(context).PerUser;
        return RateLimitPartition.GetTokenBucketLimiter($"{userId}|{limit.TokenLimit}|{limit.TokensPerPeriod}|{limit.PeriodSeconds}", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = limit.TokenLimit,
            TokensPerPeriod = limit.TokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(limit.PeriodSeconds),
            QueueLimit = 0,
            // The partitioned limiter refills every bucket from one timer; each refilling its own would add a timer per user.
            AutoReplenishment = false,
        });
    }

    /// <summary>
    /// A number of import submissions in flight per signed-in user. An import endpoint always needs a signed-in user,
    /// so a request without one is refused by authorization before it gets here.
    /// </summary>
    private static RateLimitPartition<string> ImportsPerUser(HttpContext context)
    {
        if (UserIdOf(context.User) is not { } userId)
            return RateLimitPartition.GetNoLimiter(string.Empty);

        var limit = Settings(context).Imports;
        return RateLimitPartition.GetConcurrencyLimiter($"{userId}|{limit.PermitLimit}", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = limit.PermitLimit,
            QueueLimit = 0,
        });
    }

    /// <summary>
    /// A fixed window per caller address. Behind a proxy the address is the proxy's, so every caller shares one
    /// window, unless forwarded headers are enabled (<c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c>).
    /// </summary>
    /// <remarks>
    /// The limit is part of the partition key because a limiter is created once per key: keyed on the address alone,
    /// a busy caller would keep the old limit after the settings change.
    /// </remarks>
    private static RateLimitPartition<string> PerClientAddress(HttpContext context, Func<RateLimitSettings, FixedWindowLimit> select)
    {
        var limit = select(Settings(context));
        var address = ClientAddressKey.For(context.Connection.RemoteIpAddress);

        return RateLimitPartition.GetFixedWindowLimiter($"{address}|{limit.PermitLimit}|{limit.WindowSeconds}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit.PermitLimit,
            Window = TimeSpan.FromSeconds(limit.WindowSeconds),
            QueueLimit = 0,
        });
    }

    private static RateLimitSettings Settings(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitSettings>>().CurrentValue;

    /// <summary>The signed-in user's id, or null for a request no authorization policy has authenticated.</summary>
    private static string? UserIdOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.GetUserId() is { Length: > 0 } id ? id : null;
}
