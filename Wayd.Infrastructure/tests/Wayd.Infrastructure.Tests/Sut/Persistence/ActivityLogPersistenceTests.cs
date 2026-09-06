using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Models;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Persistence;
using Wayd.Infrastructure.Persistence.Context;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Wayd.Infrastructure.Tests.Sut.Persistence;

public sealed class ActivityLogPersistenceTests
{
    [Fact]
    public async Task SaveChangesAsync_AutomaticallyCapturesRaisedDomainEvent_IntoActivityLogs()
    {
        // Arrange
        var harness = new Harness(correlationId: "corr-123");
        var entity = new ActivityTestEntity();
        var domainEvent = new TestBusinessEvent("Sample Details", EventActor.User("user-42", Guid.NewGuid()), Instant.FromUnixTimeSeconds(100));
        entity.Raise(domainEvent);
        harness.Context.Entities.Add(entity);

        // Act
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var savedLog = await harness.Context.ActivityLogs.SingleOrDefaultAsync(a => a.Id == domainEvent.EventId, TestContext.Current.CancellationToken);
        savedLog.Should().NotBeNull();
        savedLog!.Id.Should().Be(domainEvent.EventId);
        savedLog.EventType.Should().Be(nameof(TestBusinessEvent));
        savedLog.AggregateType.Should().Be(nameof(ActivityTestEntity));
        savedLog.AggregateId.Should().Be(entity.Id);
        savedLog.ActorKind.Should().Be(EventActorKind.User);
        savedLog.UserId.Should().Be("user-42");
        savedLog.EmployeeId.Should().Be(domainEvent.Actor.EmployeeId);
        savedLog.Timestamp.Should().Be(Instant.FromUnixTimeSeconds(100));
        savedLog.CorrelationId.Should().Be("corr-123");
        savedLog.Payload.Should().Contain("Sample Details");
        savedLog.Summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetEntityActivityQuery_ReturnsChronologicalLogs_ForMatchingAggregate()
    {
        // Arrange
        var harness = new Harness(correlationId: "corr-query");
        var entity1 = new ActivityTestEntity();
        var entity2 = new ActivityTestEntity();

        entity1.Raise(new TestBusinessEvent("Event 1", EventActor.User("user-1"), Instant.FromUnixTimeSeconds(10)));
        entity1.Raise(new TestBusinessEvent("Event 2", EventActor.User("user-1"), Instant.FromUnixTimeSeconds(20)));
        entity2.Raise(new TestBusinessEvent("Other Entity Event", EventActor.User("user-2"), Instant.FromUnixTimeSeconds(30)));

        harness.Context.Entities.AddRange(entity1, entity2);
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await harness.Context.ActivityLogs
            .Where(a => a.AggregateId == entity1.Id && a.AggregateType == nameof(ActivityTestEntity))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results[0].Timestamp.Should().Be(Instant.FromUnixTimeSeconds(20)); // Newest first
        results[1].Timestamp.Should().Be(Instant.FromUnixTimeSeconds(10));
        results.All(r => r.AggregateId == entity1.Id).Should().BeTrue();
    }

    private sealed class Harness
    {
        public Harness(string correlationId)
        {
            var currentUser = new Mock<ICurrentUser>();
            currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

            var dateTimeProvider = new Mock<IDateTimeProvider>();
            dateTimeProvider.SetupGet(d => d.Now).Returns(Instant.FromUnixTimeSeconds(0));

            var correlation = new Mock<IRequestCorrelationIdProvider>();
            correlation.SetupGet(c => c.CorrelationId).Returns(correlationId);

            var events = new Mock<IEventPublisher>();
            events.Setup(e => e.PublishAsync(It.IsAny<IEvent>()))
                .Returns(Task.CompletedTask);

            var outbox = new Mock<IDbContextOutbox>();
            outbox.Setup(o => o.PublishAsync(It.IsAny<object>(), It.IsAny<DeliveryOptions?>()))
                .Returns(ValueTask.CompletedTask);
            outbox.Setup(o => o.FlushOutgoingMessagesAsync()).Returns(Task.CompletedTask);

            var options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase($"activity-{Guid.CreateVersion7()}")
                .Options;

            Context = new TestDbContext(
                options,
                currentUser.Object,
                dateTimeProvider.Object,
                Options.Create(new DatabaseSettings()),
                events.Object,
                outbox.Object,
                correlation.Object);
        }

        public TestDbContext Context { get; }
    }

    public sealed class TestDbContext : BaseDbContext, IActivityLogDbContext
    {
        public TestDbContext(
            DbContextOptions options,
            ICurrentUser currentUser,
            IDateTimeProvider dateTimeProvider,
            IOptions<DatabaseSettings> dbSettings,
            IEventPublisher events,
            IDbContextOutbox outbox,
            IRequestCorrelationIdProvider requestCorrelationIdProvider)
            : base(options, currentUser, dateTimeProvider, dbSettings, events, outbox, requestCorrelationIdProvider)
        {
        }

        public DbSet<ActivityTestEntity> Entities => Set<ActivityTestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaydDbContext).Assembly);
            modelBuilder.Entity<ActivityTestEntity>().ToTable("ActivityTestEntities").HasKey(e => e.Id);
        }
    }

    public sealed class ActivityTestEntity : BaseEntity
    {
        public void Raise(DomainEvent domainEvent) => AddDomainEvent(domainEvent);
    }

    private sealed record TestBusinessEvent : DomainEvent
    {
        public string Details { get; }

        public TestBusinessEvent(string details, EventActor actor, Instant timestamp) : base(actor)
        {
            Details = details;
            Timestamp = timestamp;
        }
    }
}

