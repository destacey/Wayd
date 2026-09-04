using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wayd.Infrastructure.Middleware;
using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace Wayd.Web.Api.Extensions;

public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Builds the 422 body for a CSV import's per-row validation failures.
    /// </summary>
    /// <remarks>
    /// The import endpoints validate row by row and collect the failures into <c>ModelState</c>
    /// themselves, so they never reach the automatic model-state filter that would otherwise shape the
    /// response. Returning the raw FluentValidation result instead serialises
    /// <c>{ isValid, errors: [ { propertyName, errorMessage, ... } ] }</c> — an array of objects rather
    /// than the <c>errors: { field: [message] }</c> dictionary a ProblemDetails client reads, so a
    /// caller that flattens the dictionary gets <c>[object Object]</c> and the messages naming the
    /// offending row are lost.
    /// <para>
    /// This routes the collected ModelState through the same enrichment the rest of the API uses, so an
    /// import's 422 matches every other 422 and its declared response type.
    /// </para>
    /// </remarks>
    public static ValidationProblemDetails ForValidationErrors(ModelStateDictionary modelState, HttpContext context) =>
        ExceptionMiddleware.EnrichValidationProblemDetails(new ValidationProblemDetails(modelState), context);

    public static ProblemDetails ForBadRequest(string error, HttpContext context)
    {
        Activity? activity = context.Features.Get<IHttpActivityFeature>()?.Activity;
        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Bad Request",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = error,
            Instance = $"{context.Request.Method} {context.Request.Path}",
            Extensions =
                {
                    ["requestId"] = context.TraceIdentifier,
                    ["traceId"] = activity?.Id
                }
        };
        return problemDetails;
    }

    public static ProblemDetails ForConflict(string error, HttpContext context)
    {
        Activity? activity = context.Features.Get<IHttpActivityFeature>()?.Activity;
        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            Title = "Conflict",
            Status = (int)HttpStatusCode.Conflict,
            Detail = error,
            Instance = $"{context.Request.Method} {context.Request.Path}",
            Extensions =
                {
                    ["requestId"] = context.TraceIdentifier,
                    ["traceId"] = activity?.Id
                }
        };
        return problemDetails;
    }

    public static ProblemDetails ForUnknownIdOrKeyType(HttpContext context)
    {
        return ForBadRequest("Unknown id or key type.", context);
    }

    /// <summary>
    /// Returns a ProblemDetails object for a route parameter mismatch. The route parameter name is assumed to be "Id".
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ProblemDetails ForRouteParamMismatch(HttpContext context)
    {
        return ForRouteParamMismatch("Id", "Id", context);
    }

    /// <summary>
    /// Returns a ProblemDetails object for a route parameter mismatch.
    /// </summary>
    /// <param name="routeParamName"></param>
    /// <param name="requestPropertyName"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ProblemDetails ForRouteParamMismatch(string routeParamName, string requestPropertyName, HttpContext context)
    {
        string capitalizedRouteParamName = char.ToUpper(routeParamName[0]) + routeParamName.Substring(1);
        return ForBadRequest($"The route {capitalizedRouteParamName} and request {requestPropertyName} do not match.", context);
    }

}
