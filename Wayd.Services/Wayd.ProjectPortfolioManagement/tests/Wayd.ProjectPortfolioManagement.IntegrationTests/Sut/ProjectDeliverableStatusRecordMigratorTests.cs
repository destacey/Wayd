using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ProjectDeliverableStatusRecordMigrator"/> against a real SQL Server container.
/// <para>
/// Its refusal reads which projects are closed in SQL, joining deliverables to their projects, so only a real
/// database exercises it.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ProjectDeliverableStatusRecordMigratorTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ProjectDeliverableStatusRecordMigratorTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Seeds a completed project whose one deliverable is Accepted, on the seeded default workflow.</summary>
    private async Task<ActiveProjectWithDeliverable> CompletedProjectWithAcceptedDeliverable(CancellationToken ct)
    {
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);

        await using var context = _fixture.CreateContext();
        var project = await context.Projects.Include(p => p.Deliverables).SingleAsync(p => p.Id == seed.ProjectId, ct);
        project.Complete(PpmActor.System, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        return seed;
    }

    /// <summary>
    /// A deliverable workflow to move onto, with the Accepted status of the seeded default mapped to
    /// <paramref name="acceptedLandsIn"/>.
    /// </summary>
    private async Task<StatusRemap> RemapSendingAcceptedTo(string acceptedLandsIn, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var from = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .SingleAsync(w => w.OwnerType == PpmWorkflowOwners.ProjectDeliverable.Key && w.IsSystem, ct);

        var at = SqlServerDbContextFixture.FixedNow;
        // Named apart per test: workflows outlive the fixture's PPM reset.
        var to = StatusWorkflow.Create($"Lightweight {Guid.NewGuid().ToString("N")[..8]}", null, PpmWorkflowOwners.ProjectDeliverable.Key, EventActor.System, at).Value;
        to.AddStatus("Not Started", null, StatusCategory.Proposed, (int)ProjectDeliverableStatusAlias.NotStarted, EventActor.System, at);
        to.AddStatus("In Progress", null, StatusCategory.Active, (int)ProjectDeliverableStatusAlias.InProgress, EventActor.System, at);
        to.AddStatus("Signed Off", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, at);
        to.AddStatus("Descoped", null, StatusCategory.Removed, (int)ProjectDeliverableStatusAlias.Descoped, EventActor.System, at);
        context.StatusWorkflows.Add(to);
        await context.SaveChangesAsync(ct);

        var remap = StatusRemap.AutoMap(from, to).Value;
        var accepted = from.StatusFor((int)ProjectDeliverableStatusAlias.Accepted)!;
        remap.Resolve(accepted.Id, to.Statuses.Single(s => s.Name == acceptedLandsIn)).IsSuccess.Should().BeTrue();

        return remap;
    }

    [Fact]
    public async Task Migrate_WhenAClosedProjectsDeliverableWouldReopen_RefusesAndNamesTheMove()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await CompletedProjectWithAcceptedDeliverable(ct);
        var remap = await RemapSendingAcceptedTo("In Progress", ct);

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new ProjectDeliverableStatusRecordMigrator(context)
            .Migrate(remap, scopeId: null, EventActor.System, SqlServerDbContextFixture.FixedNow, ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("completed or canceled projects").And.Contain("1 in 'Accepted' would move to 'In Progress'");

        var deliverable = await context.ProjectDeliverables.AsNoTracking().SingleAsync(d => d.Id == seed.DeliverableId, ct);
        deliverable.StatusId.Should().Be(seed.Statuses[ProjectDeliverableStatusAlias.Accepted].StatusId);
    }

    [Fact]
    public async Task Migrate_WhenAClosedProjectsDeliverableStaysClosed_MovesItAndClearsTheAcceptance()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await CompletedProjectWithAcceptedDeliverable(ct);
        var remap = await RemapSendingAcceptedTo("Signed Off", ct);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var result = await new ProjectDeliverableStatusRecordMigrator(context)
                .Migrate(remap, scopeId: null, EventActor.System, SqlServerDbContextFixture.FixedNow, ct);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
            result.Value.Should().Be(1);
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var deliverable = await verify.ProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        deliverable.StatusName.Should().Be("Signed Off");
        deliverable.StatusCategory.Should().Be(StatusCategory.Done);
        deliverable.AcceptedOn.Should().BeNull();
        deliverable.AcceptedById.Should().BeNull();
    }
}
