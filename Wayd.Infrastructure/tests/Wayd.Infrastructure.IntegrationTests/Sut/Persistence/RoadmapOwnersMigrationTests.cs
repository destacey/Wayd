using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Rename-Roadmap-Managers-To-Owners migration keeps every roadmap's managers as its owners, gives the
/// table's key, foreign keys and indexes the names a fresh table would have, and removes the dropped claims — and
/// that its Down puts the managers table back.
/// </summary>
/// <remarks>
/// The migration renames a table the current model no longer has, so it runs against a database of its own,
/// migrated only as far as the migration before it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RoadmapOwnersMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string Previous = "20261102235331_Story-Map-Owners-And-Editors";
    private const string Target = "20261103143924_Rename-Roadmap-Managers-To-Owners";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task UpThenDown_KeepsManagersAsOwners_ThenRestoresTheManagersTable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = $"wayd-migration-{Guid.NewGuid():N}",
        }.ConnectionString;

        await using var context = _fixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync(Previous, ct);

            var managerEmployee = Guid.NewGuid();
            await Execute(context, $"""
                INSERT INTO [Organization].[Employees] ([Id], [FirstName], [LastName], [EmployeeNumber], [Email], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ('{managerEmployee}', N'First', N'Last', N'E-1', N'e-1@acme.example', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
                """, ct);

            var roadmapId = Guid.NewGuid();
            await Execute(context, $"""
                INSERT INTO [Planning].[Roadmaps] ([Id], [Name], [State], [Visibility], [Start], [End], [SystemCreated], [SystemLastModified])
                VALUES ('{roadmapId}', N'Platform', 'Active', 'Private', '2026-01-01', '2026-12-31', SYSUTCDATETIME(), SYSUTCDATETIME());
                INSERT INTO [Planning].[RoadmapManagers] ([RoadmapId], [ManagerId])
                VALUES ('{roadmapId}', '{managerEmployee}');
                """, ct);

            var roleId = Guid.NewGuid().ToString();
            await Execute(context, $"""
                INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName])
                VALUES ('{roleId}', N'Planners', N'PLANNERS');
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created])
                VALUES ('{roleId}', 'permission', 'Permissions.Roadmaps.View', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.Roadmaps.Create', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.Roadmaps.Update', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.Roadmaps.Delete', SYSUTCDATETIME());
                """, ct);

            // Act
            await migrator.MigrateAsync(Target, ct);

            // Assert
            var owners = await Rows(context, "SELECT [RoadmapId], [EmployeeId] FROM [Planning].[RoadmapOwners];", ct);
            owners.Select(r => ((Guid)r[0], (Guid)r[1])).Should().Equal((roadmapId, managerEmployee));

            (await Rows(context, "SELECT [name] FROM sys.objects WHERE [parent_object_id] = OBJECT_ID(N'[Planning].[RoadmapOwners]') ORDER BY [name];", ct))
                .Select(r => (string)r[0]).Should().Equal(
                    "FK_RoadmapOwners_Employees_EmployeeId",
                    "FK_RoadmapOwners_Roadmaps_RoadmapId",
                    "PK_RoadmapOwners");

            (await Rows(context, "SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[Planning].[RoadmapOwners]') AND [is_primary_key] = 0 ORDER BY [name];", ct))
                .Select(r => (string)r[0]).Should().Equal("IX_RoadmapOwners_EmployeeId", "IX_RoadmapOwners_RoadmapId");

            (await Rows(context, $"SELECT [ClaimValue] FROM [Identity].[RoleClaims] WHERE [RoleId] = '{roleId}' ORDER BY [ClaimValue];", ct))
                .Select(r => (string)r[0]).Should().Equal("Permissions.Roadmaps.Create", "Permissions.Roadmaps.View");

            // The rest of the way, so EF reads the rows through the current model.
            await migrator.MigrateAsync(cancellationToken: ct);
            await using (var read = _fixture.CreateContext(connectionString))
            {
                var roadmap = await read.Roadmaps.AsNoTracking()
                    .Include(r => r.Owners)
                    .SingleAsync(r => r.Id == roadmapId, ct);
                roadmap.Owners.Select(o => o.EmployeeId).Should().Equal(managerEmployee);
            }

            await migrator.MigrateAsync(Previous, ct);

            var managers = await Rows(context, "SELECT [RoadmapId], [ManagerId] FROM [Planning].[RoadmapManagers];", ct);
            managers.Select(r => ((Guid)r[0], (Guid)r[1])).Should().Equal((roadmapId, managerEmployee));

            (await Rows(context, "SELECT [name] FROM sys.objects WHERE [parent_object_id] = OBJECT_ID(N'[Planning].[RoadmapManagers]') ORDER BY [name];", ct))
                .Select(r => (string)r[0]).Should().Equal(
                    "FK_RoadmapManagers_Employees_ManagerId",
                    "FK_RoadmapManagers_Roadmaps_RoadmapId",
                    "PK_RoadmapManagers");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(ct);
        }
    }

    private static async Task<DbConnection> Open(WaydDbContext context, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task Execute(WaydDbContext context, string sql, CancellationToken ct)
    {
        var connection = await Open(context, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<object[]>> Rows(WaydDbContext context, string sql, CancellationToken ct)
    {
        var connection = await Open(context, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object[]>();
        while (await reader.ReadAsync(ct))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }
        return rows;
    }
}
