using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Wayd.Infrastructure.Common.Services;

public class RequestCorrelationIdProvider(IHttpContextAccessor httpContextAccessor) : IRequestCorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// Returns the TraceIdentifier from the HttpContext if it exists, otherwise null.
    /// </summary>
    public string? RequestCorrelationId => _httpContextAccessor.HttpContext?.TraceIdentifier;

    /// <summary>
    /// Returns the <see cref="RequestCorrelationId"/> when handling an HTTP request; otherwise the
    /// current <see cref="Activity"/>'s trace id, so work with no <c>HttpContext</c> (Hangfire jobs,
    /// Wolverine handlers running in their own scope) still gets a stable correlation id that lines up
    /// with the distributed trace — the same root id Wolverine uses for <c>Envelope.CorrelationId</c>.
    /// Falls back to a new Guid only when there is neither an HTTP request nor an active trace.
    /// </summary>
    public string CorrelationId =>
        RequestCorrelationId
        ?? Activity.Current?.TraceId.ToString()
        ?? Guid.NewGuid().ToString();
}
