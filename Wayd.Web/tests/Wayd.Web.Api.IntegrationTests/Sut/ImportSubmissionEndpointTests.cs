using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves an import submission's response contract over HTTP: 200 with the run once it has finished, or 202
/// with the run and a <c>Location</c> to follow when it is still going once the wait runs out.
/// </summary>
/// <remarks>
/// Every import endpoint answers through the same responder (a unit test holds each one to that), so the
/// strategic theme import stands in for all of them. It needs no other records to exist.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ImportSubmissionEndpointTests(WaydSqlServerApiFactory factory)
{
    private const string ImportRoute = "/api/strategic-management/strategic-themes/import";

    private static readonly string _importPermission =
        ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.StrategicThemes);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static MultipartFormDataContent ThemeFile()
    {
        var name = $"Theme {Guid.NewGuid():N}"[..24];
        var csv = $"ImportId,Name,Description,State\nt1,{name},Submitted by the import endpoint test.,Active\n";

        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        return new MultipartFormDataContent { { file, "file", "themes.csv" } };
    }

    private static async Task<JsonElement> ReadRun(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken)).RootElement;

    private static string StatusOf(JsonElement run) => run.GetProperty("status").GetString()!;

    [Fact]
    public async Task Import_ReturnsTheFinishedRun_WhenItCompletesWithinTheWait()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient(_importPermission);

        // Act
        var response = await user.Client.PostAsync(ImportRoute, ThemeFile(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var run = await ReadRun(response);
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), StatusOf(run));
        Assert.True(run.GetProperty("isTerminal").GetBoolean());
        Assert.Equal(1, run.GetProperty("totalRowCount").GetInt32());
        Assert.Equal(1, run.GetProperty("succeededRowCount").GetInt32());
        Assert.Equal(user.UserId, run.GetProperty("submittedByUserId").GetString());
    }

    [Fact]
    public async Task Import_ReturnsAcceptedWithLocation_WhenTheRunOutlastsTheWait()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient(_importPermission);
        HttpResponseMessage response;
        JsonElement accepted;

        // Act — no worker can claim the run until the gate opens, so the wait must run out
        using (_factory.WaitOnImportsFor(TimeSpan.FromSeconds(1)))
        using (_factory.ImportClaims.Hold())
        {
            response = await user.Client.PostAsync(ImportRoute, ThemeFile(), TestContext.Current.CancellationToken);
            accepted = await ReadRun(response);
        }

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(nameof(ImportProcessStatus.Queued), StatusOf(accepted));
        Assert.False(accepted.GetProperty("isTerminal").GetBoolean());

        var id = accepted.GetProperty("id").GetGuid();
        Assert.Equal($"/api/imports/{id}", response.Headers.Location?.OriginalString);

        // Following the Location reaches the same run, which finishes now the gate is open
        var elapsed = Stopwatch.StartNew();
        JsonElement followed;
        do
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            var poll = await user.Client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
            followed = await ReadRun(poll);
        }
        while (!followed.GetProperty("isTerminal").GetBoolean() && elapsed.Elapsed < TimeSpan.FromSeconds(30));

        Assert.Equal(id, followed.GetProperty("id").GetGuid());
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), StatusOf(followed));
    }

    [Fact]
    public async Task Import_ReturnsForbidden_WhenTheCallerLacksTheImportPermission()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient(
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.StrategicThemes));

        // Act
        var response = await user.Client.PostAsync(ImportRoute, ThemeFile(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Import_ReturnsUnauthorized_WhenTheCallerIsAnonymous()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(ImportRoute, ThemeFile(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
