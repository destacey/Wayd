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
