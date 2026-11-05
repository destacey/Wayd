using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Story-Map-Owners-And-Editors migration makes existing maps public and editable by anyone who can view
/// them, turns each map's owner account into an owner employee, and removes the dropped claims — and that its Down
/// puts an owner account back.
/// </summary>
/// <remarks>
/// The migration is hand-written T-SQL over a column the current model no longer has, so it runs against a database
/// of its own, migrated only as far as the migration before it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StoryMapOwnersAndEditorsMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string Previous = "20261102151428_Poker-Session-Facilitators-And-Members";
    private const string Target = "20261102235331_Story-Map-Owners-And-Editors";
    private const string NoPreferences = """{"Tours":"{}"}""";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task UpThenDown_MovesOwnersOntoEmployees_ThenRestoresTheOwner()
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

            var ownerEmployee = Guid.NewGuid();
            await AddEmployee(context, ownerEmployee, "E-1", ct);
            await AddUser(context, "owner-user", ownerEmployee, ct);
            await AddUser(context, "unlinked-user", null, ct);

            var linked = Guid.NewGuid();
            var unlinked = Guid.NewGuid();
            await AddMap(context, linked, "owner-user", ct);
            await AddMap(context, unlinked, "unlinked-user", ct);

            var roleId = Guid.NewGuid().ToString();
            await Execute(context, $"""
                INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName])
                VALUES ('{roleId}', N'Mappers', N'MAPPERS');
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created])
                VALUES ('{roleId}', 'permission', 'Permissions.StoryMaps.View', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.StoryMaps.Create', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.StoryMaps.Update', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.StoryMaps.Delete', SYSUTCDATETIME());
                """, ct);

            // Act
            await migrator.MigrateAsync(Target, ct);

            // Assert
            (await Rows(context, "SELECT DISTINCT [Visibility], [EditAccess] FROM [Planning].[StoryMaps];", ct))
                .Select(r => ((string)r[0], (string)r[1])).Should().Equal(("Public", "AnyoneWhoCanView"));

            var owners = await Rows(context, "SELECT [StoryMapId], [EmployeeId] FROM [Planning].[StoryMapOwners];", ct);
            owners.Select(r => ((Guid)r[0], (Guid)r[1])).Should().Equal((linked, ownerEmployee));

            (await Rows(context, $"SELECT [ClaimValue] FROM [Identity].[RoleClaims] WHERE [RoleId] = '{roleId}' ORDER BY [ClaimValue];", ct))
                .Select(r => (string)r[0]).Should().Equal("Permissions.StoryMaps.Create", "Permissions.StoryMaps.View");

            // The rest of the way, so EF reads the rows through the current model.
            await migrator.MigrateAsync(cancellationToken: ct);
            await using (var read = _fixture.CreateContext(connectionString))
            {
                var map = await read.StoryMaps.AsNoTracking()
                    .Include(m => m.Owners)
                    .SingleAsync(m => m.Id == linked, ct);
                map.Owners.Select(o => o.EmployeeId).Should().Equal(ownerEmployee);
                map.Visibility.Should().Be(Visibility.Public);
                map.EditAccess.Should().Be(StoryMapEditAccess.AnyoneWhoCanView);

                var ownerless = await read.StoryMaps.AsNoTracking()
                    .Include(m => m.Owners)
                    .SingleAsync(m => m.Id == unlinked, ct);
                ownerless.Owners.Should().BeEmpty();
            }

            await migrator.MigrateAsync(Previous, ct);

            (await Scalar<string>(context, $"SELECT [OwnerId] FROM [Planning].[StoryMaps] WHERE [Id] = '{linked}';", ct))
                .Should().Be("owner-user");
            (await Scalar<string>(context, $"SELECT [OwnerId] FROM [Planning].[StoryMaps] WHERE [Id] = '{unlinked}';", ct))
                .Should().Be("creator");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(ct);
        }
    }

    private static Task AddEmployee(WaydDbContext context, Guid id, string number, CancellationToken ct) =>
        Execute(context, $"""
            INSERT INTO [Organization].[Employees] ([Id], [FirstName], [LastName], [EmployeeNumber], [Email], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ('{id}', N'First', N'Last', N'{number}', N'{number}@acme.example', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);

    private static Task AddUser(WaydDbContext context, string id, Guid? employeeId, CancellationToken ct) =>
        Execute(context, $"""
            INSERT INTO [Identity].[Users] ([Id], [UserName], [NormalizedUserName], [EmployeeId], [LoginProvider], [IsActive], [MustChangePassword],
                [EmailConfirmed], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount], [Preferences])
            VALUES (N'{id}', N'{id}', N'{id.ToUpperInvariant()}', {(employeeId is null ? "NULL" : $"'{employeeId}'")}, N'Wayd', 1, 0, 1, 0, 0, 0, 0, N'{NoPreferences}');
            """, ct);

    private static Task AddMap(WaydDbContext context, Guid id, string ownerId, CancellationToken ct) =>
        Execute(context, $"""
            INSERT INTO [Planning].[StoryMaps] ([Id], [Name], [OwnerId], [Status], [IsDeleted], [SystemCreated], [SystemCreatedBy], [SystemLastModified])
            VALUES ('{id}', N'Checkout', N'{ownerId}', 'Active', 0, '2026-01-03', N'creator', '2026-01-03');
            """, ct);

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

    private static async Task<T> Scalar<T>(WaydDbContext context, string sql, CancellationToken ct)
    {
        var connection = await Open(context, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync(ct))!;
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
