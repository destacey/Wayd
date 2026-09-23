using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.BacklogHealth;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler reads a work item's parent status and sprint state
/// through navigations in a projection, and projects the grid rows with Mapster. The fakes leave those
/// navigations unset and never translate a query, so neither is visible to the unit tests.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamBacklogHealthQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 22, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReadsParentStatusAndSprintStateFromTheDatabase()
    {
        // Arrange
        MapsterConfiguration.Ensure();
        await _fixture.ResetWorkData(TestContext.Current.CancellationToken);
        var teamId = await SeedTeam();
        var completedSprintId = await SeedSprint(teamId, IterationState.Completed);
        var activeSprintId = await SeedSprint(teamId, IterationState.Active);

        Guid closedParent, carriedOver, current;
        await using (var seed = new WaydDbContextAccessor(_fixture))
        {
            var workspaceId = await SeedWorkspace(seed.Context);
            closedParent = await InsertWorkItem(seed.Context, workspaceId, 1, "Done", teamId: null);
            carriedOver = await InsertWorkItem(seed.Context, workspaceId, 2, "Proposed", teamId, parentId: closedParent, iterationId: completedSprintId, stackRank: 1);
            current = await InsertWorkItem(seed.Context, workspaceId, 3, "Active", teamId, iterationId: activeSprintId, stackRank: 2);
        }

        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamMemberCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var dateTimeProvider = Mock.Of<IDateTimeProvider>(p => p.Now == _now);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamBacklogHealthQueryHandler(accessor.Context, dispatcher.Object, dateTimeProvider);

        // Act
        var result = await handler.Handle(
            new GetTeamBacklogHealthQuery(teamId, BacklogHealthThresholds.Default),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var health = result.Value!;
        health.WorkItems.Select(w => w.Id).Should().Equal(carriedOver, current);

        var carriedOverRow = health.WorkItems[0];
        carriedOverRow.Key.Should().Be("HEALTH-2");
        carriedOverRow.Type.Should().Be("Health Story");
        carriedOverRow.Status.Should().Be("Health Status");
        carriedOverRow.Parent!.Id.Should().Be(closedParent);
        carriedOverRow.Sprint!.Id.Should().Be(completedSprintId);
        Flags(carriedOverRow).Should().Contain([BacklogHealthCheck.CarryOver, BacklogHealthCheck.ClosedParent]);

        Flags(health.WorkItems[1]).Should().NotContain([BacklogHealthCheck.CarryOver, BacklogHealthCheck.ClosedParent]);
        Check(health, BacklogHealthCheck.CarryOver).InScope.Should().Be(2);
        Check(health, BacklogHealthCheck.ClosedParent).InScope.Should().Be(1);
    }

    private static IEnumerable<BacklogHealthCheck> Flags(BacklogHealthWorkItemDto row) =>
        row.Flags.Select(f => (BacklogHealthCheck)f.Id);

    private static BacklogHealthCheckDto Check(TeamBacklogHealthDto health, BacklogHealthCheck check) =>
        health.Checks.Single(c => c.Check.Id == (int)check);

    private async Task<Guid> SeedTeam()
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, "Atlas", new TeamCode($"T{key}"), TeamType.Team, true), _now);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkTeams.Add(team);
        await context.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return team.Id;
    }

    private async Task<Guid> SeedSprint(Guid teamId, IterationState state)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var range = new IterationDateRange(Instant.FromUtc(2026, 9, 1, 0, 0), Instant.FromUtc(2026, 9, 14, 0, 0));
        var sprint = new WorkIteration(
            new SourceIteration(Guid.NewGuid(), key, $"Sprint {key}", IterationType.Sprint, state, range, teamId),
            _now);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkIterations.Add(sprint);
        await context.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return sprint.Id;
    }

    // Straight through SQL, like WorkItemSeeder: a valid WorkItem needs a workspace, process, type
    // and status that the query only reads by name.
    private static async Task<Guid> SeedWorkspace(WaydDbContext context)
    {
        var workProcessId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        await context.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Health Level')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('Health Level', 'Requirement', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'Health Story')
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT 'Health Story', 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Health Level';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] WHERE [Name] = 'Health Status')
                INSERT INTO [Work].[WorkStatuses]
                    ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ('Health Status', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[WorkProcesses]
                ([Id], [Name], [Ownership], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({0}, 'Health Process', 'Managed', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({1}, 'HEALTH', 'Health Workspace', 'Managed', {0}, 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            [workProcessId, workspaceId], TestContext.Current.CancellationToken);

        return workspaceId;
    }

    private static async Task<Guid> InsertWorkItem(
        WaydDbContext context,
        Guid workspaceId,
        int number,
        string statusCategory,
        Guid? teamId,
        Guid? parentId = null,
        Guid? iterationId = null,
        double stackRank = 1)
    {
        var id = Guid.CreateVersion7();

        var key = $"HEALTH-{number}";

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @TypeId int = (SELECT TOP 1 [Id] FROM [Work].[WorkTypes] WHERE [Name] = 'Health Story');
            DECLARE @StatusId int = (SELECT TOP 1 [Id] FROM [Work].[WorkStatuses] WHERE [Name] = 'Health Status');

            INSERT INTO [Work].[WorkItems]
                ([Id], [Key], [Title], [WorkspaceId], [ExternalId], [TypeId], [StatusId],
                 [StatusCategory], [Created], [LastModified], [StackRank],
                 [TeamId], [ParentId], [IterationId],
                 [SystemCreated], [SystemLastModified])
            VALUES ({id}, {key}, 'Health item', {workspaceId}, {number}, @TypeId, @StatusId,
                    {statusCategory}, SYSUTCDATETIME(), SYSUTCDATETIME(), {stackRank},
                    {teamId}, {parentId}, {iterationId},
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            TestContext.Current.CancellationToken);

        return id;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;

    private sealed record SourceIteration(
        Guid Id, int Key, string Name, IterationType Type, IterationState State, IterationDateRange DateRange, Guid? TeamId)
        : ISimpleIteration;
}
