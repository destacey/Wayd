using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Wayd.Infrastructure.OpenApi;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Holds the API catalog (RFC 9727) and <c>/openapi.json</c> to leading a tool that knows neither path in
/// advance to the OpenAPI document and the API reference.
/// </summary>
[Collection(InMemoryApiTestCollection.Name)]
public sealed class ApiCatalogTests(WaydApiFactory factory)
{
    private readonly WaydApiFactory _factory = factory;

    [Fact]
    public async Task Catalog_LinksTheDocumentAndTheReference()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/.well-known/api-catalog", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/linkset+json");
        var link = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["linkset"]![0]!;
        ((string)link["service-desc"]![0]!["href"]!).Should().EndWith("/openapi/v1.json");
        ((string)link["service-doc"]![0]!["href"]!).Should().EndWith("/scalar");

        using var document = await client.GetAsync(new Uri((string)link["service-desc"]![0]!["href"]!).PathAndQuery, TestContext.Current.CancellationToken);
        document.StatusCode.Should().Be(HttpStatusCode.OK, "the catalog must point at the document the API serves");
    }

    [Fact]
    public async Task Catalog_NamesItselfInALinkHeaderForAHeadRequest()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, "/.well-known/api-catalog");

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Link").Should().ContainSingle().Which.Should().EndWith("/.well-known/api-catalog>; rel=\"api-catalog\"");
    }

    [Fact]
    public async Task ConventionalDocumentPath_RedirectsToTheDocument()
    {
        // Arrange
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        using var response = await client.GetAsync("/openapi.json", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be(WaydOpenApiDocument.DocumentPath);
    }
}
