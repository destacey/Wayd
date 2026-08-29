using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// Covers the invariants the <see cref="DomainEvent"/> base guarantees at construction, independently of
/// any persistence path: every event has an id, and every event names an actor.
/// </summary>
public sealed class DomainEventEnvelopeTests
{
    [Fact]
    public void Construction_AssignsAnEventId()
    {
        // Arrange / Act
        var @event = new TestEvent(EventActor.System, Instant.FromUnixTimeSeconds(1));

        // Assert
        @event.EventId.Should().NotBe(Guid.Empty, "an event is identifiable from the moment it exists, with no stamping step required");
    }

    [Fact]
    public void Construction_GivesEachEventItsOwnId()
    {
        // Arrange / Act
        var first = new TestEvent(EventActor.System, Instant.FromUnixTimeSeconds(1));
        var second = new TestEvent(EventActor.System, Instant.FromUnixTimeSeconds(1));

        // Assert
        first.EventId.Should().NotBe(second.EventId, "the id identifies an occurrence, not an event type");
    }

    [Fact]
    public void Construction_WithoutAnActor_Throws()
    {
        // Arrange — the compiler forces callers to pass something; this guards the one remaining hole,
        // an explicit null, so the actor can never be silently absent.
        var construct = () => new TestEvent(null!, Instant.FromUnixTimeSeconds(1));

        // Act / Assert
        construct.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Construction_LeavesTheCorrelationIdUnsetUntilItIsSaved()
    {
        // Arrange / Act — correlation is infrastructure's to stamp; the domain has no idea what request
        // it is running inside.
        var @event = new TestEvent(EventActor.System, Instant.FromUnixTimeSeconds(1));

        // Assert
        @event.CorrelationId.Should().BeNull();
    }

    private sealed record TestEvent : DomainEvent
    {
        public TestEvent(EventActor actor, Instant timestamp)
            : base(actor) =>
            Timestamp = timestamp;
    }
}
