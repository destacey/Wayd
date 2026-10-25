using System.Net;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Waits out the API's rate limit and sends the request again, so a seed that outpaces its per-user limit slows down
/// rather than failing partway through.
/// </summary>
/// <remarks>
/// A refused request was never handled, so sending it again repeats nothing. The bodies a seed sends (JSON strings and
/// in-memory CSV bytes) can be read more than once, which a resend needs.
/// </remarks>
internal sealed class RateLimitRetryHandler : DelegatingHandler
{
    /// <summary>How many times a request is sent before its last refusal is returned.</summary>
    internal const int MaxAttempts = 6;

    /// <summary>The wait when a refusal does not say how long; the API's limits refill within seconds.</summary>
    internal static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(2);

    /// <summary>The longest single wait, so a misconfigured limit cannot stall a seed for a whole window.</summary>
    internal static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a request may take, waits included, for the client this handler sits in: the waits happen inside one
    /// send, so a client's default 100-second timeout would cancel a request partway through its retries.
    /// </summary>
    internal static readonly TimeSpan ClientTimeout = TimeSpan.FromMinutes(5);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RateLimitRetryHandler(HttpMessageHandler innerHandler)
        : this(innerHandler, Task.Delay)
    {
    }

    internal RateLimitRetryHandler(HttpMessageHandler innerHandler, Func<TimeSpan, CancellationToken, Task> delay)
        : base(innerHandler)
    {
        _delay = delay;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == MaxAttempts)
                return response;

            var wait = WaitFor(response);
            response.Dispose();
            await _delay(wait, cancellationToken);
        }
    }

    /// <summary>
    /// How long a refusal asks the caller to wait, capped at <see cref="MaxWait"/>. The API states it in seconds; a
    /// refusal that states no positive wait gets <see cref="DefaultWait"/>.
    /// </summary>
    internal static TimeSpan WaitFor(HttpResponseMessage response) =>
        response.Headers.RetryAfter?.Delta is { } wait && wait > TimeSpan.Zero
            ? (wait > MaxWait ? MaxWait : wait)
            : DefaultWait;
}
