using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Persistence;
using Wayd.Infrastructure.Persistence.Context;
using Wolverine.EntityFrameworkCore;

namespace Wayd.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>Builds the <see cref="WaydDbContext"/> the fixtures hand out, against any database on the server.</summary>
internal static class TestDbContexts
{
    /// <summary>
    /// Creates a context against <paramref name="connectionString"/>, with no-op collaborators. The context connects
    /// through <see cref="DatabaseSettings"/>, so the settings carry the connection string as well as the options.
    /// </summary>
    public static WaydDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<WaydDbContext>()
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

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Instant.FromUtc(2026, 1, 15, 9, 30, 0));

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
            Options.Create(new DatabaseSettings { DBProvider = "mssql", ConnectionString = connectionString }),
            events.Object,
            outbox.Object,
            correlationId.Object);
    }
}
