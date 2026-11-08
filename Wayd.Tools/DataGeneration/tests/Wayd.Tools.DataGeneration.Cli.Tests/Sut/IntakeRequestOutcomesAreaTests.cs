using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>What the outcomes area waits for Demand Management to have copied before it posts its rows.</summary>
public class IntakeRequestOutcomesAreaTests
{
    private static IntakeRequestOutcomeCsvRow Row(Guid requestId, Guid projectId, params Guid[] deliverableIds) => new()
    {
        ImportId = $"{requestId}|{projectId}",
        RequestId = requestId,
        Kind = deliverableIds.Length == 0 ? "NewProject" : "ExistingProjectScope",
        ProjectId = projectId,
        DeliverableIds = deliverableIds.Length == 0 ? null : string.Join(';', deliverableIds),
    };

    [Fact]
    public void Expected_AsksOncePerProjectForEveryDeliverableAnyRowMaps()
    {
        // Arrange — two requests absorbed by one project, mapping overlapping deliverables; another started a project.
        var shared = Guid.NewGuid();
        var started = Guid.NewGuid();
        var (portal, reports) = (Guid.NewGuid(), Guid.NewGuid());
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var expected = IntakeRequestOutcomesArea.Expected(
        [
            Row(first, shared, portal),
            Row(second, shared, portal, reports),
            Row(third, started),
        ]);

        // Assert
        expected.Should().HaveCount(2);
        var forShared = expected.Single(e => e.ProjectId == shared);
        forShared.RequestId.Should().Be(first, "any request pointing at the project can ask for its copy");
        forShared.DeliverableIds.Should().BeEquivalentTo([portal, reports]);
        expected.Single(e => e.ProjectId == started).DeliverableIds.Should().BeEmpty(
            "a new project's copy is waited for even when it maps no deliverable");
    }
}
