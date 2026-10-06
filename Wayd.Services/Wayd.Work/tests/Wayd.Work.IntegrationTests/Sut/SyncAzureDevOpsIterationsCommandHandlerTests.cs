using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because what is under test is the foreign key from a work item to its sprint:
/// the fake context enforces no keys, so a delete it accepts can still be refused by the database.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SyncAzureDevOpsIterationsCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "sync-iterations-test";
    private static readonly Guid AzdoProjectId = Guid.NewGuid();

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WhenASprintWithWorkItemsIsGoneFromTheSource_DeletesItAndClearsTheWorkItems()
    {
        // Arrange — the work item sync runs after the iteration sync, so on this pass the work item still
        // points at the sprint the source no longer lists.
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        await Sync([External(1, "Sprint 1"), External(2, "Sprint 2")], ct);

        Guid removedSprintId;
        Guid workItemId;
        await using (var seed = new WaydDbContextAccessor(_fixture))
        {
            removedSprintId = await seed.Context.Iterations
                .Where(i => i.OwnershipInfo.SystemId == SystemId && i.OwnershipInfo.ExternalId == "1")
                .Select(i => i.Id)
                .SingleAsync(ct);
            var workspaceId = await SeedWorkspace(seed.Context);
            workItemId = await InsertWorkItem(seed.Context, workspaceId, removedSprintId);
        }

        // Act
        await Sync([External(2, "Sprint 2")], ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.Iterations.AnyAsync(i => i.Id == removedSprintId, ct)).Should().BeFalse();
        (await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == workItemId, ct)).IterationId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenASprintInWorkItemHistoryIsGoneFromTheSource_DeletesItAndKeepsTheSourceIterationId()
    {
        // Arrange — the database cannot clear the history's reference itself, so the delete would be refused
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        await Sync([External(1, "Sprint 1"), External(2, "Sprint 2")], ct);

        Guid removedSprintId;
        await using (var seed = new WaydDbContextAccessor(_fixture))
        {
            removedSprintId = await seed.Context.Iterations
                .Where(i => i.OwnershipInfo.SystemId == SystemId && i.OwnershipInfo.ExternalId == "1")
                .Select(i => i.Id)
                .SingleAsync(ct);
            var workspaceId = await SeedWorkspace(seed.Context);
            var workItemId = await InsertWorkItem(seed.Context, workspaceId, removedSprintId);
            await seed.Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Work].[WorkItemStateHistory]
                    ([WorkItemId], [WorkspaceId], [Revision], [ValidFrom], [IterationId], [ExternalIterationId],
                     [StatusName], [WorkTypeName])
                VALUES ({workItemId}, {workspaceId}, 1, SYSUTCDATETIME(), {removedSprintId}, 1, 'Active', 'Sync Story');
                """,
                ct);
        }

        // Act
        await Sync([External(2, "Sprint 2")], ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.Iterations.AnyAsync(i => i.Id == removedSprintId, ct)).Should().BeFalse();
        var period = await verify.Context.WorkItemStateHistory.AsNoTracking().SingleAsync(ct);
        period.IterationId.Should().BeNull();
        period.ExternalIterationId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenAnOpenSprintMovesToATeamWithAnOpenSprint_CompletesTheMovedSprintAndKeepsItsStart()
    {
        // Arrange — each team has started its sprint; then the source moves sprint 2 onto team A
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (azdoTeamA, azdoTeamB) = (Guid.NewGuid(), Guid.NewGuid());
        var teamA = await SeedTeam(ct);
        var teamB = await SeedTeam(ct);
        var mappings = new Dictionary<Guid, Guid?> { [azdoTeamA] = teamA, [azdoTeamB] = teamB };
        await Sync([External(1, "Sprint 1", IterationType.Sprint, azdoTeamA), External(2, "Sprint 2", IterationType.Sprint, azdoTeamB)], ct, mappings);

        var started = SqlServerDbContextFixture.FixedNow.Minus(Duration.FromHours(1));
        await using (var start = new WaydDbContextAccessor(_fixture))
        {
            foreach (var teamId in new[] { teamA, teamB })
            {
                var sprints = await start.Context.Iterations.Where(i => i.TeamId == teamId).ToListAsync(ct);
                var timeline = new TeamSprintTimeline(teamId, sprints, new TeamSprintSchedules([], new SprintSchedule(DateTimeZone.Utc, 1, SizingMethod.Count)));
                sprints.Single().Start(timeline, started, EventActor.System, started).IsSuccess.Should().BeTrue();
            }
            await start.Context.SaveChangesAsync(ct);
        }

        // Act
        await Sync([External(1, "Sprint 1", IterationType.Sprint, azdoTeamA), External(2, "Sprint 2", IterationType.Sprint, azdoTeamA)], ct, mappings);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.Iterations.AsNoTracking()
            .Where(i => i.OwnershipInfo.SystemId == SystemId)
            .ToDictionaryAsync(i => i.OwnershipInfo.ExternalId!, ct);
        saved["1"].Completed.Should().BeNull();
        saved["2"].TeamId.Should().Be(teamA);
        saved["2"].Started.Should().Be(started);
        saved["2"].Completed.Should().Be(SqlServerDbContextFixture.FixedNow);
    }

    private async Task Sync(List<IExternalIteration<AzdoIterationMetadata>> iterations, CancellationToken ct, Dictionary<Guid, Guid?>? teamMappings = null)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new SyncAzureDevOpsIterationsCommandHandler(accessor.Context,
            NullLogger<SyncAzureDevOpsIterationsCommandHandler>.Instance,
            Mock.Of<IDateTimeProvider>(p => p.Now == SqlServerDbContextFixture.FixedNow));

        var result = await handler.Handle(new SyncAzureDevOpsIterationsCommand(SystemId, iterations, teamMappings ?? []), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private static ExternalIteration External(int id, string name, IterationType type = IterationType.Iteration, Guid? azdoTeamId = null) =>
        new(id, name, type, new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 18), IterationState.Completed, azdoTeamId,
            new AzdoIterationMetadata { ProjectId = AzdoProjectId, Identifier = Guid.NewGuid(), Path = $"Project\\{name}" });

    // Straight through SQL: a valid WorkItem needs a workspace, process, type and status that this test
    // never reads.
    private static async Task<Guid> SeedWorkspace(WaydDbContext context)
    {
        var workProcessId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        await context.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Sync Level')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('Sync Level', 'Requirement', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'Sync Story')
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT 'Sync Story', 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Sync Level';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] WHERE [Name] = 'Sync Status')
                INSERT INTO [Work].[WorkStatuses]
                    ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ('Sync Status', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[WorkProcesses]
                ([Id], [Name], [Ownership], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({0}, 'Sync Process', 'Managed', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({1}, 'SYNC', 'Sync Workspace', 'Managed', {0}, 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            [workProcessId, workspaceId], TestContext.Current.CancellationToken);

        return workspaceId;
    }

    private static async Task<Guid> InsertWorkItem(WaydDbContext context, Guid workspaceId, Guid iterationId)
    {
        var id = Guid.CreateVersion7();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @TypeId int = (SELECT TOP 1 [Id] FROM [Work].[WorkTypes] WHERE [Name] = 'Sync Story');
            DECLARE @StatusId int = (SELECT TOP 1 [Id] FROM [Work].[WorkStatuses] WHERE [Name] = 'Sync Status');

            INSERT INTO [Work].[WorkItems]
                ([Id], [Key], [Title], [WorkspaceId], [ExternalId], [TypeId], [StatusId],
                 [StatusCategory], [Created], [LastModified], [StackRank], [IterationId],
                 [SystemCreated], [SystemLastModified])
            VALUES ({id}, 'SYNC-1', 'Sync item', {workspaceId}, 1, @TypeId, @StatusId,
                    'Active', SYSUTCDATETIME(), SYSUTCDATETIME(), 1, {iterationId},
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

private async Task<Guid> SeedTeam(CancellationToken ct)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, $"Team {key}", new TeamCode($"T{key}"), TeamType.Team, true), SqlServerDbContextFixture.FixedNow);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkTeams.Add(team);
        await context.Context.SaveChangesAsync(ct);

        return team.Id;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;

    private sealed record ExternalIteration(int Id, string Name, IterationType Type, LocalDate? Start, LocalDate? End,
        IterationState State, Guid? TeamId, AzdoIterationMetadata Metadata) : IExternalIteration<AzdoIterationMetadata>;
}
