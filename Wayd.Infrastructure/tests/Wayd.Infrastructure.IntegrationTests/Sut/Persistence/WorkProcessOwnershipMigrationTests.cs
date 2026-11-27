using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Work.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Move-WorkProcess-To-OwnershipInfo migration carries each work process's external id across as the
/// text the application compares, names Azure DevOps as its connector, leaves a Wayd-owned process without a
/// reference, and keeps one work process per external id — and that its Down restores the GUID column.
/// </summary>
/// <remarks>
/// The migration changes a column's type under existing rows, and creates a unique index the EF model cannot
/// describe, so neither is exercised by a database built from the current model. It runs against a database
/// of its own, migrated only as far as the migration before it.
/// </remarks>
public sealed class WorkProcessOwnershipMigrationTests(SqlServerMigrationFixture fixture) : IClassFixture<SqlServerMigrationFixture>
{
    private const string Previous = "20261126150905_Backfill-Message-Notification-Event-Ids";
    private const string Target = "20261127161348_Move-WorkProcess-To-OwnershipInfo";

    private readonly SqlServerMigrationFixture _fixture = fixture;

    [Fact]
    public async Task UpThenDown_CarriesTheExternalIdAcrossAsText_ThenRestoresTheGuid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.MigratedTo(Previous, ct);

        await using var context = _fixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();

        var managedId = Guid.NewGuid();
        var externalProcessId = Guid.Parse("adcc42ab-9882-485e-a3ed-7678f01f66bc");
        var ownedId = Guid.NewGuid();
        // Deleted, with the id the active process has: the unique index leaves deleted rows out.
        var deletedId = Guid.NewGuid();
        await Execute(context, $"""
            INSERT INTO [Work].[WorkProcesses] ([Id], [Name], [Ownership], [ExternalId], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ('{managedId}', N'Agile', 'Managed', '{externalProcessId}', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
                   ('{ownedId}', N'In House', 'Owned', NULL, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
                   ('{deletedId}', N'Removed', 'Managed', '{externalProcessId}', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);

        // Act
        await migrator.MigrateAsync(Target, ct);

        // Assert
        var rows = await Rows(context, "SELECT [Id], [Ownership], [Connector], [SystemId], [ExternalId] FROM [Work].[WorkProcesses] ORDER BY [Name];", ct);
        rows.Select(r => ((Guid)r[0], (string)r[1], r[2] as string, r[3] as string, r[4] as string)).Should().Equal(
            (managedId, "Managed", "AzureDevOps", null, "adcc42ab-9882-485e-a3ed-7678f01f66bc"),
            (ownedId, "Owned", null, null, null),
            (deletedId, "Managed", "AzureDevOps", null, "adcc42ab-9882-485e-a3ed-7678f01f66bc"));

        // Compared byte for byte: the database's own comparison ignores case, and the application's does not.
        var notLowerCase = await Rows(context, "SELECT COUNT(*) FROM [Work].[WorkProcesses] WHERE [ExternalId] <> LOWER([ExternalId]) COLLATE Latin1_General_BIN2;", ct);
        notLowerCase.Single()[0].Should().Be(0);

        await using (var read = _fixture.CreateContext(connectionString))
        {
            var externalId = WorkProcess.ToExternalId(externalProcessId);
            var managed = await read.WorkProcesses.AsNoTracking().SingleAsync(p => p.OwnershipInfo.ExternalId == externalId, ct);
            managed.Id.Should().Be(managedId);
            managed.OwnershipInfo.Ownership.Should().Be(Ownership.Managed);
            managed.OwnershipInfo.Connector.Should().Be(Connector.AzureDevOps);
            managed.ExternalProcessId.Should().Be(externalProcessId);

            var owned = await read.WorkProcesses.AsNoTracking().SingleAsync(p => p.Id == ownedId, ct);
            owned.OwnershipInfo.Ownership.Should().Be(Ownership.Owned);
            owned.OwnershipInfo.HasExternalReference.Should().BeFalse();
        }

        var duplicate = () => Execute(context, $"""
            INSERT INTO [Work].[WorkProcesses] ([Id], [Name], [Ownership], [Connector], [ExternalId], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ('{Guid.NewGuid()}', N'Agile Again', 'Managed', 'AzureDevOps', '{externalProcessId}', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);
        (await duplicate.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(2601);

        await migrator.MigrateAsync(Previous, ct);

        var restored = await Rows(context, "SELECT [Id], [Ownership], [ExternalId] FROM [Work].[WorkProcesses] ORDER BY [Name];", ct);
        restored.Select(r => ((Guid)r[0], (string)r[1], r[2] as Guid?)).Should().Equal(
            (managedId, "Managed", externalProcessId),
            (ownedId, "Owned", null),
            (deletedId, "Managed", externalProcessId));
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
