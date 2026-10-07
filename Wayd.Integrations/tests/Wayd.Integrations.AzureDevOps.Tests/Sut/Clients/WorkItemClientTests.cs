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

    [Fact]
    public async Task GetWorkItemRevisions_WithNoContinuationToken_RequestsFieldsTypesAndIdentityRefsFromTheStart()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, RevisionBatchJson(isLastBatch: true, continuationToken: "watermark-1"));

        // Act
        await _sut.GetWorkItemRevisions(ProjectName, null, ["System.Id", "System.State"], ["User Story", "Bug"], TestContext.Current.CancellationToken);

        // Assert
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Uri!.AbsolutePath.Should().Be($"/acme/{ProjectName}/_apis/wit/reporting/workitemrevisions");
        var query = Uri.UnescapeDataString(request.Uri.Query);
        query.Should().Contain("fields=System.Id,System.State");
        query.Should().Contain("types=User Story,Bug");
        query.Should().Contain("includeIdentityRef=true");
        query.Should().NotContain("continuationToken");
    }

    [Fact]
    public async Task GetWorkItemRevisions_WithContinuationToken_ResumesFromIt()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, RevisionBatchJson(isLastBatch: false, continuationToken: "watermark-2"));

        // Act
        await _sut.GetWorkItemRevisions(ProjectName, "watermark-1", ["System.Id"], [], TestContext.Current.CancellationToken);

        // Assert
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Uri!.Query.Should().Contain("continuationToken=watermark-1");
        request.Uri.Query.Should().NotContain("types=");
    }

    [Fact]
    public async Task GetWorkItemRevisions_ReturnsOnePageWithItsTokenAndRevisionFields()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, RevisionBatchJson(isLastBatch: false, continuationToken: "watermark-2"));

        // Act
        var result = await _sut.GetWorkItemRevisions(ProjectName, "watermark-1", ["System.Id"], [], TestContext.Current.CancellationToken);

        // Assert
        result.IsLastBatch.Should().BeFalse();
        result.ContinuationToken.Should().Be("watermark-2");
        var revision = result.Values.Should().ContainSingle().Subject;
        revision.Id.Should().Be(101);
        revision.Rev.Should().Be(3);
        revision.Fields!.ChangedDate.Should().Be(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));
        revision.Fields.WorkItemType.Should().Be("User Story");
        revision.Fields.State.Should().Be("Active");
        revision.Fields.IterationId.Should().Be(5);
        revision.Fields.AssignedTo!.Id.Should().Be("8c8c7d32-6b1b-47f4-b2e9-30b477b5ab3d");
        revision.Fields.StoryPoints.Should().Be(3);
        revision.Fields.Effort.Should().BeNull();
        _handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetWorkItemRevisions_WithFailedResponse_ThrowsWithStatusAndBodyDetail()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.BadRequest, """{"message":"bad continuation token"}""");

        // Act
        var act = () => _sut.GetWorkItemRevisions(ProjectName, "watermark-1", ["System.Id"], [], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"*revisions*{ProjectName}*400*bad continuation token*");
    }

    [Fact]
    public async Task GetRevisionsOfWorkItem_PagesUntilAShortPage()
    {
        // Arrange - a full page of 200 forces a second request
        _handler.EnqueueResponse(HttpStatusCode.OK, RevisionListJson(1, 200));
        _handler.EnqueueResponse(HttpStatusCode.OK, RevisionListJson(201, 3));

        // Act
        var result = await _sut.GetRevisionsOfWorkItem(101, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(203);
        _handler.Requests.Should().HaveCount(2);
        _handler.Requests[0].Uri!.AbsolutePath.Should().Be("/acme/_apis/wit/workItems/101/revisions");
        _handler.Requests[0].Uri!.Query.Should().Contain("%24skip=0");
        _handler.Requests[1].Uri!.Query.Should().Contain("%24skip=200");
    }

    [Fact]
    public async Task GetRevisionsOfWorkItem_ForAnItemThatNoLongerExists_ReturnsNone()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.NotFound, """{"message":"TF401232: Work item 101 does not exist"}""");

        // Act
        var result = await _sut.GetRevisionsOfWorkItem(101, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    private static string RevisionListJson(int firstRevision, int count)
    {
        var items = string.Join(",", Enumerable.Range(firstRevision, count).Select(rev => $$$"""
            {"id":101,"rev":{{{rev}}},"fields":{"System.ChangedDate":"2026-01-02T10:00:00Z","System.WorkItemType":"User Story","System.State":"Active"}}
            """));
        return $$"""{"count":{{count}},"value":[{{items}}]}""";
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

    private static string RevisionBatchJson(bool isLastBatch, string continuationToken)
    {
        return $$"""
            {
                "values": [
                    {
                        "id": 101,
                        "rev": 3,
                        "fields": {
                            "System.Id": 101,
                            "System.Rev": 3,
                            "System.ChangedDate": "2026-01-02T10:00:00Z",
                            "System.WorkItemType": "User Story",
                            "System.State": "Active",
                            "System.IterationId": 5,
                            "System.AssignedTo": {
                                "id": "8c8c7d32-6b1b-47f4-b2e9-30b477b5ab3d",
                                "displayName": "Dev One",
                                "uniqueName": "dev@acme.example"
                            },
                            "Microsoft.VSTS.Scheduling.StoryPoints": 3
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
