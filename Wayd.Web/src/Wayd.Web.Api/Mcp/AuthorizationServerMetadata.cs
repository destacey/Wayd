using System.Text.Json.Serialization;
using Wayd.Infrastructure.Hosting;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Publishes the metadata of Wayd's OAuth authorization server (RFC 8414), the one <see cref="McpAuthentication"/>'s
/// protected resource metadata names, so an MCP client can find where to send the user and where to get a token.
/// </summary>
public static class AuthorizationServerMetadata
{
    /// <summary>
    /// The path the metadata is served at. The issuer is the API's <see cref="PublicUrl"/>, an origin with no path,
    /// so RFC 8414 puts it at the root with nothing appended.
    /// </summary>
    public const string Path = "/.well-known/oauth-authorization-server";

    /// <summary>The path prefix every authorization server endpoint sits under.</summary>
    public const string EndpointsPath = "/oauth";

    /// <summary>The authorization endpoint, where a client sends the user to sign in and consent.</summary>
    public const string AuthorizePath = EndpointsPath + "/authorize";

    /// <summary>The token endpoint, where a client exchanges an authorization code or a refresh token.</summary>
    public const string TokenPath = EndpointsPath + "/token";

    /// <summary>Serves the metadata at <see cref="Path"/> to anyone, outside the OpenAPI document and so outside the MCP tools.</summary>
    public static WebApplication MapWaydAuthorizationServerMetadata(this WebApplication app)
    {
        app.MapGet(Path, (HttpContext context, PublicUrl publicUrl) => Results.Json(Describe(publicUrl.For(context.Request))))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// Describes the server whose issuer is <paramref name="issuer"/>: the authorization code grant for public
    /// clients only, with PKCE by S256, clients identified by their Client ID Metadata Documents, and <c>iss</c> in
    /// every authorization response.
    /// </summary>
    internal static Document Describe(string issuer) => new(
        Issuer: issuer,
        AuthorizationEndpoint: issuer + AuthorizePath,
        TokenEndpoint: issuer + TokenPath,
        ResponseTypesSupported: ["code"],
        GrantTypesSupported: ["authorization_code", "refresh_token"],
        CodeChallengeMethodsSupported: ["S256"],
        TokenEndpointAuthMethodsSupported: ["none"],
        ClientIdMetadataDocumentSupported: true,
        AuthorizationResponseIssParameterSupported: true);

    /// <summary>The metadata document, in RFC 8414's member names.</summary>
    /// <param name="Issuer">The authorization server's identifier, which every token it issues carries as <c>iss</c>.</param>
    /// <param name="AuthorizationEndpoint">Where a client sends the user.</param>
    /// <param name="TokenEndpoint">Where a client gets tokens.</param>
    /// <param name="ResponseTypesSupported">Only <c>code</c>: the implicit flow is not offered.</param>
    /// <param name="GrantTypesSupported">The authorization code and refresh token grants.</param>
    /// <param name="CodeChallengeMethodsSupported">Only <c>S256</c>; a request without PKCE, or with <c>plain</c>, is refused.</param>
    /// <param name="TokenEndpointAuthMethodsSupported">Only <c>none</c>: clients are public and prove themselves with PKCE.</param>
    /// <param name="ClientIdMetadataDocumentSupported">That a <c>client_id</c> may be the URL of the client's metadata document.</param>
    /// <param name="AuthorizationResponseIssParameterSupported">That every authorization response carries <c>iss</c> (RFC 9207), so a client can tell which server answered.</param>
    internal sealed record Document(
        [property: JsonPropertyName("issuer")] string Issuer,
        [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
        [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
        [property: JsonPropertyName("response_types_supported")] IReadOnlyList<string> ResponseTypesSupported,
        [property: JsonPropertyName("grant_types_supported")] IReadOnlyList<string> GrantTypesSupported,
        [property: JsonPropertyName("code_challenge_methods_supported")] IReadOnlyList<string> CodeChallengeMethodsSupported,
        [property: JsonPropertyName("token_endpoint_auth_methods_supported")] IReadOnlyList<string> TokenEndpointAuthMethodsSupported,
        [property: JsonPropertyName("client_id_metadata_document_supported")] bool ClientIdMetadataDocumentSupported,
        [property: JsonPropertyName("authorization_response_iss_parameter_supported")] bool AuthorizationResponseIssParameterSupported);
}
