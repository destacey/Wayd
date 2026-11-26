using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Persistence;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Tests.Containers;
using Wolverine.EntityFrameworkCore;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Creates a SQL Server database of its own (see <see cref="SqlServerTestDatabase"/>), applies the real <c>Wayd.Infrastructure.Migrators.MSSQL</c> migrations
/// against it, then hands out <see cref="WaydDbContext"/> instances pointed at it. This exercises
/// the production EF provider, so value converters (e.g. <c>TeamCode</c> → <c>varchar</c>), NodaTime mapping,
/// and set-based updates (ExecuteUpdate) all behave exactly as they do in production — the very reason
/// Testcontainers is used here instead of SQLite or an in-memory fake.
/// <para>
/// This is a collection fixture (see <see cref="SqlServerTestCollection"/>): one database and one migrated
/// schema are shared by every test class in the collection, so tests must not assume a private database.
/// Reset the rows you touch with <see cref="ResetWorkData"/> at the start of each test.
/// </para>
/// </summary>
/// <remarks>Requires Docker to be running on the machine executing the tests.</remarks>
public sealed class SqlServerDbContextFixture : IAsyncLifetime
{
    // A fixed instant so audit/system columns are deterministic and no test ever reaches for DateTime.UtcNow.
    public static readonly Instant FixedNow = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private SqlServerTestDatabase _database = null!;

    private DbContextOptions<WaydDbContext> _options = null!;
    private IOptions<DatabaseSettings> _databaseSettings = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqlServerTestDatabase.Create("work");

        var connectionString = _database.ConnectionString;

        // Drive OnConfiguring down the SqlServer + NodaTime path (DBProvider "mssql"), exactly as production.
        _databaseSettings = Options.Create(new DatabaseSettings
        {
            DBProvider = "mssql",
            ConnectionString = connectionString,
        });

        _options = new DbContextOptionsBuilder<WaydDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly("Wayd.Infrastructure.Migrators.MSSQL");
                sql.UseNodaTime();

                // No EnableRetryOnFailure. BaseDbContext.SaveChangesAsync opens the transaction that commits
                // the entities with their audit trail, activity log and outbox envelopes, and a retrying
                // strategy refuses a transaction it did not start — so switching it on fails every save here
                // while production, which has no retries either, works. The engine has already answered an
                // sa login before a suite connects, which is what the retries were covering for.
            })
            .Options;

        // Apply the real migrations so the schema — varchar columns and converters — matches production.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
            await _database.DisposeAsync();
    }

    /// <summary>Creates a fresh <see cref="WaydDbContext"/> against the database, with no-op collaborators.</summary>
    public WaydDbContext CreateContext()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(FixedNow);

        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<IEvent>())).Returns(Task.CompletedTask);

        // Team* events are durable, so BaseDbContext enrolls this outbox and publishes/flushes through it when
        // a team is saved. These tests don't exercise real message persistence; Moq returns completed tasks for
        // the async members by default, and FlushOutgoingMessagesAsync is stubbed explicitly to be safe.
        var outbox = new Mock<IDbContextOutbox>();
        outbox.Setup(o => o.FlushOutgoingMessagesAsync()).Returns(Task.CompletedTask);

        var correlationId = new Mock<IRequestCorrelationIdProvider>();
        correlationId.SetupGet(c => c.CorrelationId).Returns("integration-test-correlation");

        return new WaydDbContext(
            _options,
            currentUser.Object,
            dateTimeProvider.Object,
            _databaseSettings,
            events.Object,
            outbox.Object,
            correlationId.Object);
    }

    /// <summary>
    /// Removes the Work rows these tests touch, plus the Organization and AppIntegration rows they
    /// depend on, so each test starts from a clean slate. Ordered to respect foreign keys.
    /// </summary>
    public async Task ResetWorkData(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkItemsExtended];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkItemLinks];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkItemReferences];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkItems];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [AppIntegrations].[ExternalIdentityMappings];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Organization].[EmployeeEmails];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Organization].[Employees];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[Workspaces];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkProcesses];", cancellationToken);
    }
}
