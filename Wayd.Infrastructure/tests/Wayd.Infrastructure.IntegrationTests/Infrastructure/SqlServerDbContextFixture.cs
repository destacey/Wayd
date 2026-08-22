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
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2025-CU8-ubuntu-24.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerImage).Build();

    private DbContextOptions<WaydDbContext> _options = null!;
    private IOptions<DatabaseSettings> _databaseSettings = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString();

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

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Creates a fresh context against the container, with no-op collaborators.</summary>
    public WaydDbContext CreateContext()
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
            _options,
            currentUser.Object,
            dateTimeProvider.Object,
            _databaseSettings,
            events.Object,
            outbox.Object,
            correlationId.Object);
    }

    /// <summary>Clears identity rows so each test starts clean. Ordered to respect foreign keys.</summary>
    public async Task ResetIdentityData(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[UserRefreshTokens];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[UserIdentities];", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Identity].[Users];", cancellationToken);
    }
}

[CollectionDefinition(nameof(SqlServerTestCollection))]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerDbContextFixture>;
