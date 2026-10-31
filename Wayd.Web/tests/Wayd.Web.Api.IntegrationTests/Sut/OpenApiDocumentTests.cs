using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Wayd.Infrastructure.OpenApi;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Holds the OpenAPI document the API serves to the two other places it is read from: the committed
/// <c>specification.json</c>, which the TypeScript and data-generation clients and the import templates are
/// generated from, and the document provider the hosted MCP server builds its tools from.
/// </summary>
[Collection(InMemoryApiTestCollection.Name)]
public sealed class OpenApiDocumentTests(WaydApiFactory factory)
{
    private const string DocumentPath = "/openapi/" + WaydOpenApiDocument.Name + ".json";

    private readonly WaydApiFactory _factory = factory;

    [Fact]
    public async Task CommittedSpecification_MatchesTheServedDocument()
    {
        // Arrange
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenApi", "specification.json"), TestContext.Current.CancellationToken))!.AsObject();

        // Act
        var served = await ServedDocument();

        // Assert
        Assert.True(
            JsonNode.DeepEquals(committed, served),
            $"specification.json is out of date at {FirstDifference(committed, served, "$")}: rebuild Wayd.Web.Api in Debug and commit the regenerated specification.json and wayd-api.ts.");
    }

    [Fact]
    public async Task DocumentProvider_GivesTheServedDocument()
    {
        // Arrange
        var served = await ServedDocument();
        var provider = _factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(WaydOpenApiDocument.Name);

        // Act
        var document = await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
        var json = JsonNode.Parse(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0, TestContext.Current.CancellationToken))!.AsObject();

        // Assert
        Assert.True(JsonNode.DeepEquals(served, json), $"The document provider and the endpoint describe the API differently at {FirstDifference(served, json, "$")}.");
    }

    /// <summary>The JSON path of the first place two documents differ, or null when they do not.</summary>
    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path)
    {
        switch (expected, actual)
        {
            case (JsonObject left, JsonObject right):
                foreach (var key in left.Select(p => p.Key).Union(right.Select(p => p.Key)))
                {
                    if (FirstDifference(left[key], right[key], $"{path}['{key}']") is { } found)
                        return found;
                }

                return null;
            case (JsonArray left, JsonArray right):
                for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
                {
                    if (FirstDifference(i < left.Count ? left[i] : null, i < right.Count ? right[i] : null, $"{path}[{i}]") is { } found)
                        return found;
                }

                return null;
            default:
                return JsonNode.DeepEquals(expected, actual) ? null : path;
        }
    }

    /// <summary>The served document, less the server the request reached, which only a served copy names.</summary>
    private async Task<JsonObject> ServedDocument()
    {
        using var client = _factory.CreateClient();
        var document = JsonNode.Parse(await client.GetStringAsync(DocumentPath, TestContext.Current.CancellationToken))!.AsObject();
        document.Remove("servers");
        return document;
    }
}
