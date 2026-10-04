using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Sends a request through the API's own middleware pipeline in process, so a tool call is authenticated,
/// authorized, validated and answered by exactly the code that serves the same HTTP request.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IStartupFilter"/> to capture the pipeline from its first middleware. The
/// caller's credentials are forwarded rather than its principal, because the authorization middleware
/// authenticates afresh with the schemes its policy names.
/// </remarks>
public sealed class ApiPipeline(IHttpContextFactory httpContextFactory) : IStartupFilter
{
    /// <summary>Headers carried from the caller's request, which between them hold every credential the API accepts.</summary>
    private static readonly string[] _forwardedHeaders = [HeaderNames.Authorization, "x-api-key"];

    private readonly IHttpContextFactory _httpContextFactory = httpContextFactory;
    private RequestDelegate? _pipeline;

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(rest =>
        {
            _pipeline = rest;
            return rest;
        });
        next(app);
    };

    /// <summary>Sends a request on behalf of <paramref name="caller"/> and returns the API's answer.</summary>
    /// <param name="caller">The request whose credentials the inner request carries.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path, starting with a slash.</param>
    /// <param name="query">The query string, empty or starting with a question mark.</param>
    /// <param name="headers">Extra request headers.</param>
    /// <param name="body">The request body and its content type, if any.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<ApiResponse> Send(
        HttpContext caller,
        string method,
        string path,
        QueryString query,
        IReadOnlyDictionary<string, string>? headers,
        (byte[] Content, string ContentType)? body,
        CancellationToken cancellationToken)
    {
        var pipeline = _pipeline ?? throw new InvalidOperationException("The API pipeline has not been built yet.");

        var requestHeaders = new HeaderDictionary
        {
            [HeaderNames.Host] = caller.Request.Host.Value ?? "localhost",
            [HeaderNames.Accept] = "application/json",
        };
        foreach (var name in _forwardedHeaders)
        {
            if (caller.Request.Headers.TryGetValue(name, out var value))
                requestHeaders[name] = value;
        }
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            requestHeaders[name] = value;
        if (body is { } content)
        {
            requestHeaders[HeaderNames.ContentType] = content.ContentType;
            requestHeaders[HeaderNames.ContentLength] = content.Content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var request = new HttpRequestFeature
        {
            Method = method,
            Scheme = caller.Request.Scheme,
            Protocol = caller.Request.Protocol,
            PathBase = caller.Request.PathBase,
            Path = path,
            QueryString = query.Value ?? string.Empty,
            Headers = requestHeaders,
            Body = body is { } b ? new MemoryStream(b.Content, writable: false) : Stream.Null,
        };

        var connection = caller.Features.Get<IHttpConnectionFeature>();
        var parentActivity = Activity.Current;

        // HttpContextAccessor clears the context of whatever holder the current execution context carries
        // when the inner context is assigned to it, which would leave the caller's own request with no
        // context for the rest of its life. Running without the caller's execution context gives the inner
        // request a holder of its own.
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(async () =>
            {
                Activity.Current = parentActivity;
                return await Invoke(pipeline, request, connection, cancellationToken);
            }, cancellationToken);
        }
    }

    private async Task<ApiResponse> Invoke(RequestDelegate pipeline, HttpRequestFeature request, IHttpConnectionFeature? connection, CancellationToken cancellationToken)
    {
        using var responseBody = new MemoryStream();
        var response = new HttpResponseFeature();

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(request);
        features.Set<IHttpResponseFeature>(response);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(responseBody));
        features.Set<IHttpRequestLifetimeFeature>(new HttpRequestLifetimeFeature { RequestAborted = cancellationToken });
        features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature());
        features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
        {
            ConnectionId = connection?.ConnectionId,
            RemoteIpAddress = connection?.RemoteIpAddress,
            RemotePort = connection?.RemotePort ?? 0,
            LocalIpAddress = connection?.LocalIpAddress,
            LocalPort = connection?.LocalPort ?? 0,
        });

        var context = _httpContextFactory.Create(features);
        try
        {
            await pipeline(context);
        }
        finally
        {
            _httpContextFactory.Dispose(context);
        }

        return new ApiResponse(
            response.StatusCode,
            response.Headers.ContentType.FirstOrDefault(),
            responseBody.ToArray());
    }
}

/// <summary>What the API answered to a request sent through <see cref="ApiPipeline"/>.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="ContentType">The response's content type, if it has a body.</param>
/// <param name="Body">The response body, empty when there is none.</param>
public sealed record ApiResponse(int StatusCode, string? ContentType, byte[] Body)
{
    /// <summary>Whether the status code reports success.</summary>
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}
