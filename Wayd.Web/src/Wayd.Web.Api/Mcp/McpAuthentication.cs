using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth.OAuth;
using Wayd.Infrastructure.Hosting;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Authenticates callers of <see cref="McpServerSetup.Path"/> with the API's own credentials or an access token from
/// Wayd's authorization server (<see cref="AuthorizationServerEndpoints"/>), and refuses an
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
                // Chooses as PermissionPolicyProvider's default policy does, plus the authorization server's access
                // tokens, which only this endpoint accepts. A tool call made with one reaches the endpoint it calls
                // with a delegated token ApiPipeline mints in its place.
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(AuthConstants.ApiKeyHeaderName) ? AuthConstants.PersonalAccessTokenScheme
                    : OAuthAccessTokens.IsAccessToken(context.Request.Headers.Authorization) ? AuthConstants.OAuthAccessTokenScheme
                    : AuthConstants.LocalJwtScheme;
                options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme;
            });

        services.Configure<AuthorizationServerOptions>(options => options.ResourcePath = McpServerSetup.Path);

        return services;
    }

    /// <summary>Describes the endpoint, with the API's <see cref="PublicUrl"/> as its authorization server.</summary>
    private static Task DescribeResource(ResourceMetadataRequestContext context)
    {
        var authorizationServer = context.HttpContext.RequestServices.GetRequiredService<PublicUrl>().For(context.HttpContext.Request);
        context.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = authorizationServer + McpServerSetup.Path,
            AuthorizationServers = [authorizationServer],
            BearerMethodsSupported = ["header"],
            ScopesSupported = [.. TokenScopes.Supported],
            ResourceName = "Wayd",
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// The SDK's MCP handler, with the challenge rewritten to name the metadata at the API's
    /// <see cref="PublicUrl"/> rather than at the URL the request arrived at. It adds <c>error="invalid_token"</c>
    /// when the request carried a bearer token that was refused, as RFC 6750 asks, so a client knows to refresh
    /// or reauthorize rather than start discovery; a refused personal access token was not a bearer token.
    /// </summary>
    private sealed class ChallengeHandler(
        IOptionsMonitor<McpAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        PublicUrl publicUrl)
        : McpAuthenticationHandler(options, logger, encoder)
    {
        private readonly PublicUrl _publicUrl = publicUrl;

        /// <inheritdoc />
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            await base.HandleChallengeAsync(properties);

            var challenge = $"Bearer resource_metadata=\"{_publicUrl.For(Request)}{ResourceMetadataPath}\"";
            if (Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                challenge += ", error=\"invalid_token\"";

            // Replaces rather than appends: the SDK's own value names the request's URL. Any other scheme's
            // challenge would be lost too, which holds only while the Mcp policy scheme forwards every
            // challenge here.
            Response.Headers[HeaderNames.WWWAuthenticate] = challenge;
        }
    }
}
