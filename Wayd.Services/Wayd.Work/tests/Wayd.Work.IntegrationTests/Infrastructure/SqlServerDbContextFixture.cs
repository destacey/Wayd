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
using Wolverine.EntityFrameworkCore;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a SQL Server container and applies the real <c>Wayd.Infrastructure.Migrators.MSSQL</c> migrations
/// against it, then hands out <see cref="WaydDbContext"/> instances pointed at that container. This exercises
/// the production EF provider, so value converters (e.g. <c>TeamCode</c> → <c>varchar</c>), NodaTime mapping,
/// and set-based updates (ExecuteUpdate) all behave exactly as they do in production — the very reason
/// Testcontainers is used here instead of SQLite or an in-memory fake.
/// <para>
/// This is a collection fixture (see <see cref="SqlServerTestCollection"/>): one container and one migrated
/// schema are shared by every test class in the collection, so tests must not assume a private database.
/// Reset the rows you touch with <see cref="ResetWorkData"/> at the start of each test.
/// </para>
/// </summary>
/// <remarks>Requires Docker to be running on the machine executing the tests.</remarks>
public sealed class SqlServerDbContextFixture : IAsyncLifetime
{
    // A fixed instant so audit/system columns are deterministic and no test ever reaches for DateTime.UtcNow.
    public static readonly Instant FixedNow = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    // Pinned to a concrete CU rather than a floating tag (e.g. 2022-latest), so the schema is built against
    // the same SQL Server engine on every machine and CI run. Bump deliberately.
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2025-CU8-ubuntu-24.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerImage).Build();

    private DbContextOptions<WaydDbContext> _options = null!;
    private IOptions<DatabaseSettings> _databaseSettings = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

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

                // Several of these containers start at once on a CI runner with far fewer cores than
                // containers. SQL Server accepts connections before it has finished warming up, so the
                // first queries can hit transient timeouts and fail a test that has nothing wrong with
                // it. Retry those rather than letting load masquerade as a test failure.
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            })
            .Options;

        // Apply the real migrations so the schema — varchar columns, converters and the SQL-graph
        // TeamNodes / TeamMembershipEdges tables — matches production.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Creates a fresh <see cref="WaydDbContext"/> against the container, with no-op collaborators.</summary>
    public WaydDbContext CreateContext()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(FixedNow);
        dateTimeProvider.SetupGet(d => d.Today).Returns(FixedNow.InUtc().Date);

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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkItems];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [AppIntegrations].[ExternalIdentityMappings];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Organization].[EmployeeEmails];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Organization].[Employees];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[Workspaces];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Work].[WorkProcesses];", cancellationToken);
    }
}
