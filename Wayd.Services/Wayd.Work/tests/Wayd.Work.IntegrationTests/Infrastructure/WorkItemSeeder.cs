using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds the minimum graph the attribution tests need, straight through SQL.
/// </summary>
/// <remarks>
/// Deliberately not built through the aggregates: a valid <c>WorkItem</c> needs a workspace, a
/// work process, a work type with a level, and a status — none of which the command under test
/// reads. Building that graph through the domain would make these tests fail for reasons that have
/// nothing to do with the attribution query they exist to verify. The columns written here are
/// exactly the non-nullable ones the migrations declare, so a schema change that adds a required
/// column will fail these loudly rather than silently drifting.
/// </remarks>
public static class WorkItemSeeder
{
    public sealed record SeededIds(Guid EmployeeId, Guid MatchingWorkItemId, Guid OtherWorkItemId);

    public static async Task<SeededIds> Seed(
        SqlServerDbContextFixture fixture,
        string matchingExternalId,
        string otherExternalId,
        CancellationToken cancellationToken)
    {
        var employeeId = Guid.CreateVersion7();
        var workProcessId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();
        var matchingId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();

        await using var context = fixture.CreateContext();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [Organization].[Employees]
                ([Id], [FirstName], [LastName], [EmployeeNumber], [Email], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({0}, 'Avery', 'Chen', 'E-4471', 'avery.chen@acme.example', 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            [employeeId], cancellationToken);

        // Identity columns, so the ids are read back rather than assumed.
        await context.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Seed Level')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('Seed Level', 'Other', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'Seed Type')
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT 'Seed Type', 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'Seed Level';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] WHERE [Name] = 'Seed Status')
                INSERT INTO [Work].[WorkStatuses]
                    ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ('Seed Status', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            [], cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [Work].[WorkProcesses]
                ([Id], [Name], [Ownership], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({0}, 'Seed Process', 'Managed', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({1}, 'SEEDWS', 'Seed Workspace', 'Managed', {0}, 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            [workProcessId, workspaceId], cancellationToken);

        await InsertWorkItem(context, matchingId, workspaceId, 101, matchingExternalId, cancellationToken);
        await InsertWorkItem(context, otherId, workspaceId, 102, otherExternalId, cancellationToken);

        return new SeededIds(employeeId, matchingId, otherId);
    }

    private static Task InsertWorkItem(
        WaydDbContext context,
        Guid id,
        Guid workspaceId,
        int externalId,
        string assignedToExternalId,
        CancellationToken cancellationToken) =>
        // All three attribution columns start null: these represent items whose sync could not
        // resolve the person, which is the state a mapping is supposed to repair. The same
        // external id is used for all three so one seeded item exercises every column.
        context.Database.ExecuteSqlRawAsync(
            """
            DECLARE @TypeId int = (SELECT TOP 1 [Id] FROM [Work].[WorkTypes] WHERE [Name] = 'Seed Type');
            DECLARE @StatusId int = (SELECT TOP 1 [Id] FROM [Work].[WorkStatuses] WHERE [Name] = 'Seed Status');

            INSERT INTO [Work].[WorkItems]
                ([Id], [Key], [Title], [WorkspaceId], [ExternalId], [TypeId], [StatusId],
                 [StatusCategory], [Created], [LastModified], [StackRank],
                 [AssignedToId], [CreatedById], [LastModifiedById],
                 [SystemCreated], [SystemLastModified])
            VALUES ({0}, {1}, 'Seeded item', {2}, {3}, @TypeId, @StatusId, 'Proposed',
                    SYSUTCDATETIME(), SYSUTCDATETIME(), 1, NULL, NULL, NULL,
                    SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[WorkItemsExtended]
                ([Id], [AssignedToExternalId], [CreatedByExternalId], [LastModifiedByExternalId])
            VALUES ({0}, {4}, {4}, {4});
            """,
            [id, $"SEED-{externalId}", workspaceId, externalId, assignedToExternalId],
            cancellationToken);
}
