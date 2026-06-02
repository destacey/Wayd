using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace Wayd.Infrastructure.Middleware;

public sealed class ExceptionMiddleware(IProblemDetailsService problemDetailsService, ICurrentUser currentUser) : IMiddleware
{
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        try
        {
            await next(httpContext);
        }
        catch (Exception ex) when (IsClientDisconnect(ex, httpContext))
        {
            // The client gave up mid-request: it either sent a truncated body (BadHttpRequestException,
            // "Unexpected end of request content") or cancelled an in-flight request (RequestAborted).
            // This is not a server fault — most commonly it's the Hangfire dashboard's stats poller or
            // a browser navigating away. Log it quietly and don't try to write a response: the socket is
            // already gone, so a write would throw a second, more confusing exception.
            Log.Debug(ex, "Request aborted by the client before completion ({Method} {Path}).",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("CorrelationId", httpContext.TraceIdentifier))
            using (LogContext.PushProperty("UserEmail", _currentUser.GetUserEmail() is string userEmail ? userEmail : "Anonymous"))
            using (LogContext.PushProperty("UserId", _currentUser.GetUserId()))
            using (LogContext.PushProperty("ExceptionMessage", ex.Message))
            using (LogContext.PushProperty("SourceContext", $"{typeof(ExceptionMiddleware).FullName}"))
            {

                httpContext.Response.ContentType = "application/problem+json";
                httpContext.Response.StatusCode = GetStatusCodeFromException(ex);

                if (ex is not CustomException && ex.InnerException is not null)
                {
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }
                }

                var logLevel = GetLogLevelFromException(ex);
                Log.Write(logLevel, ex, "An unhandled exception has occurred while executing the request.");

                if (ex is ValidationException validationException)
                {
                    var validationProblemDetails = CreateValidationProblemDetails(validationException, httpContext);

                    var json = System.Text.Json.JsonSerializer.Serialize(validationProblemDetails);
                    await httpContext.Response.WriteAsync(json);

                    return;
                }

                var problemDetails = new ProblemDetails
                {
                    Title = "An error occurred while processing your request.",
                    Detail = ex.Message
                };

                await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    Exception = ex,
                    ProblemDetails = problemDetails
                });
            }
        }
    }

    /// <summary>
    /// True when the exception represents the client abandoning the request rather than a server
    /// fault: a truncated/malformed request body (<see cref="BadHttpRequestException"/>) or a
    /// cancellation that lines up with <see cref="HttpContext.RequestAborted"/> firing. These should
    /// be logged quietly and never produce an error response — the connection is already gone.
    /// </summary>
    private static bool IsClientDisconnect(Exception exception, HttpContext httpContext)
    {
        // Kestrel surfaces a truncated/aborted request body as BadHttpRequestException (a client 400).
        if (exception is BadHttpRequestException)
            return true;

        // A cancellation that coincides with the request being aborted is the client hanging up, not
        // an internal timeout — distinguish on RequestAborted so we don't swallow genuine cancellations.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return true;

        return false;
    }

    public static int GetStatusCodeFromException(Exception exception)
    {
        return exception switch
        {
            ApplicationException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            ValidationException => StatusCodes.Status422UnprocessableEntity,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
    }
    public static LogEventLevel GetLogLevelFromException(Exception exception)
    {
        return exception switch
        {
            ApplicationException => LogEventLevel.Error,
            UnauthorizedException => LogEventLevel.Warning,
            ForbiddenException => LogEventLevel.Warning,
            NotFoundException => LogEventLevel.Information,
            ConflictException => LogEventLevel.Warning,
            ValidationException => LogEventLevel.Information,
            ServiceUnavailableException => LogEventLevel.Warning,
            _ => LogEventLevel.Error
        };
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(ValidationException exception, HttpContext httpContext)
    {
        return EnrichValidationProblemDetails(new ValidationProblemDetails(exception.Errors), httpContext);
    }

    public static ValidationProblemDetails EnrichValidationProblemDetails(ValidationProblemDetails validationProblemDetails, HttpContext httpContext)
    {
        Activity? activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;

        validationProblemDetails.Type = "https://tools.ietf.org/html/rfc4918#section-11.2";
        validationProblemDetails.Title = "One or more validation errors occurred.";
        validationProblemDetails.Status = StatusCodes.Status422UnprocessableEntity;
        validationProblemDetails.Detail = "See the errors property for details.";
        validationProblemDetails.Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}";
        validationProblemDetails.Extensions = new Dictionary<string, object?>
        {
            ["requestId"] = httpContext.TraceIdentifier,
            ["traceId"] = activity?.Id
        };

        return validationProblemDetails;
    }
}