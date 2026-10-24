using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Wayd.Infrastructure.RateLimiting;

/// <summary>
/// Endpoint metadata that answers a request refused by a rate limit in the endpoint's own format. Without it the
/// refusal is problem details, which suits the API but not a protocol endpoint whose clients parse something else,
/// such as OAuth's error object or a page shown in a browser.
/// </summary>
/// <param name="write">
/// Writes the body once the status (429) and any <c>Retry-After</c> are set. It receives how long the caller should
/// wait, when the limit knows.
/// </param>
public sealed class RateLimitRejection(Func<HttpContext, TimeSpan?, CancellationToken, Task> write)
{
    /// <summary>Writes the refusal's body.</summary>
    internal Task Write(HttpContext context, TimeSpan? retryAfter, CancellationToken cancellationToken) =>
        write(context, retryAfter, cancellationToken);

    /// <summary>The sentence every refusal carries, telling the caller how long to wait when that is known.</summary>
    public static string Describe(TimeSpan? retryAfter) => retryAfter is { } wait
        ? string.Create(CultureInfo.InvariantCulture, $"Too many requests. Try again in {Seconds(wait)} seconds.")
        : "Too many requests. Try again shortly.";

    /// <summary>A wait in whole seconds, rounded up so the caller never retries before the limit allows it.</summary>
    internal static int Seconds(TimeSpan wait) => Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
}
