using System.Net;
using Microsoft.Net.Http.Headers;
using Wayd.Infrastructure.Auth.OAuth;
using Wayd.Infrastructure.RateLimiting;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Serves the authorization server's protocol endpoints, which <see cref="AuthorizationServerMetadata"/> advertises:
/// the authorization endpoint a client sends the user to, and the token endpoint it gets tokens from. Both are
/// anonymous, rate-limited, and outside the OpenAPI document and so outside the MCP tools.
/// </summary>
public static class AuthorizationServerEndpoints
{
    /// <summary>Maps <see cref="AuthorizationServerMetadata.AuthorizePath"/> and <see cref="AuthorizationServerMetadata.TokenPath"/>.</summary>
    public static WebApplication MapWaydAuthorizationServer(this WebApplication app)
    {
        app.MapGet(AuthorizationServerMetadata.AuthorizePath, Authorize)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.OAuthAuthorize)
            .WithMetadata(new RateLimitRejection((context, retryAfter, _) =>
                ErrorPage(context, RateLimitRejection.Describe(retryAfter), StatusCodes.Status429TooManyRequests).ExecuteAsync(context)))
            .ExcludeFromDescription();

        app.MapPost(AuthorizationServerMetadata.TokenPath, Token)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.OAuthToken)
            .WithMetadata(new RateLimitRejection((context, retryAfter, _) =>
            {
                // OAuth clients parse the RFC 6749 §5.2 error object, not problem details.
                NeverCache(context);
                return Results.Json(
                    new TokenOutcome.Refused("temporarily_unavailable", RateLimitRejection.Describe(retryAfter)),
                    statusCode: StatusCodes.Status429TooManyRequests).ExecuteAsync(context);
            }))
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>Redirects the browser to the consent page, or shows the user why it cannot.</summary>
    private static async Task<IResult> Authorize(HttpContext context, IAuthorizationServer server)
    {
        var outcome = await server.Authorize(context.Request, context.RequestAborted);
        return outcome switch
        {
            AuthorizeOutcome.Redirect redirect => Results.Redirect(redirect.Location),
            AuthorizeOutcome.ShowError error => ErrorPage(context, error.Description, error.StatusCode),
            _ => throw new InvalidOperationException($"Unhandled authorization outcome {outcome.GetType().Name}."),
        };
    }

    /// <summary>Answers tokens as JSON, or the RFC 6749 §5.2 error with 400, never cached.</summary>
    private static async Task<IResult> Token(HttpContext context, IAuthorizationServer server)
    {
        var outcome = await server.Token(context.Request, context.RequestAborted);

        NeverCache(context);

        return outcome switch
        {
            TokenOutcome.Issued issued => Results.Json(issued),
            TokenOutcome.Refused refused => Results.Json(refused, statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException($"Unhandled token outcome {outcome.GetType().Name}."),
        };
    }

    /// <summary>RFC 6749 §5.1: a response carrying tokens, or saying why none were issued, must never be cached.</summary>
    private static void NeverCache(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    /// <summary>
    /// A bare page telling the user why the request was refused. It loads nothing and runs nothing, and its text is
    /// encoded, because part of it may come from the request or from the client's metadata document.
    /// </summary>
    private static IResult ErrorPage(HttpContext context, string description, int statusCode)
    {
        context.Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'";
        context.Response.Headers.CacheControl = "no-store";

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="referrer" content="no-referrer">
            <title>Wayd: authorization refused</title>
            <style>body{font-family:system-ui,sans-serif;max-width:36rem;margin:4rem auto;padding:0 1rem;line-height:1.5}</style>
            </head>
            <body>
            <h1>This connection request can't be completed</h1>
            <p>{{WebUtility.HtmlEncode(description)}}</p>
            <p>Return to the app that sent you here and try connecting again. If it keeps happening, tell the app's developer.</p>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);
    }
}
