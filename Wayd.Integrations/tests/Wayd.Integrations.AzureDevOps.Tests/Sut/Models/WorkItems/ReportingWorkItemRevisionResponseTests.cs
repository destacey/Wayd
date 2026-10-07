using NodaTime;
using Wayd.Integrations.AzureDevOps.Models;
using Wayd.Integrations.AzureDevOps.Models.WorkItems;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Models.WorkItems;

public class ReportingWorkItemRevisionResponseTests
{
    [Fact]
    public void ToIExternalWorkItemRevisions_MapsTrackedFields()
    {
        // Arrange
        var revision = MakeRevision(id: 101, rev: 4, fields: MakeFields() with
        {
            IterationId = 5,
            AssignedTo = new UserResponse { Id = "8c8c7d32-6b1b-47f4-b2e9-30b477b5ab3d", DisplayName = "Dev One", UniqueName = "dev@acme.example" },
            StoryPoints = 3,
            Effort = 8,
            Size = 13,
        });

        // Act
        var result = new List<ReportingWorkItemRevisionResponse> { revision }.ToIExternalWorkItemRevisions();

        // Assert
        var mapped = result.Should().ContainSingle().Subject;
        mapped.WorkItemId.Should().Be(101);
        mapped.Revision.Should().Be(4);
        mapped.Changed.Should().Be(Instant.FromUtc(2026, 1, 2, 10, 0));
        mapped.WorkType.Should().Be("User Story");
        mapped.WorkStatus.Should().Be("Active");
        mapped.IterationId.Should().Be(5);
        mapped.TeamKey.Should().BeNull();
        mapped.AssignedTo!.ExternalId.Should().Be("8c8c7d32-6b1b-47f4-b2e9-30b477b5ab3d");
        mapped.AssignedTo.Email.Should().Be("dev@acme.example");
        mapped.StoryPoints.Should().Be(3);
        mapped.Effort.Should().Be(8);
        mapped.Size.Should().Be(13);
    }

    [Fact]
    public void ToIExternalWorkItemRevisions_WithIterationIdZero_MapsToNoIteration()
    {
        // Arrange
        var revision = MakeRevision(id: 101, rev: 1, fields: MakeFields() with { IterationId = 0 });

        // Act
        var result = new List<ReportingWorkItemRevisionResponse> { revision }.ToIExternalWorkItemRevisions();

        // Assert
        result.Should().ContainSingle().Which.IterationId.Should().BeNull();
    }

    [Fact]
    public void ToIExternalWorkItemRevisions_WithAssigneeWithoutIdentityId_MapsToUnassigned()
    {
        // Arrange
        var revision = MakeRevision(id: 101, rev: 1, fields: MakeFields() with { AssignedTo = new UserResponse { UniqueName = "dev@acme.example" } });

        // Act
        var result = new List<ReportingWorkItemRevisionResponse> { revision }.ToIExternalWorkItemRevisions();

        // Assert
        result.Should().ContainSingle().Which.AssignedTo.Should().BeNull();
    }

    [Fact]
    public void ToIExternalWorkItemRevisions_WithNegativeEstimates_ClampsThemToZero()
    {
        // Arrange
        var revision = MakeRevision(id: 101, rev: 1, fields: MakeFields() with { StoryPoints = -1, Effort = -2, Size = -3 });

        // Act
        var result = new List<ReportingWorkItemRevisionResponse> { revision }.ToIExternalWorkItemRevisions();

        // Assert
        var mapped = result.Should().ContainSingle().Subject;
        mapped.StoryPoints.Should().Be(0);
        mapped.Effort.Should().Be(0);
        mapped.Size.Should().Be(0);
    }

    [Fact]
    public void ToIExternalWorkItemRevisions_WithoutWorkTypeStateOrFields_DropsTheRevision()
    {
        // Arrange
        List<ReportingWorkItemRevisionResponse> revisions =
        [
            MakeRevision(id: 101, rev: 1, fields: MakeFields() with { WorkItemType = null }),
            MakeRevision(id: 102, rev: 1, fields: MakeFields() with { State = " " }),
            MakeRevision(id: 103, rev: 1, fields: null),
            MakeRevision(id: 104, rev: 1, fields: MakeFields()),
        ];

        // Act
        var result = revisions.ToIExternalWorkItemRevisions();

        // Assert
        result.Should().ContainSingle().Which.WorkItemId.Should().Be(104);
    }

    private static ReportingWorkItemRevisionResponse MakeRevision(int id, int rev, ReportingWorkItemRevisionFieldsResponse? fields) =>
        new() { Id = id, Rev = rev, Fields = fields };

    private static ReportingWorkItemRevisionFieldsResponse MakeFields() =>
        new()
        {
            ChangedDate = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero),
            WorkItemType = "User Story",
            State = "Active",
        };
}
