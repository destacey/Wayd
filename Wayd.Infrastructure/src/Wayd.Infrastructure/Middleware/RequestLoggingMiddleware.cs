using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Wayd.Infrastructure.Middleware;

public class RequestLoggingMiddleware(
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IMiddleware
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        var userEmail = _currentUser.GetUserEmail();
        var userId = _currentUser.GetUserId();
        var authenticationScheme = httpContext.User?.Identity?.AuthenticationType ?? "None";

        using (LogContext.PushProperty("CorrelationId", httpContext.TraceIdentifier))
        using (LogContext.PushProperty("RequestTimestampUtc", _dateTimeProvider.Now))
        using (LogContext.PushProperty("UserEmail", userEmail is string email && !string.IsNullOrEmpty(email) ? email : "Anonymous"))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("AuthenticationScheme", authenticationScheme))
        {
            await next(httpContext);
        }
    }
}