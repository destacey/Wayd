using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Wayd.Infrastructure.Middleware;

/// <summary>
/// The id a problem details response gives its caller to quote when reporting it.
/// </summary>
public static class ProblemTrace
{
    /// <summary>
    /// The request's trace id, which finds its spans and log entries. ASP.NET Core creates the request's
    /// activity only while something listens for it, so without tracing this falls back to the request id,
    /// which the log also carries: a generic error message that asks for the id must always have one.
    /// </summary>
    public static string Id(HttpContext httpContext) =>
        httpContext.Features.Get<IHttpActivityFeature>()?.Activity?.Id
            ?? Activity.Current?.Id
            ?? httpContext.TraceIdentifier;
}
