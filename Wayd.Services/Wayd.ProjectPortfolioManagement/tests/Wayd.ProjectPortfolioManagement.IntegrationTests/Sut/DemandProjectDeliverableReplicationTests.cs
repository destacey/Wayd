using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.DemandProjectDeliverables.Commands;
using Wayd.DemandManagement.Application.DemandProjectDeliverables.EventHandlers;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Demand Management's copy of PPM's deliverables, end to end against a real SQL Server container: the source
/// queries project the deliverable in SQL, the copy's watermarks and enums round-trip through their converters,
/// it goes with its project's copy by cascade, and the migration's backfill writes watermarks EF can read. The
/// in-memory fake proves none of these.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DemandProjectDeliverableReplicationTests
{
    private static readonly Instant Later = SqlServerDbContextFixture.FixedNow.Plus(Duration.FromMinutes(5));

    private readonly SqlServerDbContextFixture _fixture;

    public DemandProjectDeliverableReplicationTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetSimpleProjectDeliverable_ProjectsTheDeliverableWithItsStatusMeaning()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);
        await using var context = _fixture.CreateContext();

        // Act
        var one = await new GetSimpleProjectDeliverableQueryHandler(context)
            .Handle(new GetSimpleProjectDeliverableQuery(seed.DeliverableId), ct);
        var all = await new GetSimpleProjectDeliverablesQueryHandler(context)
            .Handle(new GetSimpleProjectDeliverablesQuery(), ct);

        // Assert
        one.Should().BeEquivalentTo(new SimpleProjectDeliverable(
            seed.DeliverableId, seed.ProjectId, "Customer portal", StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted));
        all.Should().ContainSingle(d => d.Id == seed.DeliverableId).Which.Should().BeEquivalentTo(one);
    }

    [Fact]
    public async Task GetSimpleProjectDeliverable_ForADeliverableThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetSimpleProjectDeliverableQueryHandler(context)
            .Handle(new GetSimpleProjectDeliverableQuery(Guid.NewGuid()), ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SyncHandler_BuildsTheCopyAndItsProjectFromTheSource_ThenAppliesAStatusChange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);

        // Act
        await using (var adding = _fixture.CreateContext())
        {
            await Handler(adding).Handle(AddedEvent(seed), ct);
        }
        await using (var accepting = _fixture.CreateContext())
        {
            await Handler(accepting).Handle(AcceptedEvent(seed, Later), ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var projectCopy = verify.DemandProjects.Should().ContainSingle(p => p.Id == seed.ProjectId).Subject;
        projectCopy.Watermarks.Should().Be(DemandProjectWatermarks.At(SqlServerDbContextFixture.FixedNow));
        projectCopy.LifecycleCategory.Should().Be(LifecycleCategory.Active);
        var copy = await verify.DemandProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        copy.ProjectId.Should().Be(seed.ProjectId);
        copy.Name.Should().Be("Customer portal");
        copy.StatusCategory.Should().Be(StatusCategory.Done);
        copy.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        copy.Watermarks.Should().Be(new DemandProjectDeliverableWatermarks(SqlServerDbContextFixture.FixedNow, Later));
    }

    [Fact]
    public async Task DeletingTheProjectsCopy_TakesItsDeliverablesCopiesWithIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);
        await using (var adding = _fixture.CreateContext())
        {
            await Handler(adding).Handle(AddedEvent(seed), ct);
        }

        // Act
        await using (var deleting = _fixture.CreateContext())
        {
            var project = await deleting.DemandProjects.SingleAsync(p => p.Id == seed.ProjectId, ct);
            deleting.DemandProjects.Remove(project);
            await deleting.SaveChangesAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.DemandProjectDeliverables.AnyAsync(d => d.Id == seed.DeliverableId, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task TwoHandlersChangingOneCopy_TheSecondSaveConflicts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);
        await using (var adding = _fixture.CreateContext())
        {
            await Handler(adding).Handle(AddedEvent(seed), ct);
        }

        await using var renaming = _fixture.CreateContext();
        var stale = await renaming.DemandProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        await using (var accepting = _fixture.CreateContext())
        {
            var current = await accepting.DemandProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
            current.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Later);
            await accepting.SaveChangesAsync(ct);
        }
        stale.ApplyName("Partner portal", Later);

        // Act
        var act = () => renaming.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the rename would write back the status watermark the acceptance replaced");
    }

    [Fact]
    public async Task Resync_CreatesAMissingCopyFromEveryDeliverable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);
        await using (var copying = _fixture.CreateContext())
        {
            copying.DemandProjects.Add(new DemandProject(await SourceProject(copying, seed.ProjectId, ct), SqlServerDbContextFixture.FixedNow));
            await copying.SaveChangesAsync(ct);
        }

        // Act
        await using (var syncing = _fixture.CreateContext())
        {
            var sources = await new GetSimpleProjectDeliverablesQueryHandler(syncing).Handle(new GetSimpleProjectDeliverablesQuery(), ct);
            var result = await new SyncDemandProjectDeliverablesCommandHandler(syncing, NullLogger<SyncDemandProjectDeliverablesCommandHandler>.Instance)
                .Handle(new SyncDemandProjectDeliverablesCommand(sources, Later), ct);
            result.IsSuccess.Should().BeTrue();
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var copy = await verify.DemandProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        copy.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        copy.Watermarks.Should().Be(DemandProjectDeliverableWatermarks.At(Later));
    }

    [Fact]
    public async Task Backfill_CopiesEachDeliverableOfACopiedProject_WithWatermarksEfCanRead()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: true, ct);
        await using (var copying = _fixture.CreateContext())
        {
            copying.DemandProjects.Add(new DemandProject(await SourceProject(copying, seed.ProjectId, ct), SqlServerDbContextFixture.FixedNow));
            await copying.SaveChangesAsync(ct);
        }
        // ExecuteSqlRaw formats its text, so the JSON literal's braces are escaped; the migration runs it unformatted.
        var backfill = new AddDemandProjectDeliverables().UpOperations.OfType<SqlOperation>().Single().Sql
            .Replace("{", "{{").Replace("}", "}}");

        // Act
        await using (var migrating = _fixture.CreateContext())
        {
            await migrating.Database.ExecuteSqlRawAsync(backfill, ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var copy = await verify.DemandProjectDeliverables.SingleAsync(d => d.Id == seed.DeliverableId, ct);
        copy.ProjectId.Should().Be(seed.ProjectId);
        copy.Name.Should().Be("Customer portal");
        copy.StatusCategory.Should().Be(StatusCategory.Done);
        copy.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        copy.Watermarks.Name.Should().NotBeNull();
        copy.Watermarks.Status.Should().Be(copy.Watermarks.Name);
    }

    [Fact]
    public async Task LifecycleBackfill_GivesEachCopyItsProjectsLifecycle_WithAWatermarkEfCanRead()
    {
        // Arrange — a copy made before copies held a lifecycle reads the column's default
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct);
        await using (var copying = _fixture.CreateContext())
        {
            var source = await SourceProject(copying, seed.ProjectId, ct);
            copying.DemandProjects.Add(new DemandProject(new PredatingSource(source), SqlServerDbContextFixture.FixedNow));
            await copying.SaveChangesAsync(ct);
        }
        var backfill = new AddDemandProjectLifecycleCategory().UpOperations.OfType<SqlOperation>().Single().Sql;

        // Act
        await using (var migrating = _fixture.CreateContext())
        {
            await migrating.Database.ExecuteSqlRawAsync(backfill, ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var copy = await verify.DemandProjects.SingleAsync(p => p.Id == seed.ProjectId, ct);
        copy.LifecycleCategory.Should().Be(LifecycleCategory.Active);
        copy.Watermarks.Lifecycle.Should().NotBeNull();
        copy.Watermarks.Name.Should().Be(SqlServerDbContextFixture.FixedNow, "the backfill stamps only the lifecycle group");
    }

    /// <summary>A project as a copy made before copies held a lifecycle saw it: at the column's default.</summary>
    private sealed record PredatingSource(ISimpleProjectWithLifecycle Source) : ISimpleProjectWithLifecycle
    {
        public Guid Id => Source.Id;
        public ProjectKey Key => Source.Key;
        public string Name => Source.Name;
        public string Description => Source.Description;
        public LifecycleCategory LifecycleCategory => LifecycleCategory.NotStarted;
    }

    private static async Task<ISimpleProjectWithLifecycle> SourceProject(Wayd.Infrastructure.Persistence.Context.WaydDbContext context, Guid projectId, CancellationToken ct) =>
        (await new GetSimpleProjectWithLifecycleQueryHandler(context).Handle(new GetSimpleProjectWithLifecycleQuery(projectId), ct))!;

    /// <summary>A handler whose source reads go to PPM's real query handlers over the same database.</summary>
    private static DemandProjectDeliverableSyncHandler Handler(Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleProjectDeliverableQuery>(), It.IsAny<CancellationToken>()))
            .Returns((GetSimpleProjectDeliverableQuery q, CancellationToken ct) => new GetSimpleProjectDeliverableQueryHandler(context).Handle(q, ct));
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleProjectWithLifecycleQuery>(), It.IsAny<CancellationToken>()))
            .Returns((GetSimpleProjectWithLifecycleQuery q, CancellationToken ct) => new GetSimpleProjectWithLifecycleQueryHandler(context).Handle(q, ct));

        return new DemandProjectDeliverableSyncHandler(context, dispatcher.Object, NullLogger<DemandProjectDeliverableSyncHandler>.Instance);
    }

    private static ProjectDeliverableAddedEvent AddedEvent(ActiveProjectWithDeliverable seed) =>
        new(seed.ProjectId, new ProjectKey("GEMINI"), seed.DeliverableId, "Customer portal", null, null, 1, null, null,
            seed.Statuses[ProjectDeliverableStatusAlias.NotStarted].StatusId, "Not Started", StatusCategory.Proposed,
            ProjectDeliverableStatusAlias.NotStarted, EventActor.System, SqlServerDbContextFixture.FixedNow);

    private static ProjectDeliverableStatusChangedEvent AcceptedEvent(ActiveProjectWithDeliverable seed, Instant timestamp) =>
        new(seed.ProjectId, new ProjectKey("GEMINI"), seed.DeliverableId,
            seed.Statuses[ProjectDeliverableStatusAlias.NotStarted].StatusId, "Not Started", StatusCategory.Proposed, ProjectDeliverableStatusAlias.NotStarted,
            seed.Statuses[ProjectDeliverableStatusAlias.Accepted].StatusId, "Accepted", StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted,
            null, EventActor.System, timestamp);
}
