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

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a SQL Server container and applies the real <c>Wayd.Infrastructure.Migrators.MSSQL</c> migrations
/// against it, then hands out <see cref="WaydDbContext"/> instances pointed at that container.
/// <para>
/// Planning writes need this because the in-memory <c>FakePlanningDbContext</c> runs LINQ-to-Objects, where
/// <c>.Include</c> is a no-op and a save records nothing: a handler that forgets to load a collection the
/// aggregate changes passes against the fake and silently skips the change in production.
/// </para>
/// <para>
/// This is a collection fixture (see <see cref="SqlServerTestCollection"/>): one container and one migrated
/// schema are shared by every test class in the collection, so tests must not assume a private database.
/// </para>
/// </summary>
/// <remarks>Requires Docker to be running on the machine executing the tests.</remarks>
public sealed class SqlServerDbContextFixture : IAsyncLifetime
{
    // A fixed instant so audit/system columns are deterministic and no test ever reaches for DateTime.UtcNow.
    public static readonly Instant FixedNow = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    public const string UserId = "integration-test-user";

    private MsSqlContainer _container = null!;

    private DbContextOptions<WaydDbContext> _options = null!;
    private IOptions<DatabaseSettings> _databaseSettings = null!;

    public async ValueTask InitializeAsync()
    {
        _container = await SqlServerTestContainer.Start();

        var connectionString = _container.GetConnectionString();

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
                // while production, which has no retries either, works. SqlServerTestContainer waits on a real
                // SELECT 1 before handing the container over, which is what the retries were covering for.
            })
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    /// <summary>Creates a fresh <see cref="WaydDbContext"/> against the container, with no-op collaborators.</summary>
    public WaydDbContext CreateContext()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(UserId);

        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<IEvent>())).Returns(Task.CompletedTask);

        var outbox = new Mock<IDbContextOutbox>();
        outbox.Setup(o => o.FlushOutgoingMessagesAsync()).Returns(Task.CompletedTask);

        var correlationId = new Mock<IRequestCorrelationIdProvider>();
        correlationId.SetupGet(c => c.CorrelationId).Returns("integration-test-correlation");

        return new WaydDbContext(
            _options,
            currentUser.Object,
            DateTimeProvider(),
            _databaseSettings,
            events.Object,
            outbox.Object,
            correlationId.Object);
    }

    public static IDateTimeProvider DateTimeProvider() => DateTimeProvider(FixedNow);

    public static IDateTimeProvider DateTimeProvider(Instant now)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(now);
        dateTimeProvider.SetupGet(d => d.Today).Returns(now.InUtc().Date);
        return dateTimeProvider.Object;
    }

    public static ICurrentUser CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(UserId);
        return currentUser.Object;
    }
}
