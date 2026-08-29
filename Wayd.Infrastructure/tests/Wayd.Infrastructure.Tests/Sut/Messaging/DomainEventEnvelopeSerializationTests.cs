using System.Text.Json;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.Infrastructure.Tests.Sut.Messaging;

/// <summary>
/// Proves the <see cref="DomainEvent"/> envelope survives the durable outbox's serialization.
/// </summary>
/// <remarks>
/// The options here mirror <c>WolverineConfiguration</c>'s
/// <c>UseSystemTextJsonForSerialization(json =&gt; json.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb))</c>.
/// <see cref="DomainEvent.Actor"/> is a required constructor parameter, so it binds by name like any other
/// parameter; <see cref="DomainEvent.EventId"/> is assigned at construction and must survive rather than be
/// regenerated, which is what makes it usable as a deduplication key across at-least-once redeliveries.
/// </remarks>
public sealed class DomainEventEnvelopeSerializationTests
{
    private static JsonSerializerOptions OutboxOptions() =>
        new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    [Fact]
    public void RoundTrip_OnAnEventWithoutAnExplicitJsonConstructor_PreservesTheEnvelope()
    {
        // Arrange — ProjectDeletedEvent has a single constructor and no [JsonConstructor].
        var options = OutboxOptions();
        var original = new ProjectDeletedEvent(
            Guid.CreateVersion7(),
            EventActor.User("user-99"),
            Instant.FromUnixTimeSeconds(1_700_000_000))
        {
            CorrelationId = "corr-round-trip",
        };

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<ProjectDeletedEvent>(json, options);

        // Assert
        restored.Should().NotBeNull();
        restored!.EventId.Should().Be(original.EventId, "the id is the dedupe key and must survive the outbox, not be regenerated");
        restored.Actor.Should().Be(original.Actor);
        restored.Actor.Kind.Should().Be(EventActorKind.User);
        restored.Actor.UserId.Should().Be("user-99");
        restored.CorrelationId.Should().Be("corr-round-trip");
        restored.Timestamp.Should().Be(original.Timestamp);
        restored.Id.Should().Be(original.Id);
    }

    [Fact]
    public void RoundTrip_OnAnEventWithAnExplicitJsonConstructor_PreservesTheEnvelope()
    {
        // Arrange — ProjectDetailsUpdatedEvent carries an explicit [JsonConstructor] because it has a
        // second, non-bindable constructor. The envelope must survive that path too.
        var options = OutboxOptions();
        var original = new ProjectDetailsUpdatedEvent(
            Guid.CreateVersion7(),
            new ProjectKey("APOLLO"),
            "Apollo",
            "A project",
            expenditureCategoryId: 3,
            EventActor.Import("user-100"),
            Instant.FromUnixTimeSeconds(1_700_000_100))
        {
            CorrelationId = "corr-json-ctor",
        };

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<ProjectDetailsUpdatedEvent>(json, options);

        // Assert
        restored.Should().NotBeNull();
        restored!.EventId.Should().Be(original.EventId);
        restored.Actor.Kind.Should().Be(EventActorKind.Import, "the mechanism must survive, not just the user behind it");
        restored.Actor.UserId.Should().Be("user-100");
        restored.CorrelationId.Should().Be("corr-json-ctor");
        restored.Name.Should().Be("Apollo", "the payload must round-trip unchanged alongside the envelope");
        restored.ExpenditureCategoryId.Should().Be(3);
    }

    [Fact]
    public void RoundTrip_PreservesTheEventIdAcrossRepeatedRedeliveries()
    {
        // Arrange — durable delivery is at-least-once, so the same stored payload is deserialized afresh on
        // every attempt. A handler deduplicates on EventId, so it must be stable across those attempts; an
        // id minted during deserialization would differ each time and silently defeat the deduplication.
        var options = OutboxOptions();
        var original = new TeamDeletedEvent(Guid.CreateVersion7(), EventActor.System, Instant.FromUnixTimeSeconds(1_700_000_200));
        var storedPayload = JsonSerializer.Serialize(original, options);

        // Act
        var firstAttempt = JsonSerializer.Deserialize<TeamDeletedEvent>(storedPayload, options)!;
        var secondAttempt = JsonSerializer.Deserialize<TeamDeletedEvent>(storedPayload, options)!;
        var thirdAttempt = JsonSerializer.Deserialize<TeamDeletedEvent>(storedPayload, options)!;

        // Assert
        new[] { firstAttempt.EventId, secondAttempt.EventId, thirdAttempt.EventId }
            .Should().AllBeEquivalentTo(original.EventId, "every redelivery of one occurrence carries the same id");
    }

    [Fact]
    public void RoundTrip_OnASystemActor_PreservesTheSystemAttribution()
    {
        // Arrange — background work has no signed-in user; the platform is the actor.
        var options = OutboxOptions();
        var original = new TeamDeletedEvent(Guid.CreateVersion7(), EventActor.System, Instant.FromUnixTimeSeconds(1_700_000_300));

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<TeamDeletedEvent>(json, options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Actor.Kind.Should().Be(EventActorKind.System);
        restored.Actor.UserId.Should().Be(SystemUser.Id);
        restored.Actor.HasOriginatingUser.Should().BeFalse("nobody triggered a platform action, so a notification must not name one");
    }

    [Fact]
    public void RoundTrip_OnASyncActorWithNoOriginatingUser_PreservesTheNullUser()
    {
        // Arrange — a scheduled sync nobody started: the mechanism is known, the person is not.
        var options = OutboxOptions();
        var original = new TeamDeletedEvent(Guid.CreateVersion7(), EventActor.Sync(null), Instant.FromUnixTimeSeconds(1_700_000_400));

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<TeamDeletedEvent>(json, options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Actor.Kind.Should().Be(EventActorKind.Sync);
        restored.Actor.UserId.Should().BeNull();
        restored.Actor.HasOriginatingUser.Should().BeFalse();
    }
}
