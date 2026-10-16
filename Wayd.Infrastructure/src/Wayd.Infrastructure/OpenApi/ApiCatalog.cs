using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// The places a tool finds the API's description without knowing which documentation tool serves it: the
/// RFC 9727 API catalog at <c>/.well-known/api-catalog</c>, and <c>/openapi.json</c>, the path many tools try
/// first.
/// </summary>
/// <remarks>
/// The catalog is a linkset (RFC 9264) naming the API's base address, its OpenAPI document
/// (<c>service-desc</c>) and its human-readable reference (<c>service-doc</c>), the relations RFC 8631 defines.
/// Its links are absolute, built from the request, because a catalog is read from outside the application.
/// </remarks>
internal static class ApiCatalog
{
    /// <summary>The catalog's well-known path.</summary>
    public const string Path = "/.well-known/api-catalog";

    /// <summary>The path many tools look for an OpenAPI document at before reading any catalog.</summary>
    public const string ConventionalDocumentPath = "/openapi.json";

    /// <summary>The media type RFC 9727 gives a linkset catalog.</summary>
    public const string MediaType = "application/linkset+json; profile=\"https://www.rfc-editor.org/info/rfc9727\"";

    /// <summary>Writes the media types' <c>+</c> as itself; the default encoder escapes it for HTML.</summary>
    private static readonly JsonSerializerOptions _json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Maps the catalog and the conventional document path.</summary>
    public static IEndpointRouteBuilder MapApiCatalog(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(Path, [HttpMethods.Get, HttpMethods.Head], (HttpContext context) =>
        {
            var baseAddress = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";

            // RFC 9727 asks the catalog to identify itself in a Link header, which a HEAD request reads alone.
            context.Response.Headers.Link = $"<{baseAddress}{Path}>; rel=\"api-catalog\"";

            var catalog = new JsonObject
            {
                ["linkset"] = new JsonArray(new JsonObject
                {
                    ["anchor"] = $"{baseAddress}/",
                    ["service-desc"] = new JsonArray(new JsonObject
                    {
                        ["href"] = $"{baseAddress}{WaydOpenApiDocument.DocumentPath}",
                        ["type"] = "application/vnd.oai.openapi+json",
                    }),
                    ["service-doc"] = new JsonArray(new JsonObject
                    {
                        ["href"] = $"{baseAddress}{WaydOpenApiDocument.ReferencePath}",
                        ["type"] = "text/html",
                    }),
                }),
            };

            return Results.Text(catalog.ToJsonString(_json), MediaType);
        }).ExcludeFromDescription();

        endpoints.MapGet(ConventionalDocumentPath, () => Results.Redirect(WaydOpenApiDocument.DocumentPath))
            .ExcludeFromDescription();

        return endpoints;
    }
}
