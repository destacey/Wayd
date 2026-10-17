using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Wayd.Common.Domain.Authorization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Holds each JSON Patch endpoint to binding a patch sent with the content type <c>specification.json</c> lists
/// for it, which is the one the generated clients send.
/// </summary>
/// <remarks>
/// A <c>JsonPatchDocument&lt;T&gt;</c> parameter narrows what its action accepts, which the generator does not
/// see, so the document can list a content type the endpoint answers 415. The endpoints are read from the
/// document, so a new patch endpoint is covered without being added here. Each request names a record that does
/// not exist, so an action that bound the patch answers 404 from its lookup.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed partial class JsonPatchBindingTests(WaydSqlServerApiFactory factory)
{
    private const string Patch = """[{ "op": "replace", "path": "/Name", "value": "Renamed" }]""";

    private readonly WaydSqlServerApiFactory _factory = factory;

    public static TheoryData<string, string> PatchEndpoints => new(JsonPatchOperations());

    public static TheoryData<string> PatchRoutes => new(JsonPatchOperations().Select(o => o.Route));

    [Fact]
    public void Specification_DescribesJsonPatchEndpoints()
    {
        // Arrange / Act
        var endpoints = JsonPatchOperations();

        // Assert — guards the theories below against passing vacuously
        Assert.NotEmpty(endpoints);
    }

    [Theory]
    [MemberData(nameof(PatchEndpoints))]
    public async Task Patch_BindsAPatchSentAsTheDocumentDescribes(string route, string contentType)
    {
        // Arrange
        var user = await PatchingUser();

        // Act
        var response = await user.Client.PatchAsync(
            route, new StringContent(Patch, Encoding.UTF8, contentType), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PatchRoutes))]
    public async Task Patch_RejectsAPatchSentAsPlainText(string route)
    {
        // Arrange
        var user = await PatchingUser();

        // Act
        var response = await user.Client.PatchAsync(
            route, new StringContent(Patch, Encoding.UTF8, "text/plain"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    /// <summary>
    /// Every PATCH operation whose body is a JSON Patch document, as a route naming fresh ids and the one content
    /// type the document lists for its body.
    /// </summary>
    private static (string Route, string ContentType)[] JsonPatchOperations()
    {
        var specification = JsonNode.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "OpenApi", "specification.json")))!;

        return
        [
            .. specification["paths"]!.AsObject()
                .Where(path => path.Value!["patch"]?["requestBody"]?["content"] is JsonObject content
                    && content.Single().Value!["schema"]?["$ref"]?.GetValue<string>().Contains("/JsonPatchDocumentOf", StringComparison.Ordinal) == true)
                .Select(path => (
                    RouteParameter().Replace(path.Key, _ => Guid.NewGuid().ToString()),
                    path.Value!["patch"]!["requestBody"]!["content"]!.AsObject().Single().Key)),
        ];
    }

    private Task<AuthenticatedClient> PatchingUser() =>
        _factory.CreateAuthenticatedClient(
            ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Projects),
            ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Roadmaps));

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}
