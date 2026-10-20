using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Wayd.Infrastructure;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Authenticates callers of <see cref="McpServerSetup.Path"/> with the API's own credentials, and refuses an
/// unauthenticated one the way the MCP authorization spec expects: a 401 whose <c>WWW-Authenticate</c> header
/// points at the endpoint's protected resource metadata (RFC 9728), which names Wayd as the authorization server.
/// </summary>
public static class McpAuthentication
{
    /// <summary>
    /// The scheme the endpoint's policy names. It authenticates with the scheme the API would choose for the
    /// same request and challenges with the MCP scheme, so a refusal carries exactly one <c>WWW-Authenticate</c>
    /// header: naming the credential schemes in the policy would challenge with each of them.
    /// </summary>
    public const string Scheme = "Mcp";

    /// <summary>The path the endpoint's protected resource metadata is served at, which RFC 9728 derives from the endpoint's own.</summary>
    public const string ResourceMetadataPath = "/.well-known/oauth-protected-resource" + McpServerSetup.Path;

    /// <summary>The policy the endpoint requires: an authenticated caller, through <see cref="Scheme"/>.</summary>
    public static AuthorizationPolicy Policy { get; } = new AuthorizationPolicyBuilder(Scheme)
        .RequireAuthenticatedUser()
        .Build();

    /// <summary>Registers <see cref="Scheme"/> and the MCP scheme that serves the metadata and the challenge.</summary>
    public static IServiceCollection AddWaydMcpAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<McpAuthenticationOptions, ChallengeHandler>(
                McpAuthenticationDefaults.AuthenticationScheme,
                McpAuthenticationDefaults.DisplayName,
                options =>
                {
                    // A fixed path, rather than the SDK's default of one per request path, so that /mcp/ and /MCP
                    // are pointed at the same document, and no other path under the prefix is answered.
                    options.ResourceMetadataUri = new Uri(ResourceMetadataPath, UriKind.Relative);
                    options.Events.OnResourceMetadataRequest = DescribeResource;
                })
            .AddPolicyScheme(Scheme, "MCP", options =>
            {
                // Must choose as PermissionPolicyProvider's default policy does, or /mcp and the endpoints its
                // tools call would accept different credentials.
                options.ForwardDefaultSelector = context => context.Request.Headers.ContainsKey(AuthConstants.ApiKeyHeaderName)
                    ? AuthConstants.PersonalAccessTokenScheme
                    : AuthConstants.LocalJwtScheme;
                options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme;
            });

        return services;
    }

    /// <summary>Describes the endpoint, with Wayd's own origin as its authorization server.</summary>
    /// <remarks>
    /// URLs come from the request, so behind a proxy that terminates TLS they are only as right as the forwarded
    /// headers the host has applied.
    /// </remarks>
    private static Task DescribeResource(ResourceMetadataRequestContext context)
    {
        var request = context.HttpContext.Request;
        var authorizationServer = $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase}";
        context.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = authorizationServer + McpServerSetup.Path,
            AuthorizationServers = [authorizationServer],
            BearerMethodsSupported = ["header"],
            ResourceName = "Wayd",
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// The SDK's MCP handler, adding <c>error="invalid_token"</c> to the challenge when the request carried a
    /// bearer token that was refused, as RFC 6750 asks, so a client knows to refresh or reauthorize rather than
    /// start discovery. A refused personal access token gets the plain challenge: it was not a bearer token.
    /// </summary>
    private sealed class ChallengeHandler(IOptionsMonitor<McpAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : McpAuthenticationHandler(options, logger, encoder)
    {
        /// <inheritdoc />
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            await base.HandleChallengeAsync(properties);

            var authorization = Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return;

            var challenges = Response.Headers.WWWAuthenticate.ToArray();
            challenges[^1] += ", error=\"invalid_token\"";
            Response.Headers[HeaderNames.WWWAuthenticate] = challenges;
        }
    }
}
