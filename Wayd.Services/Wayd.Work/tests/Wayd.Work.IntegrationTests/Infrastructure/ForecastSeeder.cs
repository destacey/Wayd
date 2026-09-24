using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds forecast scenarios: teams, a workspace with story and epic types, work items with the
/// columns forecasts read, completion history, dependencies, references and projects.
/// </summary>
/// <remarks>
/// Work items go straight through SQL, like <see cref="WorkItemSeeder"/>: a valid WorkItem needs a
/// process, types and statuses the forecast only reads by tier. Replicas, dependencies and
/// references go through their domain constructors, since the forecast reads their shapes.
/// </remarks>
public sealed class ForecastSeeder(SqlServerDbContextFixture fixture)
{
    public const string WorkspaceKey = "FCAST";

    /// <summary>The forecasts' "now"; completion history is laid out on the days before it.</summary>
    public static readonly Instant Now = Instant.FromUtc(2026, 9, 22, 12, 0);

    /// <summary>The first simulated day for <see cref="Now"/>.</summary>
    public static readonly LocalDate Start = new(2026, 9, 22);

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private Guid _workspaceId;
    private int _nextNumber = 1;

    public async Task Reset(CancellationToken cancellationToken)
    {
        await _fixture.ResetWorkData(cancellationToken);

        _workspaceId = Guid.CreateVersion7();
        var workProcessId = Guid.CreateVersion7();

        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Forecast Stories')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('Forecast Stories', 'Requirement', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Forecast Epics')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('Forecast Epics', 'Portfolio', 0, 2, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'Forecast Story')
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT 'Forecast Story', 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Forecast Stories';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'Forecast Epic')
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT 'Forecast Epic', 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Forecast Epics';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] WHERE [Name] = 'Forecast Status')
                INSERT INTO [Work].[WorkStatuses]
                    ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ('Forecast Status', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[WorkProcesses]
                ([Id], [Name], [Ownership], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({0}, 'Forecast Process', 'Managed', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({1}, 'FCAST', 'Forecast Workspace', 'Managed', {0}, 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            [workProcessId, _workspaceId], cancellationToken);
    }

    public async Task<(Guid Id, string Code)> AddTeam(CancellationToken cancellationToken)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var code = $"F{key}";
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, $"Team {key}", new TeamCode(code), TeamType.Team, true), Now);

        await using var context = _fixture.CreateContext();
        context.WorkTeams.Add(team);
        await context.SaveChangesAsync(cancellationToken);

        return (team.Id, code);
    }

    public async Task<Guid> AddProject(CancellationToken cancellationToken)
    {
        var project = new WorkProject(new SourceProject(Guid.NewGuid(), new ProjectKey($"FP{Random.Shared.Next(1000, 9999)}"), "Forecast Project", "A project to forecast."), Now);

        await using var context = _fixture.CreateContext();
        context.WorkProjects.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        return project.Id;
    }

    /// <summary>One item finished on each of the given number of days before today.</summary>
    public async Task AddHistory(Guid teamId, CancellationToken cancellationToken, int days = 90)
    {
        for (var day = 1; day <= days; day++)
            await AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Now.Minus(Duration.FromDays(day)));
    }

    public async Task<(Guid Id, string Key)> AddItem(
        Guid? teamId,
        CancellationToken cancellationToken,
        WorkStatusCategory statusCategory = WorkStatusCategory.Proposed,
        double stackRank = 1,
        bool epic = false,
        Instant? done = null,
        Guid? parentId = null,
        Guid? projectId = null,
        Guid? parentProjectId = null)
    {
        var id = Guid.CreateVersion7();
        var number = _nextNumber++;
        var key = $"{WorkspaceKey}-{number}";
        var typeName = epic ? "Forecast Epic" : "Forecast Story";
        var category = statusCategory.ToString();
        // Rows are created in order, so creation date breaks rank ties the way it would in a sync.
        var created = Instant.FromUtc(2026, 1, 1, 0, 0).Plus(Duration.FromMinutes(number)).ToDateTimeUtc();
        var doneAt = done?.ToDateTimeUtc();

        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @TypeId int = (SELECT TOP 1 [Id] FROM [Work].[WorkTypes] WHERE [Name] = {typeName});
            DECLARE @StatusId int = (SELECT TOP 1 [Id] FROM [Work].[WorkStatuses] WHERE [Name] = 'Forecast Status');

            INSERT INTO [Work].[WorkItems]
                ([Id], [Key], [Title], [WorkspaceId], [ExternalId], [TypeId], [StatusId],
                 [StatusCategory], [Created], [LastModified], [StackRank], [DoneTimestamp],
                 [TeamId], [ParentId], [ProjectId], [ParentProjectId],
                 [SystemCreated], [SystemLastModified])
            VALUES ({id}, {key}, {"Item " + key}, {_workspaceId}, {number}, @TypeId, @StatusId,
                    {category}, {created}, {created}, {stackRank}, {doneAt},
                    {teamId}, {parentId}, {projectId}, {parentProjectId},
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            cancellationToken);

        return (id, key);
    }

    public async Task AddDependency(Guid predecessorId, Guid successorId, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var infos = await context.WorkItems
            .Where(w => w.Id == predecessorId || w.Id == successorId)
            .Select(w => new DependencyWorkItemInfo { WorkItemId = w.Id, StatusCategory = w.StatusCategory })
            .ToListAsync(cancellationToken);

        context.WorkItemDependencies.Add(WorkItemDependency.Create(
            infos.Single(i => i.WorkItemId == predecessorId),
            infos.Single(i => i.WorkItemId == successorId),
            Now, null, null, null, null, Now));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddReference(Guid workItemId, Guid objectId, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        context.WorkItemReferences.Add(WorkItemReference.Create(workItemId, objectId, SystemContext.PlanningPlanningIntervalObjective));
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;

    private sealed record SourceProject(Guid Id, ProjectKey Key, string Name, string Description) : ISimpleProject;
}
