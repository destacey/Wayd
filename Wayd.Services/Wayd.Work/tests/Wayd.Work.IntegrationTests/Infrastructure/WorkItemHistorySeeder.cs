using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds managed workspaces and work items for the work item history tests, straight through SQL.
/// </summary>
/// <remarks>
/// A valid WorkItem needs a workspace, process, type and status that the history tests never read
/// through the aggregates. Every workspace shares one process, the "History Story" type and the
/// "History Status" status.
/// </remarks>
public static class WorkItemHistorySeeder
{
    public const string WorkTypeName = "History Story";
    public const string StatusName = "History Status";

    /// <summary>A managed workspace in source system <paramref name="systemId"/>, with its own process.</summary>
    public static async Task<Guid> SeedWorkspace(SqlServerDbContextFixture fixture, string systemId, string key, CancellationToken ct)
    {
        var workProcessId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'History Level')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('History Level', 'Requirement', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = {WorkTypeName})
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT {WorkTypeName}, 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'History Level';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] WHERE [Name] = {StatusName})
                INSERT INTO [Work].[WorkStatuses]
                    ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ({StatusName}, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[WorkProcesses]
                ([Id], [Name], [Ownership], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({workProcessId}, 'History Process', 'Managed', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [SystemId], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({workspaceId}, {key}, {key + " Workspace"}, 'Managed', {systemId}, {workProcessId}, 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            ct);

        return workspaceId;
    }

    /// <summary>A second managed workspace in the same source system, sharing the seeded workspace's process.</summary>
    public static async Task<Guid> SeedSiblingWorkspace(SqlServerDbContextFixture fixture, Guid seededWorkspaceId, string key, CancellationToken ct)
    {
        var workspaceId = Guid.CreateVersion7();

        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [SystemId], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            SELECT {workspaceId}, {key}, {key + " Workspace"}, 'Managed', [SystemId], [WorkProcessId], 1, 0,
                   SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM [Work].[Workspaces] WHERE [Id] = {seededWorkspaceId};
            """,
            ct);

        return workspaceId;
    }

    /// <summary>A work item in <paramref name="workspaceId"/> with the source id <paramref name="externalId"/>.</summary>
    public static async Task<Guid> SeedWorkItem(SqlServerDbContextFixture fixture, Guid workspaceId, int externalId, CancellationToken ct)
    {
        var workItemId = Guid.CreateVersion7();

        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @TypeId int = (SELECT TOP 1 [Id] FROM [Work].[WorkTypes] WHERE [Name] = {WorkTypeName});
            DECLARE @StatusId int = (SELECT TOP 1 [Id] FROM [Work].[WorkStatuses] WHERE [Name] = {StatusName});

            INSERT INTO [Work].[WorkItems]
                ([Id], [Key], [Title], [WorkspaceId], [ExternalId], [TypeId], [StatusId],
                 [StatusCategory], [Created], [LastModified], [StackRank],
                 [SystemCreated], [SystemLastModified])
            VALUES ({workItemId}, {"HIST-" + externalId}, 'History item', {workspaceId}, {externalId}, @TypeId, @StatusId,
                    'Done', SYSUTCDATETIME(), SYSUTCDATETIME(), 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            ct);

        return workItemId;
    }

    /// <summary>A Wayd-owned sprint with no team, planned from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static async Task<Guid> SeedSprint(SqlServerDbContextFixture fixture, LocalDate start, LocalDate end, CancellationToken ct)
    {
        var sprint = Iteration.Create("History Sprint", IterationType.Sprint,
            new IterationDateRange(start, end), null,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, SqlServerDbContextFixture.FixedNow);

        await using var accessor = new WaydDbContextAccessor(fixture);
        accessor.Context.Iterations.Add(sprint);
        await accessor.Context.SaveChangesAsync(ct);

        return sprint.Id;
    }

    /// <summary>Records that the workspace's history has been read through to the end.</summary>
    public static async Task MarkHistoryReadToEnd(SqlServerDbContextFixture fixture, Guid workspaceId, CancellationToken ct)
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Work].[Workspaces] SET [WorkItemHistoryBackfilledOn] = SYSUTCDATETIME() WHERE [Id] = {workspaceId};", ct);
    }

    /// <summary>One history period of a "History Story" item, with the status category as its status name.</summary>
    public static async Task SeedPeriod(SqlServerDbContextFixture fixture, Guid workItemId, Guid workspaceId, int revision, Instant from, Instant? to, Guid? iterationId, string statusCategory, CancellationToken ct, double? storyPoints = null)
    {
        var validFrom = from.ToDateTimeUtc();
        var validTo = to?.ToDateTimeUtc();

        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Work].[WorkItemStateHistory]
                ([WorkItemId], [WorkspaceId], [Revision], [ValidFrom], [ValidTo], [IterationId],
                 [StatusName], [StatusCategory], [WorkTypeId], [WorkTypeName], [StoryPoints])
            SELECT {workItemId}, {workspaceId}, {revision}, {validFrom}, {validTo}, {iterationId},
                   {statusCategory}, {statusCategory}, [Id], [Name], {storyPoints}
            FROM [Work].[WorkTypes] WHERE [Name] = {WorkTypeName};
            """,
            ct);
    }
}
