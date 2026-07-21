using System.Net;
using System.Text.Json;
using Wayd.Integrations.AzureDevOps.Clients;
using Wayd.Integrations.AzureDevOps.Tests.Support;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Clients;

public class WorkItemClientTests
{
    private const string OrganizationUrl = "https://dev.azure.com/acme";
    private const string Token = "test-pat-token";
    private const string ApiVersion = "7.0";
    private const string ProjectName = "Atlas";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly WorkItemClient _sut;

    public WorkItemClientTests()
    {
        _sut = new WorkItemClient(new HttpClient(_handler), OrganizationUrl, Token, ApiVersion);
    }

    [Fact]
    public async Task GetWorkItems_SendsOmitErrorPolicyInBatchRequest()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, WorkItemsJson(101));

        // Act
        await _sut.GetWorkItems(ProjectName, [101], ["System.Title"], TestContext.Current.CancellationToken);

        // Assert
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Body.Should().NotBeNull();
        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetProperty("errorPolicy").GetString().Should().Be("omit");
    }

    [Fact]
    public async Task GetWorkItems_WithEmptyBatchResponse_ReturnsEmptyList()
    {
        // Arrange - errorPolicy=omit returns an empty batch when every requested id was deleted
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":0,"value":[]}""");

        // Act
        var result = await _sut.GetWorkItems(ProjectName, [101, 102], ["System.Title"], TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkItems_WithMoreThanBatchSizeIds_SplitsIntoBatchedRequests()
    {
        // Arrange - 250 distinct ids should produce two batches (200 + 50)
        var workItemIds = Enumerable.Range(1, 250).ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, WorkItemsJson(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, WorkItemsJson(201));

        // Act
        var result = await _sut.GetWorkItems(ProjectName, workItemIds, ["System.Title"], TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        _handler.Requests.Should().HaveCount(2);
        CountIdsInBody(_handler.Requests[0].Body!).Should().Be(200);
        CountIdsInBody(_handler.Requests[1].Body!).Should().Be(50);
    }

    [Fact]
    public async Task GetWorkItems_WithFailedResponse_ThrowsWithStatusAndBodyDetail()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.BadRequest, """{"message":"bad request"}""");

        // Act
        var act = () => _sut.GetWorkItems(ProjectName, [101], ["System.Title"], TestContext.Current.CancellationToken);

        // Assert - RestSharp leaves ErrorMessage null on HTTP failures; the status code and the
        // response body must still surface in the thrown message
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"*{ProjectName}*400*bad request*");
    }

    [Fact]
    public async Task GetWorkItemIds_EscapesSingleQuotesInWiqlLiterals()
    {
        // Arrange
        var quotedProject = "O'Brien Project";
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"queryType":"flat","queryResultType":"workItem","workItems":[]}""");

        // Act
        await _sut.GetWorkItemIds(quotedProject, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ["Bug's Type"], excludeWorkItemTypes: false, TestContext.Current.CancellationToken);

        // Assert
        var request = _handler.Requests.Should().ContainSingle().Subject;
        using var body = JsonDocument.Parse(request.Body!);
        var query = body.RootElement.GetProperty("query").GetString()!;
        query.Should().Contain("[System.TeamProject] = 'O''Brien Project'");
        query.Should().Contain("[System.WorkItemType] IN ('Bug''s Type')");
    }

    [Fact]
    public async Task GetWorkItems_WithNoIds_ReturnsEmptyListWithoutRequest()
    {
        // Arrange

        // Act
        var result = await _sut.GetWorkItems(ProjectName, [], ["System.Title"], TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkItemIds_WithFullPage_PagesFromLastIdUntilShortPage()
    {
        // Arrange - a page of exactly 10,000 ids (the client's page size) forces a second query
        var firstPageIds = Enumerable.Range(1, 10_000).ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson(firstPageIds));
        _handler.EnqueueResponse(HttpStatusCode.OK, WiqlIdsJson([10_001, 10_002]));

        // Act
        var result = await _sut.GetWorkItemIds(ProjectName, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), [], excludeWorkItemTypes: false, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(10_002);
        _handler.Requests.Should().HaveCount(2);
        GetWiqlQuery(_handler.Requests[0].Body!).Should().Contain("[System.Id] > 0");
        GetWiqlQuery(_handler.Requests[1].Body!).Should().Contain("[System.Id] > 10000");
    }

    [Fact]
    public async Task GetWorkItemLinkChanges_FollowsContinuationTokenUntilLastBatch()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, LinkBatchJson(sourceId: 1, isLastBatch: false, continuationToken: "watermark-1"));
        _handler.EnqueueResponse(HttpStatusCode.OK, LinkBatchJson(sourceId: 2, isLastBatch: true, continuationToken: "watermark-2"));

        // Act
        var result = await _sut.GetWorkItemLinkChanges(ProjectName, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ["System.LinkTypes.Hierarchy"], [], TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        _handler.Requests.Should().HaveCount(2);
        _handler.Requests[0].Uri!.Query.Should().NotContain("continuationToken");
        _handler.Requests[1].Uri!.Query.Should().Contain("continuationToken=watermark-1");
    }

    [Fact]
    public async Task GetWorkItemLinkChanges_WithStalledContinuationToken_ThrowsInsteadOfLooping()
    {
        // Arrange - a non-final batch that repeats the same token would otherwise page forever
        _handler.EnqueueResponse(HttpStatusCode.OK, LinkBatchJson(sourceId: 1, isLastBatch: false, continuationToken: "watermark-1"));
        _handler.EnqueueResponse(HttpStatusCode.OK, LinkBatchJson(sourceId: 2, isLastBatch: false, continuationToken: "watermark-1"));

        // Act
        var act = () => _sut.GetWorkItemLinkChanges(ProjectName, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ["System.LinkTypes.Hierarchy"], [], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*continuation token did not advance*");
        _handler.Requests.Should().HaveCount(2);
    }

    private static int CountIdsInBody(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("ids").GetArrayLength();
    }

    private static string GetWiqlQuery(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        return document.RootElement.GetProperty("query").GetString()!;
    }

    private static string WiqlIdsJson(int[] ids)
    {
        var items = string.Join(",", ids.Select(id => $$"""{"id":{{id}}}"""));
        return $$"""{"queryType":"flat","queryResultType":"workItem","workItems":[{{items}}]}""";
    }

    private static string LinkBatchJson(int sourceId, bool isLastBatch, string continuationToken)
    {
        return $$"""
            {
                "values": [
                    {
                        "rel": "System.LinkTypes.Hierarchy",
                        "attributes": {
                            "sourceId": {{sourceId}},
                            "targetId": {{sourceId + 100}},
                            "isActive": true,
                            "changedDate": "2026-01-02T10:00:00Z",
                            "changedBy": { "uniqueName": "dev@acme.example" },
                            "comment": null,
                            "changedOperation": "create",
                            "sourceProjectId": "6ff2ee2f-9d9b-40b1-9502-e4c00a318c00",
                            "targetProjectId": "6ff2ee2f-9d9b-40b1-9502-e4c00a318c00"
                        }
                    }
                ],
                "isLastBatch": {{(isLastBatch ? "true" : "false")}},
                "continuationToken": "{{continuationToken}}",
                "nextLink": "https://dev.azure.com/acme/next"
            }
            """;
    }

    private static string WorkItemsJson(int id)
    {
        return $$"""
            {
                "count": 1,
                "value": [
                    {
                        "id": {{id}},
                        "rev": 1,
                        "fields": {
                            "System.Title": "Sample work item",
                            "System.WorkItemType": "User Story",
                            "System.State": "Active",
                            "System.CreatedDate": "2026-01-05T10:00:00Z",
                            "System.CreatedBy": { "uniqueName": "dev@acme.example" },
                            "System.ChangedDate": "2026-01-06T10:00:00Z",
                            "System.ChangedBy": { "uniqueName": "dev@acme.example" },
                            "System.IterationId": 5
                        }
                    }
                ]
            }
            """;
    }
}
