using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Poker-Session-Facilitators-And-Members migration makes existing sessions public, turns each session's
/// facilitator account into a facilitator employee, adds everyone who voted as a voting member, and removes the
/// dropped claims — and that its Down puts a facilitator account back.
/// </summary>
/// <remarks>
/// The migration is hand-written T-SQL over a column the current model no longer has, so it runs against a database
/// of its own, migrated only as far as the migration before it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class PokerSessionFacilitatorsAndMembersMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string Previous = "20261101221339_Backfill-Estimation-Scale-Baseline-Activity";
    private const string Target = "20261102151428_Poker-Session-Facilitators-And-Members";
    private const string NoPreferences = """{"Tours":"{}"}""";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task UpThenDown_MovesFacilitatorsAndVotersOntoEmployees_ThenRestoresTheFacilitator()
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

            var facilitatorEmployee = Guid.NewGuid();
            var voterEmployee = Guid.NewGuid();
            await AddEmployee(context, facilitatorEmployee, "E-1", ct);
            await AddEmployee(context, voterEmployee, "E-2", ct);
            await AddUser(context, "facilitator-user", facilitatorEmployee, ct);
            await AddUser(context, "voter-user", voterEmployee, ct);
            await AddUser(context, "unlinked-user", null, ct);

            var scaleId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            await Execute(context, $$"""
                INSERT INTO [Planning].[EstimationScales] ([Id], [Name], [IsActive], [SystemCreated], [SystemLastModified])
                VALUES ('{{scaleId}}', N'Fibonacci', 1, '2026-01-01', '2026-01-01');
                INSERT INTO [Planning].[EstimationScaleVersions] ([Id], [EstimationScaleId], [Number], [PublishedOn], [Values])
                VALUES ('{{versionId}}', '{{scaleId}}', 1, '2026-01-01', N'[{"Value":"1","IsEstimate":true}]');
                """, ct);

            var linked = Guid.NewGuid();
            var unlinked = Guid.NewGuid();
            await AddSession(context, linked, "facilitator-user", scaleId, versionId, ct);
            await AddSession(context, unlinked, "unlinked-user", scaleId, versionId, ct);

            var round = Guid.NewGuid();
            var secondRound = Guid.NewGuid();
            await Execute(context, $"""
                INSERT INTO [Planning].[PokerRounds] ([Id], [PokerSessionId], [Status], [Order])
                VALUES ('{round}', '{linked}', 'Revealed', 0), ('{secondRound}', '{linked}', 'Voting', 1);
                INSERT INTO [Planning].[PokerVotes] ([Id], [PokerRoundId], [ParticipantId], [Value], [SubmittedOn])
                VALUES (NEWID(), '{round}', N'voter-user', N'1', '2026-01-03T10:05:00'),
                       (NEWID(), '{secondRound}', N'voter-user', N'1', '2026-01-03T10:01:00'),
                       (NEWID(), '{round}', N'unlinked-user', N'1', '2026-01-03T10:02:00');
                """, ct);

            var roleId = Guid.NewGuid().ToString();
            await Execute(context, $"""
                INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName])
                VALUES ('{roleId}', N'Planners', N'PLANNERS');
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created])
                VALUES ('{roleId}', 'permission', 'Permissions.PokerSessions.View', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.PokerSessions.Update', SYSUTCDATETIME()),
                       ('{roleId}', 'permission', 'Permissions.PokerSessions.Delete', SYSUTCDATETIME());
                """, ct);

            // Act
            await migrator.MigrateAsync(Target, ct);

            // Assert
            (await Rows(context, "SELECT DISTINCT [Visibility] FROM [Planning].[PokerSessions];", ct))
                .Select(r => (string)r[0]).Should().Equal("Public");

            var facilitators = await Rows(context, "SELECT [PokerSessionId], [EmployeeId] FROM [Planning].[PokerSessionFacilitators];", ct);
            facilitators.Select(r => ((Guid)r[0], (Guid)r[1])).Should().Equal((linked, facilitatorEmployee));

            var members = await Rows(context, "SELECT [PokerSessionId], [EmployeeId], [IsVoting], [JoinedOn] FROM [Planning].[PokerSessionMembers];", ct);
            var member = members.Should().ContainSingle().Subject;
            member[0].Should().Be(linked);
            member[1].Should().Be(voterEmployee);
            member[2].Should().Be(true);
            member[3].Should().Be(new DateTime(2026, 1, 3, 10, 1, 0));

            (await Rows(context, $"SELECT [ClaimValue] FROM [Identity].[RoleClaims] WHERE [RoleId] = '{roleId}';", ct))
                .Select(r => (string)r[0]).Should().Equal("Permissions.PokerSessions.View");

            // The rest of the way, so EF reads the rows through the current model.
            await migrator.MigrateAsync(cancellationToken: ct);
            await using (var read = _fixture.CreateContext(connectionString))
            {
                var session = await read.PokerSessions.AsNoTracking()
                    .Include(s => s.Facilitators)
                    .Include(s => s.Members)
                    .SingleAsync(s => s.Id == linked, ct);
                session.Facilitators.Select(f => f.EmployeeId).Should().Equal(facilitatorEmployee);
                session.Members.Select(m => m.EmployeeId).Should().Equal(voterEmployee);
            }

            await migrator.MigrateAsync(Previous, ct);

            (await Scalar<string>(context, $"SELECT [FacilitatorId] FROM [Planning].[PokerSessions] WHERE [Id] = '{linked}';", ct))
                .Should().Be("facilitator-user");
            (await Scalar<string>(context, $"SELECT [FacilitatorId] FROM [Planning].[PokerSessions] WHERE [Id] = '{unlinked}';", ct))
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

    private static Task AddSession(WaydDbContext context, Guid id, string facilitatorId, Guid scaleId, Guid versionId, CancellationToken ct) =>
        Execute(context, $"""
            INSERT INTO [Planning].[PokerSessions] ([Id], [Name], [EstimationScaleId], [EstimationScaleVersionId], [FacilitatorId], [Status], [ActivatedOn], [SystemCreated], [SystemCreatedBy], [SystemLastModified])
            VALUES ('{id}', N'Refinement', '{scaleId}', '{versionId}', N'{facilitatorId}', 'Active', '2026-01-03', '2026-01-03', N'creator', '2026-01-03');
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
