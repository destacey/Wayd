using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Integrations.AzureDevOps.Services;
using Wayd.Integrations.AzureDevOps.Tests.Support;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Services;

public class WorkItemServiceTests
{
    private const string OrganizationUrl = "https://dev.azure.com/acme";
    private const string Token = "test-pat-token";
    private const string ApiVersion = "7.0";
    private const string ProjectName = "Atlas";

    private static readonly DateTime _lastChangedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly StubHttpMessageHandler _handler = new();
    private readonly WorkItemService _sut;

    public WorkItemServiceTests()
    {
        _sut = new WorkItemService(new HttpClient(_handler), OrganizationUrl, Token, ApiVersion, NullLogger<WorkItemService>.Instance);
    }

    [Fact]
    public async Task GetDeletedWorkItemIds_CombinesRecycleBinAndTypeChangedIdsDistinct()
    {
        // Arrange - id 11 appears in both the recycle bin and the type-changed query
        _handler.EnqueueResponse(HttpStatusCode.OK, RecycleBinJson(10, 11));
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson(11, 12));

        // Act
        var result = await _sut.GetDeletedWorkItemIds(ProjectName, _lastChangedDate, ["Epic", "Feature"], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { 10, 11, 12 });
    }

    [Fact]
    public async Task GetDeletedWorkItemIds_QueriesTypeChangesWithNotInOverSyncedTypes()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, RecycleBinJson());
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson());

        // Act
        await _sut.GetDeletedWorkItemIds(ProjectName, _lastChangedDate, ["Epic", "Feature"], TestContext.Current.CancellationToken);

        // Assert
        _handler.Requests.Should().HaveCount(2);
        var wiqlQuery = GetWiqlQuery(_handler.Requests[1].Body!);
        wiqlQuery.Should().Contain("[System.WorkItemType] NOT IN ('Epic','Feature')");
    }

    [Fact]
    public async Task GetDeletedWorkItemIds_WithNoSyncedTypes_OnlyQueriesRecycleBin()
    {
        // Arrange - with no type filter every type is synced, so no type-changed query applies
        _handler.EnqueueResponse(HttpStatusCode.OK, RecycleBinJson(10));

        // Act
        var result = await _sut.GetDeletedWorkItemIds(ProjectName, _lastChangedDate, [], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { 10 });
        _handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetDeletedWorkItemIds_WithNoTypeChangeWindow_OnlyQueriesRecycleBin()
    {
        // Arrange - a full sync has no window, so it skips the type-changed query
        _handler.EnqueueResponse(HttpStatusCode.OK, RecycleBinJson(10));

        // Act
        var result = await _sut.GetDeletedWorkItemIds(ProjectName, null, ["Epic", "Feature"], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { 10 });
        _handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetWorkItems_WhenUnderlyingCallIsCancelled_PropagatesCancellationInsteadOfReturningFailure()
    {
        // Arrange - a genuine cancellation (caller's token fired) must not be laundered into an
        // ordinary Result.Failure; the caller's own cancellation handling depends on the exception
        // actually reaching it.
        _handler.ThrowOperationCanceledOnNextSend = true;

        // Act
        var act = () => _sut.GetWorkItems(ProjectName, _lastChangedDate, [], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetDeletedWorkItemIds_WhenRecycleBinFails_ReturnsFailureWithStatusDetail()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"message":"access denied"}""");

        // Act
        var result = await _sut.GetDeletedWorkItemIds(ProjectName, _lastChangedDate, ["Epic"], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("401");
    }

    [Fact]
    public async Task GetWorkItems_RequestsAllThreeEstimateFields()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson(101));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":0,"value":[]}""");

        // Act
        await _sut.GetWorkItems(ProjectName, _lastChangedDate, [], TestContext.Current.CancellationToken);

        // Assert
        using var document = JsonDocument.Parse(_handler.Requests[1].Body!);
        var fields = document.RootElement.GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        fields.Should().Contain([
            "Microsoft.VSTS.Scheduling.StoryPoints",
            "Microsoft.VSTS.Scheduling.Effort",
            "Microsoft.VSTS.Scheduling.Size"]);
    }

    [Fact]
    public async Task GetRevisionsOfWorkItems_WithAnItemThatFails_SkipsItAndReturnsTheRest()
    {
        // Arrange - the first item is forbidden; the second reads normally
        _handler.EnqueueResponse(HttpStatusCode.Forbidden, """{"message":"TF237111: access denied"}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """
            {"count":1,"value":[{"id":102,"rev":1,"fields":{"System.ChangedDate":"2026-01-02T10:00:00Z","System.WorkItemType":"Bug","System.State":"New"}}]}
            """);

        // Act
        var result = await _sut.GetRevisionsOfWorkItems([101, 102], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be(102);
    }

    [Fact]
    public async Task GetIdsOfWorkItemsChangedOutsideProjects_QueriesTheOrganizationExcludingEverySyncedProject()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson(7, 9));
        var changedSince = new DateTime(2026, 10, 31, 9, 15, 42, DateTimeKind.Utc);

        // Act
        var result = await _sut.GetIdsOfWorkItemsChangedOutsideProjects(["Atlas", "O'Brien Labs"], changedSince, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(7, 9);
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Uri!.AbsolutePath.Should().Be("/acme/_apis/wit/wiql");
        GetWiqlQuery(request.Body!).Should().Be(
            "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] NOT IN ('Atlas','O''Brien Labs') AND [System.Id] > 0 AND [System.ChangedDate] > '2026-10-31T09:15:42Z' ORDER BY [System.Id] Asc");
    }

    [Fact]
    public async Task GetIdsOfWorkItemsChangedOutsideProjects_ReadsEveryPage()
    {
        // Arrange - a full page of 10,000 ids means another page may follow
        var firstPage = Enumerable.Range(1, 10_000).ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson(firstPage));
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson(10_001));

        // Act
        var result = await _sut.GetIdsOfWorkItemsChangedOutsideProjects(["Atlas"], _lastChangedDate, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().HaveCount(10_001);
        GetWiqlQuery(_handler.Requests[1].Body!).Should().Contain("[System.Id] > 10000");
    }

    [Fact]
    public async Task GetIdsOfWorkItemsChangedOutsideProjects_WhenAzureDevOpsFails_ReturnsFailure()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.BadRequest, """{"message":"TF51005: The query references a field that does not exist."}""");

        // Act
        var result = await _sut.GetIdsOfWorkItemsChangedOutsideProjects(["Atlas"], _lastChangedDate, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetIdsOfWorkItemsOutsideProjects_ReturnsTheItemsInAnUnsyncedProject()
    {
        // Arrange - project names compare case-insensitively; id 3 no longer exists
        _handler.EnqueueResponse(HttpStatusCode.OK, ProjectsJson((1, "atlas"), (2, "Elsewhere")));

        // Act
        var result = await _sut.GetIdsOfWorkItemsOutsideProjects([1, 2, 3], ["Atlas"], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(2);
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Uri!.AbsolutePath.Should().Be("/acme/_apis/wit/workitemsbatch");
        request.Body.Should().Contain("System.TeamProject").And.Contain("\"omit\"");
    }

    [Fact]
    public async Task GetIdsOfWorkItemsOutsideProjects_Reads200ItemsPerCall()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, ProjectsJson());
        _handler.EnqueueResponse(HttpStatusCode.OK, ProjectsJson((201, "Elsewhere")));

        // Act
        var result = await _sut.GetIdsOfWorkItemsOutsideProjects([.. Enumerable.Range(1, 201)], ["Atlas"], TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Equal(201);
        _handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetIdsOfWorkItemsOutsideProjects_WithNoIds_CallsNothing()
    {
        // Act
        var result = await _sut.GetIdsOfWorkItemsOutsideProjects([], ["Atlas"], TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeEmpty();
        _handler.Requests.Should().BeEmpty();
    }

    private static string ProjectsJson(params (int Id, string Project)[] items)
    {
        var values = string.Join(",", items.Select(i => $$$"""{"id":{{{i.Id}}},"fields":{"System.Id":{{{i.Id}}},"System.TeamProject":"{{{i.Project}}}"}}"""));
        return $$"""{"count":{{items.Length}},"value":[{{values}}]}""";
    }

    private static string GetWiqlQuery(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        return document.RootElement.GetProperty("query").GetString()!;
    }

    private static string RecycleBinJson(params int[] ids)
    {
        var items = string.Join(",", ids.Select(id => $$"""{"id":{{id}}}"""));
        return $$"""{"count":{{ids.Length}},"value":[{{items}}]}""";
    }

    private static string WiqlIdsJson(params int[] ids)
    {
        var items = string.Join(",", ids.Select(id => $$"""{"id":{{id}}}"""));
        return $$"""{"queryType":"flat","queryResultType":"workItem","workItems":[{{items}}]}""";
    }
}
