using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Tests.Containers;

namespace Wayd.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>
/// Databases migrated only as far as the migration before the one under test, for a test of a migration the current
/// model no longer describes. Each test gets the database as it was migrated, however the test before it left it.
/// </summary>
/// <remarks>
/// A class fixture rather than a collection: migrating a database from nothing takes far longer than anything else in
/// the suite, so each such class runs as a collection of its own, in parallel with the others and with
/// <see cref="SqlServerTestCollection"/>. Within a class, the database is migrated once and snapshotted; every later
/// test reverts it to that snapshot, which takes a fraction of a second where migrating again would not.
/// </remarks>
public sealed class SqlServerMigrationFixture : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, SqlServerTestDatabase> _databases = new(StringComparer.Ordinal);

    /// <summary>
    /// A connection string for a database migrated as far as <paramref name="migration"/> and nothing more, with
    /// nothing a previous test wrote to it.
    /// </summary>
    /// <param name="migration">The last migration to apply, by its full id.</param>
    /// <param name="cancellationToken">Cancels creating, migrating or reverting the database.</param>
    public async Task<string> MigratedTo(string migration, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_databases.TryGetValue(migration, out var existing))
            {
                await existing.RevertToSnapshot(cancellationToken);
                return existing.ConnectionString;
            }

            var database = await SqlServerTestDatabase.Create("migration", cancellationToken);
            try
            {
                await using (var context = CreateContext(database.ConnectionString))
                    await context.GetService<IMigrator>().MigrateAsync(migration, cancellationToken);

                await database.CreateSnapshot(cancellationToken);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }

            _databases[migration] = database;
            return database.ConnectionString;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Creates a context against a database on the server.</summary>
    public WaydDbContext CreateContext(string connectionString) => TestDbContexts.Create(connectionString);

    /// <summary>Drops every database the fixture migrated, with its snapshot.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var database in _databases.Values)
            await database.DisposeAsync();
        _gate.Dispose();
    }
}
