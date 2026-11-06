using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Persistence tests for <see cref="Domain.Models.ProjectDeliverable"/> against a real SQL Server container.
/// <para>
/// A deliverable's status history is not a navigation, so its rows reach the database only because the save
/// collects them from the change tracker, and the project's revision guards a rule only when the database
/// enforces it. The in-memory fake models neither.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ProjectDeliverablePersistenceTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ProjectDeliverablePersistenceTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Save_WritesTheDeliverableWithItsStatusHistoryAndEvents()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var deliverable = await verify.ProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        deliverable.ProjectId.Should().Be(seed.ProjectId);
        deliverable.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        deliverable.AcceptedOn.Should().Be(SqlServerDbContextFixture.FixedNow);

        var transitions = await verify.StatusTransitions
            .Where(t => t.OwnerType == PpmWorkflowOwners.ProjectDeliverable.Key && t.RecordId == seed.DeliverableId)
            .OrderBy(t => t.Sequence)
            .ToListAsync(ct);
        transitions.Select(t => t.ToStatusName).Should().Equal("Not Started", "Accepted");

        var eventTypes = await verify.ActivityLogs
            .Where(a => a.AggregateId == seed.ProjectId)
            .Select(a => a.EventType)
            .ToListAsync(ct);
        eventTypes.Should().Contain([nameof(ProjectDeliverableAddedEvent), nameof(ProjectDeliverableStatusChangedEvent)]);
    }

    [Fact]
    public async Task Save_WhenTheProjectChangedSinceItWasRead_IsRefused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);

        await using var completing = _fixture.CreateContext();
        var stale = await completing.Projects
            .Include(p => p.Deliverables)
            .SingleAsync(p => p.Id == seed.ProjectId, ct);

        await using (var reopening = _fixture.CreateContext())
        {
            var current = await reopening.Projects
                .Include(p => p.Deliverables)
                .SingleAsync(p => p.Id == seed.ProjectId, ct);
            current.ChangeDeliverableStatus(PpmActor.System, ProjectAncestryRoles.None, seed.DeliverableId,
                seed.Statuses[ProjectDeliverableStatusAlias.InProgress], null, SqlServerDbContextFixture.FixedNow)
                .IsSuccess.Should().BeTrue();
            await reopening.SaveChangesAsync(ct);
        }

        stale.Complete(PpmActor.System, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue("its loaded deliverables are all still accepted");

        // Act
        var act = () => completing.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "completing over a reopened deliverable would leave a completed project with work outstanding");
    }
}
