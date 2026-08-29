using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Persistence;
using Wayd.Infrastructure.Persistence.Context;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Wayd.Infrastructure.Tests.Sut.Persistence.Context;

/// <summary>
/// Covers what <see cref="BaseDbContext"/> does to the <see cref="DomainEvent"/> envelope when it drains
/// domain events: it stamps the correlation id, and nothing else. The event id and the actor are the
/// domain's, assigned at construction, and the drain point must leave both alone.
/// </summary>
/// <remarks>
/// Exercised through a real <see cref="BaseDbContext"/> subclass on the InMemory provider rather than a
/// hand-rolled fake, so the assertions run against the production <c>SaveChangesAsync</c> path.
/// </remarks>
public sealed class DomainEventEnvelopeTests
{
    [Fact]
    public async Task SaveChanges_StampsTheCorrelationIdOnAnInlineEvent()
    {
        // Arrange
        var harness = new Harness(correlationId: "corr-abc");
        var entity = new EventRaisingEntity();
        entity.Raise(new PortfolioRenamedEvent(EventActor.User("user-42"), Instant.FromUnixTimeSeconds(1)));
        harness.Context.Entities.Add(entity);

        // Act
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var published = harness.PublishedInline.Should().ContainSingle().Subject.Should().BeOfType<PortfolioRenamedEvent>().Subject;
        published.CorrelationId.Should().Be("corr-abc");
        published.EventId.Should().NotBe(Guid.Empty, "the id is assigned at construction, never left empty");
        published.Actor.UserId.Should().Be("user-42", "the actor is the domain's and the drain point must not touch it");
        published.Timestamp.Should().Be(Instant.FromUnixTimeSeconds(1), "stamping must not disturb the payload the aggregate supplied");
    }

    [Fact]
    public async Task SaveChanges_StampsTheCorrelationIdOnADurableEvent()
    {
        // Arrange — ProjectDeletedEvent is on the DurableEventRoutes allow-list, so it takes the outbox
        // branch rather than the inline one. Both branches must be stamped.
        var harness = new Harness(correlationId: "corr-durable");
        var entity = new EventRaisingEntity();
        entity.Raise(new ProjectDeletedEvent(Guid.NewGuid(), EventActor.User("user-7"), Instant.FromUnixTimeSeconds(2)));
        harness.Context.Entities.Add(entity);

        // Act
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        harness.PublishedInline.Should().BeEmpty("a durable event is routed to the outbox, not dispatched inline");
        var enrolled = harness.PublishedToOutbox.Should().ContainSingle().Subject.Should().BeOfType<ProjectDeletedEvent>().Subject;
        enrolled.CorrelationId.Should().Be("corr-durable", "the correlation id must be stamped before the event is serialized into an outbox row");
        enrolled.EventId.Should().NotBe(Guid.Empty);
        enrolled.Actor.UserId.Should().Be("user-7");
    }

    [Fact]
    public async Task SaveChanges_WithNoSignedInUser_DoesNotThrow()
    {
        // Arrange — a background job or an anonymous request: nobody is signed in, and the domain says so
        // by raising the event with the system actor. A save must never fail for want of a signed-in user.
        var harness = new Harness(correlationId: "corr-system");
        var entity = new EventRaisingEntity();
        entity.Raise(new PortfolioRenamedEvent(EventActor.System, Instant.FromUnixTimeSeconds(3)));
        harness.Context.Entities.Add(entity);

        // Act
        var save = async () => await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await save.Should().NotThrowAsync();
        var published = harness.PublishedInline.Should().ContainSingle().Subject.Should().BeOfType<PortfolioRenamedEvent>().Subject;
        published.Actor.Kind.Should().Be(EventActorKind.System);
        published.Actor.UserId.Should().Be(SystemUser.Id);
        published.CorrelationId.Should().Be("corr-system", "correlation does not depend on there being a user");
    }

    [Fact]
    public async Task SaveChanges_PreservesAnImportActorRatherThanOverwritingItWithTheSignedInUser()
    {
        // Arrange — the case the envelope exists for: an import run BY a user is not the same as that user
        // editing each record by hand. The drain point must not rewrite the domain's attribution.
        var harness = new Harness(correlationId: "corr-import");
        var entity = new EventRaisingEntity();
        entity.Raise(new PortfolioRenamedEvent(EventActor.Import("alice"), Instant.FromUnixTimeSeconds(4)));
        harness.Context.Entities.Add(entity);

        // Act
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var published = harness.PublishedInline.Should().ContainSingle().Subject.Should().BeOfType<PortfolioRenamedEvent>().Subject;
        published.Actor.Kind.Should().Be(EventActorKind.Import, "an import stays an import, however it was triggered");
        published.Actor.UserId.Should().Be("alice", "the person who started the import is still recorded");
    }

