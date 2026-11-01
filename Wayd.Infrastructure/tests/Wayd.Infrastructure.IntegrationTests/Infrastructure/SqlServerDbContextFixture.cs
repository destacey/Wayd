using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Testcontainers.MsSql;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Persistence;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Tests.Containers;
using Wolverine.EntityFrameworkCore;

namespace Wayd.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a SQL Server container, applies the real migrations, and hands out
/// <see cref="WaydDbContext"/> instances pointed at it. Refresh-token sessions depend on
/// behaviour an in-memory provider does not have — the filtered unique index, NodaTime
/// <c>Instant</c> mapping, and cascade delete from Users — so they are tested here.
/// </summary>
/// <remarks>Requires Docker on the machine running the tests.</remarks>
public sealed class SqlServerDbContextFixture : IAsyncLifetime
{
    private MsSqlContainer _container = null!;

    private DbContextOptions<WaydDbContext> _options = null!;
    private IOptions<DatabaseSettings> _databaseSettings = null!;

    public async ValueTask InitializeAsync()
    {
        _container = await SqlServerTestContainer.Start();

        var connectionString = _container.GetConnectionString();

        _databaseSettings = Options.Create(new DatabaseSettings
        {
            DBProvider = "mssql",
            ConnectionString = connectionString,
        });

        _options = BuildOptions(connectionString);

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>The container's connection string, for a test that needs a database of its own on it.</summary>
    public string ConnectionString => _container.GetConnectionString();

    private static DbContextOptions<WaydDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<WaydDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly("Wayd.Infrastructure.Migrators.MSSQL");
                sql.UseNodaTime();

                // No EnableRetryOnFailure. BaseDbContext.SaveChangesAsync opens the transaction that commits
                // the entities with their audit trail, activity log and outbox envelopes, and a retrying
                // strategy refuses a transaction it did not start — so switching it on fails every save here
                // while production, which has no retries either, works. SqlServerTestContainer waits on a real
                // SELECT 1 before handing the container over, which is what the retries were covering for.
            })
            .Options;

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    /// <summary>Creates a fresh context against the container, with no-op collaborators.</summary>
    public WaydDbContext CreateContext() => CreateContext(_options, _databaseSettings);

    /// <summary>
    /// Creates a context against another database on the container, unmigrated. The context connects through
    /// <see cref="DatabaseSettings"/>, so the settings carry the connection string as well as the options.
    /// </summary>
    public WaydDbContext CreateContext(string connectionString) => CreateContext(
        BuildOptions(connectionString),
        Options.Create(new DatabaseSettings { DBProvider = "mssql", ConnectionString = connectionString }));

    private static WaydDbContext CreateContext(DbContextOptions<WaydDbContext> options, IOptions<DatabaseSettings> databaseSettings)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        dateTimeProvider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 1, 15));

        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<IEvent>())).Returns(Task.CompletedTask);

        var outbox = new Mock<IDbContextOutbox>();
        outbox.Setup(o => o.FlushOutgoingMessagesAsync()).Returns(Task.CompletedTask);

        var correlationId = new Mock<IRequestCorrelationIdProvider>();
        correlationId.SetupGet(c => c.CorrelationId).Returns("integration-test-correlation");

        return new WaydDbContext(
            options,
            currentUser.Object,
            dateTimeProvider.Object,
            databaseSettings,
            events.Object,
            outbox.Object,
            correlationId.Object);
    }

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
