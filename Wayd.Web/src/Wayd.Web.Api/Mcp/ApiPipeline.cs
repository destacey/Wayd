using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth.OAuth;
using Wayd.Infrastructure.Hosting;
using Wayd.Infrastructure.OpenTelemetry;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Sends a request through the API's own middleware pipeline in process, so a tool call is authenticated,
/// authorized, validated and answered by exactly the code that serves the same HTTP request.
/// </summary>
/// <remarks>
/// <para>
/// Registered as an <see cref="IStartupFilter"/> to capture the pipeline from its first middleware. The
/// caller's credentials are forwarded rather than its principal, because the authorization middleware
/// authenticates afresh with the schemes its policy names.
/// </para>
/// <para>
/// A personal access token or the web app's token is forwarded as it came. An OAuth access token is not: it is bound
/// to <c>/mcp</c>, which the API's endpoints refuse, so the request carries a token minted for it instead
/// (<see cref="DelegatedAccessTokens"/>) that names the user and, as its actor, the client, so a change is recorded
/// as made through that client.
/// </para>
/// </remarks>
public sealed class ApiPipeline(IHttpContextFactory httpContextFactory) : IStartupFilter
{
    /// <summary>Headers carried from the caller's request, which between them hold every credential the API accepts.</summary>
    private static readonly string[] _forwardedHeaders = [HeaderNames.Authorization, AuthConstants.ApiKeyHeaderName];

    private static readonly ActivitySource _activitySource = new(McpTelemetry.Name);
    private static readonly Meter _meter = new(McpTelemetry.Name);
    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>(
        McpTelemetry.ToolCallDuration, "s", "How long a hosted MCP tool call took to answer, by tool and credential.");

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
    /// <param name="tool">The name of the tool making the call, for telemetry.</param>
    /// <param name="caller">The request whose credentials the inner request carries.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path, starting with a slash.</param>
    /// <param name="query">The query string, empty or starting with a question mark.</param>
    /// <param name="headers">Extra request headers.</param>
    /// <param name="body">The request body and its content type, if any.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<ApiResponse> Send(
        string tool,
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
        var accessToken = OAuthAccessTokens.AccessTokenIdentityOf(caller.User);
        // The delegated token travels only in the request feature, never a header, so nothing that reads or logs
        // headers can carry it out of the process.
        var delegation = accessToken is null ? null : Delegate(caller, accessToken);
        if (delegation is null)
        {
            foreach (var name in _forwardedHeaders)
            {
                if (caller.Request.Headers.TryGetValue(name, out var value))
                    requestHeaders[name] = value;
            }
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
        var credential = accessToken is not null ? "oauth"
            : caller.Request.Headers.ContainsKey(AuthConstants.ApiKeyHeaderName) ? "personal_access_token"
            : "session";

        using var activity = _activitySource.StartActivity($"tools/call {tool}", ActivityKind.Internal);
        activity?.SetTag(McpTelemetry.ToolTag, tool);
        activity?.SetTag(McpTelemetry.CredentialTag, credential);
        activity?.SetTag(McpTelemetry.ClientIdTag, accessToken?.FindFirst(OAuthAccessTokens.ClientIdClaim)?.Value);
        activity?.SetTag("http.request.method", method);
        activity?.SetTag("url.path", path);
        var parentActivity = Activity.Current;
        var started = Stopwatch.GetTimestamp();

        Task<ApiResponse> sent;

        // HttpContextAccessor clears the context of whatever holder the current execution context carries
        // when the inner context is assigned to it, which would leave the caller's own request with no
        // context for the rest of its life. Running without the caller's execution context gives the inner
        // request a holder of its own.
        using (ExecutionContext.SuppressFlow())
        {
            sent = Task.Run(async () =>
            {
                Activity.Current = parentActivity;
                return await Invoke(pipeline, request, delegation, connection, cancellationToken);
            }, cancellationToken);
        }

        ApiResponse response;
        try
        {
            response = await sent;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            RecordDuration(started, tool, credential, new("error.type", ex.GetType().FullName));
            throw;
        }

        activity?.SetTag("http.response.status_code", response.StatusCode);
        if (!response.IsSuccess)
            activity?.SetStatus(ActivityStatusCode.Error);
        RecordDuration(started, tool, credential, new("http.response.status_code", response.StatusCode));

        return response;
    }

    /// <summary>Records how long a tool call took, with how it ended: its status code, or the error that ended it.</summary>
    private static void RecordDuration(long started, string tool, string credential, KeyValuePair<string, object?> outcome) =>
        _duration.Record(
            Stopwatch.GetElapsedTime(started).TotalSeconds,
            new KeyValuePair<string, object?>(McpTelemetry.ToolTag, tool),
            new KeyValuePair<string, object?>(McpTelemetry.CredentialTag, credential),
            outcome);

    /// <summary>
    /// Exchanges the caller's OAuth access token for a token the API accepts, naming the same user, the client as its
    /// actor, and the same scope. The <c>/mcp</c> scheme has already checked the access token and its session for this
    /// request. An access token issued before tokens had scopes carries none and keeps <see cref="TokenScopes.Default"/>.
    /// </summary>
    private static InProcessDelegation Delegate(HttpContext caller, ClaimsIdentity accessToken)
    {
        var userId = accessToken.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("An OAuth access token names no user.");
        var clientId = accessToken.FindFirst(OAuthAccessTokens.ClientIdClaim)?.Value
            ?? throw new InvalidOperationException("An OAuth access token names no client.");
        var scope = accessToken.FindFirst(TokenScopes.ClaimType)?.Value is { } named
            ? TokenScopes.Parse(named) ?? throw new InvalidOperationException("An OAuth access token names no scope Wayd defines.")
            : TokenScopes.Default;

        var services = caller.RequestServices;
        return new InProcessDelegation(DelegatedAccessTokens.Issue(
            services.GetRequiredService<IConfiguration>(),
            services.GetRequiredService<PublicUrl>().For(caller.Request),
            userId,
            clientId,
            scope,
            services.GetRequiredService<IDateTimeProvider>().Now));
    }

    private async Task<ApiResponse> Invoke(
        RequestDelegate pipeline,
        HttpRequestFeature request,
        InProcessDelegation? delegation,
        IHttpConnectionFeature? connection,
        CancellationToken cancellationToken)
    {
        using var responseBody = new MemoryStream();
        var response = new HttpResponseFeature();

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(request);
        features.Set<IHttpResponseFeature>(response);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(responseBody));
        features.Set<IHttpRequestLifetimeFeature>(new HttpRequestLifetimeFeature { RequestAborted = cancellationToken });
        features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature());
        // The inner request shares the caller's connection, so rate limits and logs see the same address and id.
        if (connection is not null)
        {
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                ConnectionId = connection.ConnectionId,
                RemoteIpAddress = connection.RemoteIpAddress,
                RemotePort = connection.RemotePort,
                LocalIpAddress = connection.LocalIpAddress,
                LocalPort = connection.LocalPort,
            });
        }
        if (delegation is not null)
            features.Set(delegation);

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
