using Microsoft.EntityFrameworkCore;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Tests.Containers;

namespace Wayd.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>
/// Creates a SQL Server database of its own (see <see cref="SqlServerTestDatabase"/>), applies the real migrations, and hands out
/// <see cref="WaydDbContext"/> instances pointed at it. Refresh-token sessions depend on
/// behaviour an in-memory provider does not have — the filtered unique index, NodaTime
/// <c>Instant</c> mapping, and cascade delete from Users — so they are tested here.
/// </summary>
/// <remarks>Requires Docker on the machine running the tests.</remarks>
public sealed class SqlServerDbContextFixture : IAsyncLifetime
{
    private SqlServerTestDatabase _database = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqlServerTestDatabase.Create("infrastructure");

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
            await _database.DisposeAsync();
    }

    /// <summary>Creates a fresh context against the database, with no-op collaborators.</summary>
    public WaydDbContext CreateContext() => TestDbContexts.Create(_database.ConnectionString);

    /// <summary>Clears identity rows so each test starts clean. Ordered to respect foreign keys.</summary>
    public async Task ResetIdentityData(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[PersonalAccessTokens];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[UserRefreshTokens];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[UserIdentities];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[Users];", cancellationToken);
    }

    /// <summary>Clears import runs and their rows. Ordered to respect foreign keys.</summary>
    public async Task ResetImportData(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Imports].[ImportProcessRows];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Imports].[ImportProcesses];", cancellationToken);
    }
}

[CollectionDefinition(nameof(SqlServerTestCollection))]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerDbContextFixture>;
