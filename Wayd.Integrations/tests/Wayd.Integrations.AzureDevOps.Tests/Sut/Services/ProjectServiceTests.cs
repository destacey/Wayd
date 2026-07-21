using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Integrations.AzureDevOps.Models.Projects;
using Wayd.Integrations.AzureDevOps.Services;
using Wayd.Integrations.AzureDevOps.Tests.Models;
using Wayd.Integrations.AzureDevOps.Tests.Support;
using Wayd.Integrations.AzureDevOps.Utils;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Services;

public class ProjectServiceTests : CommonResponseOptions
{
    private const string OrganizationUrl = "https://dev.azure.com/acme";
    private const string Token = "test-pat-token";
    private const string ApiVersion = "7.0";

    [Fact]
    public async Task GetTeams_PagesThroughAllTeamsAndFetchesSettingsForEach()
    {
        // Arrange - 105 teams: a full page of 100 forces a second page request
        var handler = new StubHttpMessageHandler();
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);
        var projectId = Guid.NewGuid();

        handler.EnqueueResponse(HttpStatusCode.OK, TeamsPageJson(teamCount: 100));
        handler.EnqueueResponse(HttpStatusCode.OK, TeamsPageJson(teamCount: 5));
        for (var i = 0; i < 105; i++)
        {
            handler.EnqueueResponse(HttpStatusCode.OK, TeamSettingsJson());
        }

        // Act
        var result = await service.GetTeams([projectId], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(105);
        handler.Requests.Should().HaveCount(107); // 2 team pages + 105 settings lookups
        handler.Requests[1].Uri!.Query.Should().Contain("skip=100");
    }

    [Fact]
    public async Task GetTeams_WhenTeamsPageRequestIsCancelled_PropagatesCancellationInsteadOfReturningFailure()
    {
        // Arrange - a genuine cancellation must not be laundered into an ordinary Result.Failure
        var handler = new StubHttpMessageHandler { ThrowOperationCanceledOnNextSend = true };
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);
        var projectId = Guid.NewGuid();

