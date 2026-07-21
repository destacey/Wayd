using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Integrations.AzureDevOps.Models.Projects;
using Wayd.Integrations.AzureDevOps.Models.WorkItems;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Models.WorkItems;

public class WorkItemResponseTests
{
    [Fact]
    public void ToIExternalWorkItems_WithKnownIteration_MapsTeamAndIterationFields()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var identifier = Guid.NewGuid();
        var iteration = MakeIteration(id: 5, identifier, teamId);
        var workItem = MakeWorkItem(id: 101, iterationId: 5);

        // Act
        var result = new List<WorkItemResponse> { workItem }.ToIExternalWorkItems([iteration], NullLogger.Instance);

        // Assert
        var mapped = result.Should().ContainSingle().Subject;
        mapped.IterationId.Should().Be(5);
        mapped.TeamId.Should().Be(teamId);
        mapped.ExternalTeamIdentifier.Should().Be(identifier.ToString());
    }

    [Fact]
    public void ToIExternalWorkItems_WithIterationIdNotInSyncedSet_SyncsItemWithNullIterationAndTeam()
    {
        // Arrange - System.IterationId defaults to 0 when Azure DevOps has no iteration assigned;
        // it can also reference an iteration outside the cached tree. Either way the item must
        // still sync rather than throw KeyNotFoundException.
        var workItem = MakeWorkItem(id: 102, iterationId: 0);

        // Act
        var act = () => new List<WorkItemResponse> { workItem }.ToIExternalWorkItems([], NullLogger.Instance);

        // Assert
        var result = act.Should().NotThrow().Subject;
        var mapped = result.Should().ContainSingle().Subject;
        mapped.IterationId.Should().BeNull();
        mapped.TeamId.Should().BeNull();
        mapped.ExternalTeamIdentifier.Should().BeNull();
    }

    [Fact]
    public void ToIExternalWorkItems_WithMixOfKnownAndUnknownIterations_MapsEachIndependently()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var identifier = Guid.NewGuid();
        var iteration = MakeIteration(id: 5, identifier, teamId);
        var knownItem = MakeWorkItem(id: 201, iterationId: 5);
        var unknownItem = MakeWorkItem(id: 202, iterationId: 999);

        // Act
        var result = new List<WorkItemResponse> { knownItem, unknownItem }.ToIExternalWorkItems([iteration], NullLogger.Instance);

        // Assert
        result.Should().HaveCount(2);
        result.Single(w => w.Id == 201).TeamId.Should().Be(teamId);
        result.Single(w => w.Id == 202).TeamId.Should().BeNull();
    }

    private static WorkItemResponse MakeWorkItem(int id, int iterationId)
    {
        return new WorkItemResponse
        {
            Id = id,
            Fields = new WorkItemFieldsResponse
            {
                Title = "Sample work item",
                WorkItemType = "User Story",
                State = "Active",
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = new() { UniqueName = "dev@acme.example" },
                ChangedDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                ChangedBy = new() { UniqueName = "dev@acme.example" },
                IterationId = iterationId
            }
        };
    }

    private static IterationDto MakeIteration(int id, Guid identifier, Guid teamId)
    {
        return new IterationDto
        {
            Id = id,
            Identifier = identifier,
            Name = "Sprint 1",
            Path = "\\Atlas\\Sprint 1",
            TeamId = teamId
        };
    }
}
