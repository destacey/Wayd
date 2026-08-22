using Microsoft.AspNetCore.Http;

namespace Wayd.Infrastructure.Auth.Local;

/// <summary>
/// Reads the current request's User-Agent and remote address for recording on a new session.
/// </summary>
internal interface ISessionContextAccessor : IScopedService
{
    /// <summary>
    /// The calling request's device detail, or <see cref="SessionContext.None"/> off the HTTP
    /// path (background jobs, tests), where a session has no originating request.
    /// </summary>
    SessionContext Current { get; }
}

internal sealed class SessionContextAccessor(IHttpContextAccessor httpContextAccessor) : ISessionContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public SessionContext Current
    {
        get
        {
            var http = _httpContextAccessor.HttpContext;
            if (http is null)
            {
                return SessionContext.None;
            }

            // RemoteIpAddress is the proxy's address unless forwarded headers are configured;
            // it is display-only, so an inaccurate value costs a confusing row, not access.
            return new SessionContext(
                http.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null,
                http.Connection.RemoteIpAddress?.ToString());
        }
    }
}
