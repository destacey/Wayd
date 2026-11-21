using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Wayd.Common.Domain;

namespace Wayd.Infrastructure.Middleware;

/// <summary>
/// Turns an exception that escapes the request pipeline into a problem details response and logs it.
/// </summary>
/// <remarks>
/// A <see cref="CustomException"/> is a deliberate outcome, so its message is the response's
/// <c>detail</c>. Any other exception is a fault whose text — a database message, a stack trace — is for
/// the log: outside Development the caller gets <see cref="ErrorMessages.Unexpected"/> and the
/// <c>traceId</c> that finds the log entry.
/// </remarks>
public sealed class ExceptionMiddleware(IProblemDetailsService problemDetailsService, ICurrentUser currentUser, IHostEnvironment environment) : IMiddleware
{
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly bool _isDevelopment = environment.IsDevelopment();

    /// <summary>Runs the rest of the pipeline and answers for any exception it throws.</summary>
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
            // This is not a server fault — most commonly a poller or a browser navigating away. Log it
            // quietly and don't try to write a response: the socket is already gone, so a write would
            // throw a second, more confusing exception.
            Log.Debug(ex, "Request aborted by the client before completion ({Method} {Path}).",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        catch (BadHttpRequestException ex)
        {
            await WriteBadRequest(ex, httpContext);
        }
        catch (Exception ex)
        {
            await WriteUnhandled(ex, httpContext);
        }
    }

    /// <summary>
    /// Answers a request Kestrel refused — a body over the size limit (413), a malformed body (400) — with
    /// the status it chose. Its message describes the request, not the server, so it is safe to return.
    /// </summary>
    private async Task WriteBadRequest(BadHttpRequestException exception, HttpContext httpContext)
    {
        Log.Information(exception, "Rejected a bad request with {StatusCode} ({Method} {Path}).",
            exception.StatusCode, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = exception.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = ReasonPhrases.GetReasonPhrase(exception.StatusCode),
                Status = exception.StatusCode,
                Detail = exception.Message
            }
        });
    }

    private async Task WriteUnhandled(Exception exception, HttpContext httpContext)
    {
        using (LogContext.PushProperty("CorrelationId", httpContext.TraceIdentifier))
        using (LogContext.PushProperty("UserEmail", _currentUser.GetUserEmail() is string userEmail ? userEmail : "Anonymous"))
        using (LogContext.PushProperty("UserId", _currentUser.GetUserId()))
        using (LogContext.PushProperty("ExceptionMessage", exception.Message))
        using (LogContext.PushProperty("SourceContext", $"{typeof(ExceptionMiddleware).FullName}"))
        {
            // A wrapper (an aggregate, a framework's invocation exception) says less than what it wraps, and
            // a deliberate outcome — a not-found, a validation failure — can arrive wrapped. Unwrapping stops
            // at the first CustomException, so both the status and the detail describe that outcome.
            var cause = exception;
            while (cause is not CustomException && cause.InnerException is not null)
                cause = cause.InnerException;

            var statusCode = GetStatusCodeFromException(cause);
            httpContext.Response.ContentType = "application/problem+json";
            httpContext.Response.StatusCode = statusCode;

            Log.Write(GetLogLevelFromException(cause), exception, "An unhandled exception has occurred while executing the request.");

            if (cause is ValidationException validationException)
            {
                var validationProblemDetails = CreateValidationProblemDetails(validationException, httpContext);

                var json = System.Text.Json.JsonSerializer.Serialize(validationProblemDetails);
                await httpContext.Response.WriteAsync(json);

                return;
            }

            var problemDetails = new ProblemDetails
            {
                Title = "An error occurred while processing your request.",
                Status = statusCode,
                Detail = cause is CustomException || _isDevelopment ? cause.Message : ErrorMessages.Unexpected
            };

            await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = cause,
                ProblemDetails = problemDetails
            });
        }
    }

    /// <summary>
    /// True when the exception represents the client abandoning the request rather than a server
    /// fault: a request body cut short once the response is underway, or a cancellation that lines up
    /// with <see cref="HttpContext.RequestAborted"/> firing. These are logged quietly and never produce
    /// an error response — the connection is already gone.
    /// </summary>
    private static bool IsClientDisconnect(Exception exception, HttpContext httpContext)
    {
        if (httpContext.RequestAborted.IsCancellationRequested
            && exception is BadHttpRequestException or OperationCanceledException)
            return true;

        // Once the response has started, its status is sent and a problem body can no longer be written.
        if (exception is BadHttpRequestException && httpContext.Response.HasStarted)
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
        validationProblemDetails.Type = "https://tools.ietf.org/html/rfc4918#section-11.2";
        validationProblemDetails.Title = "One or more validation errors occurred.";
        validationProblemDetails.Status = StatusCodes.Status422UnprocessableEntity;
        validationProblemDetails.Detail = "See the errors property for details.";
        validationProblemDetails.Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}";
        validationProblemDetails.Extensions = new Dictionary<string, object?>
        {
            ["requestId"] = httpContext.TraceIdentifier,
            ["traceId"] = ProblemTrace.Id(httpContext)
        };

        return validationProblemDetails;
    }
}