        // Act
        var act = () => service.GetTeams([projectId], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetTeams_WhenTeamSettingsLookupIsCancelled_PropagatesCancellationInsteadOfReturningFailure()
    {
        // Arrange - the settings lookups run inside Parallel.ForEachAsync; a cancellation there
        // must still propagate out of GetTeams rather than being folded into a per-team skip.
        var handler = new StubHttpMessageHandler();
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);
        var projectId = Guid.NewGuid();

        handler.EnqueueResponse(HttpStatusCode.OK, TeamsPageJson(teamCount: 1));
        handler.ThrowOperationCanceledOnNextSend = true;

        // Act
        var act = () => service.GetTeams([projectId], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetTeams_WhenTeamSettingsLookupFails_SkipsThatTeamAndContinues()
    {
        // Arrange - one of the two settings lookups fails; only that team is dropped
        var handler = new StubHttpMessageHandler();
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);
        var projectId = Guid.NewGuid();

        handler.EnqueueResponse(HttpStatusCode.OK, TeamsPageJson(teamCount: 2));
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, """{"message":"server error"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, TeamSettingsJson());

        // Act
        var result = await service.GetTeams([projectId], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAreaPaths_WhenRequestFailsWithLongErrorBody_ReturnsTruncatedErrorText()
    {
        // Arrange - GetErrorText caps the response body at 500 chars so a large error page (e.g. an
        // HTML error document from a misconfigured proxy) doesn't blow up log lines or Result errors.
        // GetAreaPaths surfaces GetErrorText() directly in its Result, unlike GetTeams's fixed message,
        // making it the clearest place to pin the formatting contract end-to-end.
        var handler = new StubHttpMessageHandler();
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);
        var longBody = new string('x', 1000);

        handler.EnqueueResponse(HttpStatusCode.InternalServerError, longBody);

        // Act
        var result = await service.GetAreaPaths("Atlas", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("500");
        result.Error.Length.Should().BeLessThan(longBody.Length);
        result.Error.Should().EndWith("…");
    }

    [Fact]
    public async Task GetAreaPaths_WhenRequestFailsWithShortErrorBody_IncludesFullBodyUntruncated()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);

        handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"message":"TF400813: resource not authorized"}""");

        // Act
        var result = await service.GetAreaPaths("Atlas", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("401");
        result.Error.Should().Contain("TF400813: resource not authorized");
        result.Error.Should().NotContain("…");
    }

    [Fact]
    public async Task GetTeams_WhenTeamsRequestFails_ReturnsFailure()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var service = new ProjectService(new HttpClient(handler), OrganizationUrl, Token, ApiVersion, NullLogger<ProjectService>.Instance);
        var projectId = Guid.NewGuid();

        handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"message":"access denied"}""");

        // Act
        var result = await service.GetTeams([projectId], TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(projectId.ToString());
    }

    private static string TeamsPageJson(int teamCount)
    {
        var teams = string.Join(",", Enumerable.Range(1, teamCount)
            .Select(i => $$"""{"id":"{{Guid.NewGuid()}}","name":"Team {{i}}"}"""));
        return $$"""{"count":{{teamCount}},"value":[{{teams}}]}""";
    }

    private static string TeamSettingsJson()
    {
        return "{\"backlogIteration\":{\"id\":\"" + Guid.NewGuid() + "\"}}";
    }

    [Fact]
    public void FlattenIterationNode_WithSimpleStructure_ReturnsAllNodes()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var child1Id = Guid.NewGuid();
        var child2Id = Guid.NewGuid();

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = child1Id,
                    Name = "Child 1",
                    Path = "\\Root\\Iteration\\Child 1"
                },
                new IterationNodeResponse
                {
                    Id = 3,
                    Identifier = child2Id,
                    Name = "Child 2",
                    Path = "\\Root\\Iteration\\Child 2"
                }
            ]
        };

        var json = JsonSerializer.Serialize(root, _options);
        var deserialized = JsonSerializer.Deserialize<IterationNodeResponse>(json, _options);

        // Act - Use a helper method to test the flattening logic
        var result = FlattenIterationNode(deserialized!, []);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(i => i.Id == 1 && i.Name == "Root");
        result.Should().Contain(i => i.Id == 2 && i.Name == "Child 1");
        result.Should().Contain(i => i.Id == 3 && i.Name == "Child 2");
    }

    [Fact]
    public void FlattenIterationNode_WithMappedTeamId_AssignsTeamIdCorrectly()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children = []
        };

        var iterationTeamMappings = new Dictionary<Guid, Guid>
        {
            { rootId, teamId }
        };

        // Act
        var result = FlattenIterationNode(root, iterationTeamMappings);

        // Assert
        result.Should().ContainSingle();
        result[0].TeamId.Should().Be(teamId);
    }

    [Fact]
    public void FlattenIterationNode_WithUnmappedChildren_InheritsParentTeamId()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var child1Id = Guid.NewGuid();
        var child2Id = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = child1Id,
                    Name = "Child 1",
                    Path = "\\Root\\Iteration\\Child 1"
                },
                new IterationNodeResponse
                {
                    Id = 3,
                    Identifier = child2Id,
                    Name = "Child 2",
                    Path = "\\Root\\Iteration\\Child 2"
                }
            ]
        };

        var iterationTeamMappings = new Dictionary<Guid, Guid>
        {
            { rootId, teamId }
        };

        // Act
        var result = FlattenIterationNode(root, iterationTeamMappings);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(i => i.TeamId == teamId);
    }

    [Fact]
    public void FlattenIterationNode_WithChildHavingOwnMapping_OverridesParentTeamId()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var child1Id = Guid.NewGuid();
        var child2Id = Guid.NewGuid();
        var rootTeamId = Guid.NewGuid();
        var child2TeamId = Guid.NewGuid();

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = child1Id,
                    Name = "Child 1",
                    Path = "\\Root\\Iteration\\Child 1"
                },
                new IterationNodeResponse
                {
                    Id = 3,
                    Identifier = child2Id,
                    Name = "Child 2",
                    Path = "\\Root\\Iteration\\Child 2"
                }
            ]
        };

        var iterationTeamMappings = new Dictionary<Guid, Guid>
        {
            { rootId, rootTeamId },
            { child2Id, child2TeamId }
        };

        // Act
        var result = FlattenIterationNode(root, iterationTeamMappings);

        // Assert
        result.Should().HaveCount(3);
        result.Single(i => i.Id == 1).TeamId.Should().Be(rootTeamId);
        result.Single(i => i.Id == 2).TeamId.Should().Be(rootTeamId); // Child 1 inherits from root
        result.Single(i => i.Id == 3).TeamId.Should().Be(child2TeamId); // Child 2 has its own mapping
    }

    [Fact]
    public void FlattenIterationNode_WithDeepHierarchyAndMixedMappings_AssignsTeamIdsCorrectly()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var level1Id = Guid.NewGuid();
        var level2AId = Guid.NewGuid();
        var level2BId = Guid.NewGuid();
        var level3Id = Guid.NewGuid();

        var rootTeamId = Guid.NewGuid();
        var level2BTeamId = Guid.NewGuid();

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = level1Id,
                    Name = "Level 1",
                    Path = "\\Root\\Iteration\\Level 1",
                    Children =
                    [
                        new IterationNodeResponse
                        {
                            Id = 3,
                            Identifier = level2AId,
                            Name = "Level 2A",
                            Path = "\\Root\\Iteration\\Level 1\\Level 2A"
                        },
                        new IterationNodeResponse
                        {
                            Id = 4,
                            Identifier = level2BId,
                            Name = "Level 2B",
                            Path = "\\Root\\Iteration\\Level 1\\Level 2B",
                            Children =
                            [
                                new IterationNodeResponse
                                {
                                    Id = 5,
                                    Identifier = level3Id,
                                    Name = "Level 3",
                                    Path = "\\Root\\Iteration\\Level 1\\Level 2B\\Level 3"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var iterationTeamMappings = new Dictionary<Guid, Guid>
        {
            { rootId, rootTeamId },
            { level2BId, level2BTeamId }
        };

        // Act
        var result = FlattenIterationNode(root, iterationTeamMappings);

        // Assert
        result.Should().HaveCount(5);
        result.Single(i => i.Id == 1).TeamId.Should().Be(rootTeamId);
        result.Single(i => i.Id == 2).TeamId.Should().Be(rootTeamId); // Inherits from root
        result.Single(i => i.Id == 3).TeamId.Should().Be(rootTeamId); // Inherits from Level 1, which inherited from root
        result.Single(i => i.Id == 4).TeamId.Should().Be(level2BTeamId); // Has its own mapping
        result.Single(i => i.Id == 5).TeamId.Should().Be(level2BTeamId); // Inherits from Level 2B
    }

    [Fact]
    public void FlattenIterationNode_WithNullChildren_ReturnsRootOnly()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children = null
        };

        // Act
        var result = FlattenIterationNode(root, []);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public void FlattenIterationNode_WithEmptyChildren_ReturnsRootWithTeamId()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = rootId,
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children = []
        };

        var iterationTeamMappings = new Dictionary<Guid, Guid>
        {
            { rootId, teamId }
        };

        // Act
        var result = FlattenIterationNode(root, iterationTeamMappings);

        // Assert
        result.Should().ContainSingle();
        result[0].TeamId.Should().Be(teamId);
    }

    [Fact]
    public void FlattenIterationNode_WithNoTeamMappings_ReturnsNodesWithNullTeamIds()
    {
        // Arrange
        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = Guid.NewGuid(),
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = Guid.NewGuid(),
                    Name = "Child",
                    Path = "\\Root\\Iteration\\Child"
                }
            ]
        };

        // Act
        var result = FlattenIterationNode(root, []);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.TeamId == null);
    }

    [Fact]
    public void FlattenIterationNode_WithVariousHierarchyLevels_SetsHasChildrenCorrectly()
    {
        // Arrange
        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = Guid.NewGuid(),
            Name = "Root",
            Path = "\\Root\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = Guid.NewGuid(),
                    Name = "Parent",
                    Path = "\\Root\\Iteration\\Parent",
                    Children =
                    [
                        new IterationNodeResponse
                        {
                            Id = 3,
                            Identifier = Guid.NewGuid(),
                            Name = "Child",
                            Path = "\\Root\\Iteration\\Parent\\Child",
                            Children = null
                        }
                    ]
                }
            ]
        };

        // Act
        var result = FlattenIterationNode(root, []);

        // Assert
        result.Should().HaveCount(3);
        result.Single(i => i.Id == 1).HasChildren.Should().BeTrue(); // Root has children
        result.Single(i => i.Id == 2).HasChildren.Should().BeTrue(); // Parent has children
        result.Single(i => i.Id == 3).HasChildren.Should().BeFalse(); // Child has no children
    }

    [Fact]
    public void FlattenIterationNode_WithDateAttributes_PreservesDatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = Guid.NewGuid(),
            Name = "2024",
            Path = "\\Root\\Iteration\\2024",
            Attributes = new IterationAttributes
            {
                StartDate = startDate,
                EndDate = endDate
            }
        };

        // Act
        var result = FlattenIterationNode(root, []);

        // Assert
        result.Should().ContainSingle();
        result[0].StartDate.Should().Be(startDate);
        result[0].EndDate.Should().Be(endDate);
    }

    [Fact]
    public void FlattenIterationNode_WithNullAttributes_ReturnsNullDates()
    {
        // Arrange
        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = Guid.NewGuid(),
            Name = "Root",
            Path = "\\Root\\Iteration",
            Attributes = null
        };

        // Act
        var result = FlattenIterationNode(root, []);

        // Assert
        result.Should().ContainSingle();
        result[0].StartDate.Should().BeNull();
        result[0].EndDate.Should().BeNull();
    }

    [Fact]
    public void FlattenIterationNode_TransformsPathsByRemovingIterationNode()
    {
        // Arrange
        var root = new IterationNodeResponse
        {
            Id = 1,
            Identifier = Guid.NewGuid(),
            Name = "Root",
            Path = "\\Wayd\\Iteration",
            Children =
            [
                new IterationNodeResponse
                {
                    Id = 2,
                    Identifier = Guid.NewGuid(),
                    Name = "Team Wayd",
                    Path = "\\Wayd\\Iteration\\Team Wayd",
                    Children =
                    [
                        new IterationNodeResponse
                        {
                            Id = 3,
                            Identifier = Guid.NewGuid(),
                            Name = "2024",
                            Path = "\\Wayd\\Iteration\\Team Wayd\\2024"
                        }
                    ]
                }
            ]
        };

        // Act
        var result = FlattenIterationNode(root, []);

        // Assert
        result.Should().HaveCount(3);
        result.Single(i => i.Id == 1).Path.Should().Be("\\Wayd");
        result.Single(i => i.Id == 2).Path.Should().Be("\\Wayd\\Team Wayd");
        result.Single(i => i.Id == 3).Path.Should().Be("\\Wayd\\Team Wayd\\2024");
    }

    [Fact]
    public void FlattenIterationNode_WithComplexRealWorldStructure_FlattensAndAssignsTeamIdsCorrectly()
    {
        // Arrange - Use the same JSON structure from IterationNodeResponseTests
        var json = GetRealWorldJson();
        var root = JsonSerializer.Deserialize<IterationNodeResponse>(json, _options);

        var teamWaydId = Guid.Parse("c2d4a31b-9c69-4efb-898e-34a82b559edf");
        var teamId = Guid.NewGuid();

        var iterationTeamMappings = new Dictionary<Guid, Guid>
        {
            { teamWaydId, teamId }
        };

        // Act
        var result = FlattenIterationNode(root!, iterationTeamMappings);

        // Assert
        result.Should().HaveCount(14); // Same count as the old FlattenHierarchy test

        // Verify specific iterations exist
        var iterationNoDates = result.Single(i => i.Id == 421);
        iterationNoDates.StartDate.Should().BeNull();
        iterationNoDates.EndDate.Should().BeNull();
        iterationNoDates.Name.Should().Be("Wayd Integrations Team");

        var iterationWithDates = result.Single(i => i.Id == 426);
        iterationWithDates.StartDate.Should().Be(new DateTime(2022, 9, 26, 0, 0, 0, DateTimeKind.Utc));
        iterationWithDates.EndDate.Should().Be(new DateTime(2022, 10, 9, 0, 0, 0, DateTimeKind.Utc));
        iterationWithDates.Name.Should().Be("22.3.7");

        // Verify team ID inheritance
        var teamWayd = result.Single(i => i.Id == 422);
        teamWayd.TeamId.Should().Be(teamId);

        // Children of Team Wayd should inherit the team ID
        var year2022 = result.Single(i => i.Id == 423);
        year2022.TeamId.Should().Be(teamId);
    }

    /// <summary>
    /// Helper method that mimics the private FlattenAndSetTeamIds logic for testing purposes.
    /// This uses the same algorithm as the actual implementation.
    /// </summary>
    private static List<IterationDto> FlattenIterationNode(IterationNodeResponse root, Dictionary<Guid, Guid> iterationTeamMapping)
    {
        var result = new List<IterationDto>();
        var stack = new Stack<(IterationNodeResponse Node, Guid? ParentTeamId)>();
        stack.Push((root, null));

        while (stack.Count > 0)
        {
            var (current, parentTeamId) = stack.Pop();

            var teamId = iterationTeamMapping.TryGetValue(current.Identifier, out var mappedTeamId)
                ? mappedTeamId
                : parentTeamId;

            result.Add(new IterationDto
            {
                Id = current.Id,
                Identifier = current.Identifier,
                Name = current.Name,
                Path = ClassificationNodeUtils.RemoveClassificationTypeFromPath(current.Path),
                TeamId = teamId,
                StartDate = current.Attributes?.StartDate,
                EndDate = current.Attributes?.EndDate,
                HasChildren = current.Children is not null && current.Children.Count != 0
            });

            if (current.Children is not null)
            {
                foreach (var child in current.Children)
                {
                    stack.Push((child, teamId));
                }
            }
        }

        return result;
    }

    private static string GetRealWorldJson()
    {
        return """
            {
                "id": 127,
                "identifier": "68429463-523f-4c69-8c16-4321543db2e4",
                "name": "Wayd",
                "structureType": "iteration",
                "hasChildren": true,
                "children": [
                    {
                        "id": 421,
                        "identifier": "8f218f2f-dc77-4f6a-af01-6d39deb89cca",
                        "name": "Wayd Integrations Team",
                        "structureType": "iteration",
                        "hasChildren": true,
                        "children": [
                            {
                                "id": 32,
                                "identifier": "56503f9a-075b-4688-b83b-135c2adc982d",
                                "name": "Test",
                                "structureType": "iteration",
                                "hasChildren": false,
                                "path": "\\Wayd\\Iteration\\Wayd Integrations Team\\Test",
                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Wayd%20Integrations%20Team/Test"
                            }
                        ],
                        "path": "\\Wayd\\Iteration\\Wayd Integrations Team",
                        "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Wayd%20Integrations%20Team"
                    },
                    {
                        "id": 422,
                        "identifier": "c2d4a31b-9c69-4efb-898e-34a82b559edf",
                        "name": "Team Wayd",
                        "structureType": "iteration",
                        "hasChildren": true,
                        "children": [
                            {
                                "id": 423,
                                "identifier": "7b2dca14-11a5-4eec-ac94-68924cbf9342",
                                "name": "2022",
                                "structureType": "iteration",
                                "hasChildren": true,
                                "children": [
                                    {
                                        "id": 424,
                                        "identifier": "4ca97d96-cf9d-484f-b9c8-ee9bb795d167",
                                        "name": "22.3",
                                        "structureType": "iteration",
                                        "hasChildren": true,
                                        "children": [
                                            {
                                                "id": 425,
                                                "identifier": "fe563999-7a47-46b5-b880-18a0daa3f47d",
                                                "name": "22.3.6",
                                                "structureType": "iteration",
                                                "hasChildren": false,
                                                "attributes": {
                                                    "startDate": "2022-09-12T00:00:00Z",
                                                    "finishDate": "2022-09-25T00:00:00Z"
                                                },
                                                "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.3\\22.3.6",
                                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.3/22.3.6"
                                            },
                                            {
                                                "id": 426,
                                                "identifier": "17edf3d0-b04b-4b60-980a-dfc5ca27c751",
                                                "name": "22.3.7",
                                                "structureType": "iteration",
                                                "hasChildren": false,
                                                "attributes": {
                                                    "startDate": "2022-09-26T00:00:00Z",
                                                    "finishDate": "2022-10-09T00:00:00Z"
                                                },
                                                "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.3\\22.3.7",
                                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.3/22.3.7"
                                            }
                                        ],
                                        "attributes": {
                                            "startDate": "2022-08-01T00:00:00Z",
                                            "finishDate": "2022-10-09T00:00:00Z"
                                        },
                                        "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.3",
                                        "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.3"
                                    },
                                    {
                                        "id": 427,
                                        "identifier": "134aadb6-3666-45ed-bb56-478fa6a12779",
                                        "name": "22.4",
                                        "structureType": "iteration",
                                        "hasChildren": true,
                                        "children": [
                                            {
                                                "id": 428,
                                                "identifier": "5d2df634-b458-4175-8f82-e415640f0472",
                                                "name": "22.4.1",
                                                "structureType": "iteration",
                                                "hasChildren": false,
                                                "attributes": {
                                                    "startDate": "2022-10-10T00:00:00Z",
                                                    "finishDate": "2022-10-23T00:00:00Z"
                                                },
                                                "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.4\\22.4.1",
                                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.4/22.4.1"
                                            },
                                            {
                                                "id": 429,
                                                "identifier": "5fa3f493-506e-4ebf-b494-8bd3e435b43a",
                                                "name": "22.4.2",
                                                "structureType": "iteration",
                                                "hasChildren": false,
                                                "attributes": {
                                                    "startDate": "2022-10-24T00:00:00Z",
                                                    "finishDate": "2022-11-06T00:00:00Z"
                                                },
                                                "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.4\\22.4.2",
                                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.4/22.4.2"
                                            },
                                            {
                                                "id": 430,
                                                "identifier": "7aac18ba-8e07-4842-8e9c-fca9af9b837b",
                                                "name": "22.4.3",
                                                "structureType": "iteration",
                                                "hasChildren": false,
                                                "attributes": {
                                                    "startDate": "2022-11-07T00:00:00Z",
                                                    "finishDate": "2022-11-20T00:00:00Z"
                                                },
                                                "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.4\\22.4.3",
                                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.4/22.4.3"
                                            }
                                        ],
                                        "attributes": {
                                            "startDate": "2022-10-10T00:00:00Z",
                                            "finishDate": "2023-01-01T00:00:00Z"
                                        },
                                        "path": "\\Wayd\\Iteration\\Team Wayd\\2022\\22.4",
                                        "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022/22.4"
                                    }
                                ],
                                "attributes": {
                                    "startDate": "2022-01-03T00:00:00Z",
                                    "finishDate": "2023-01-01T00:00:00Z"
                                },
                                "path": "\\Wayd\\Iteration\\Team Wayd\\2022",
                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2022"
                            },
                            {
                                "id": 431,
                                "identifier": "95c3321d-e1fd-413b-9506-49fa7427af56",
                                "name": "2023",
                                "structureType": "iteration",
                                "hasChildren": false,
                                "attributes": {
                                    "startDate": "2023-01-02T00:00:00Z",
                                    "finishDate": "2023-12-31T00:00:00Z"
                                },
                                "path": "\\Wayd\\Iteration\\Team Wayd\\2023",
                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2023"
                            },
                            {
                                "id": 432,
                                "identifier": "e9b7c75f-16ca-4a04-9552-0e41f4f1e0b9",
                                "name": "2024",
                                "structureType": "iteration",
                                "hasChildren": false,
                                "attributes": {
                                    "startDate": "2024-01-01T00:00:00Z",
                                    "finishDate": "2024-12-29T00:00:00Z"
                                },
                                "path": "\\Wayd\\Iteration\\Team Wayd\\2024",
                                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd/2024"
                            }
                        ],
                        "path": "\\Wayd\\Iteration\\Team Wayd",
                        "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations/Team%20Wayd"
                    }
                ],
                "path": "\\Wayd\\Iteration",
                "_links": {
                    "self": {
                        "href": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations"
                    }
                },
                "url": "https://dev.azure.com/dstacey/3b15d01e-d259-48eb-a15c-dd29384fd598/_apis/wit/classificationNodes/Iterations"
            }
            """;
    }
}