    [Fact]
    public async Task SaveChanges_GivesEachEventItsOwnIdButOneSharedCorrelationId()
    {
        // Arrange
        var harness = new Harness(correlationId: "corr-shared");
        var entity = new EventRaisingEntity();
        entity.Raise(new PortfolioRenamedEvent(EventActor.User("user-1"), Instant.FromUnixTimeSeconds(5)));
        entity.Raise(new PortfolioRenamedEvent(EventActor.User("user-1"), Instant.FromUnixTimeSeconds(6)));
        harness.Context.Entities.Add(entity);

        // Act
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var published = harness.PublishedInline.Cast<PortfolioRenamedEvent>().ToArray();
        published.Should().HaveCount(2);
        published.Select(e => e.EventId).Distinct().Should().HaveCount(2, "each occurrence is deduplicated independently");
        published.Select(e => e.CorrelationId).Should().AllBe("corr-shared", "one save is one chain of consequences");
    }

    [Fact]
    public async Task SaveChanges_DoesNotOverwriteACorrelationIdTheEventAlreadyCarries()
    {
        // Arrange — a re-entrant save (the audit temp-property pass calls SaveChanges again) must not
        // re-stamp an event that has already been through the drain point.
        var harness = new Harness(correlationId: "corr-new");
        var entity = new EventRaisingEntity();
        entity.Raise(new PortfolioRenamedEvent(EventActor.User("user-1"), Instant.FromUnixTimeSeconds(7)) { CorrelationId = "corr-original" });
        harness.Context.Entities.Add(entity);

        // Act
        await harness.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var published = harness.PublishedInline.Should().ContainSingle().Subject.Should().BeOfType<PortfolioRenamedEvent>().Subject;
        published.CorrelationId.Should().Be("corr-original");
    }

    /// <summary>
    /// Wires a real <see cref="BaseDbContext"/> over the InMemory provider with recording test doubles for
    /// the two dispatch sinks, so a test can assert on what each routing branch was handed.
    /// </summary>
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
                .Callback<IEvent>(PublishedInline.Add)
                .Returns(Task.CompletedTask);

            var outbox = new Mock<IDbContextOutbox>();
            outbox.Setup(o => o.PublishAsync(It.IsAny<object>(), It.IsAny<DeliveryOptions?>()))
                .Callback<object, DeliveryOptions?>((message, _) => PublishedToOutbox.Add(message))
                .Returns(ValueTask.CompletedTask);
            outbox.Setup(o => o.FlushOutgoingMessagesAsync()).Returns(Task.CompletedTask);

            var options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase($"envelope-{Guid.CreateVersion7()}")
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

        public List<IEvent> PublishedInline { get; } = [];

        public List<object> PublishedToOutbox { get; } = [];
    }

    /// <summary>
    /// A minimal concrete <see cref="BaseDbContext"/>. <c>WaydDbContext</c> cannot stand in here: its
    /// inherited <c>OnConfiguring</c> resolves a provider from <c>DatabaseSettings</c> and throws for
    /// anything but SQL Server, so it cannot run on the InMemory provider.
    /// </summary>
    private sealed class TestDbContext : BaseDbContext
    {
        public TestDbContext(DbContextOptions options, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, IOptions<DatabaseSettings> dbSettings, IEventPublisher events, IDbContextOutbox outbox, IRequestCorrelationIdProvider requestCorrelationIdProvider)
            : base(options, currentUser, dateTimeProvider, dbSettings, events, outbox, requestCorrelationIdProvider)
        {
        }

        public DbSet<EventRaisingEntity> Entities => Set<EventRaisingEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Deliberately does NOT call base: the inherited override forces a SQL Server provider.
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // BaseDbContext applies configurations from GetType().Assembly, which for this subclass is the
            // TEST assembly — so none of the real ones are found and EF cannot bind value objects such as
            // EmailAddress reached from the inherited Identity model. Apply them from the assembly that
            // actually declares them.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaydDbContext).Assembly);

            modelBuilder.Entity<EventRaisingEntity>().ToTable("EnvelopeTestEntities").HasKey(e => e.Id);
        }
    }

    private sealed class EventRaisingEntity : BaseEntity
    {
        public void Raise(DomainEvent domainEvent) => AddDomainEvent(domainEvent);
    }

    /// <summary>
    /// A throwaway event that is absent from the <c>DurableEventRoutes</c> allow-list, so it takes the
    /// inline branch. Declared here rather than reusing a production event so the test does not silently
    /// change meaning if that event's routing is changed.
    /// </summary>
    private sealed record PortfolioRenamedEvent : DomainEvent
    {
        public PortfolioRenamedEvent(EventActor actor, Instant timestamp)
            : base(actor) =>
            Timestamp = timestamp;
    }
}
