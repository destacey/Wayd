using Microsoft.AspNetCore.Http;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Middleware;

public class UserActivityTrackingMiddleware(
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    LastSeenWriter lastSeenWriter) : IMiddleware
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly LastSeenWriter _lastSeenWriter = lastSeenWriter;

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        if (_currentUser.IsAuthenticated())
        {
            var userId = _currentUser.GetUserId();
            if (!string.IsNullOrEmpty(userId))
                _lastSeenWriter.RecordUserActivity(userId, _dateTimeProvider.Now);
        }

        await next(httpContext);
    }
}
