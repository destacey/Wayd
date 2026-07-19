using System.Text.Json;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// Domain events must survive a System.Text.Json round-trip for Wolverine's durable outbox: an event only
/// becomes durable once it can be written to and read back from the envelope store. These tests guard the
/// concrete events whose members are the ones most likely to break serialization — NodaTime types
/// (<see cref="Instant"/>, <see cref="LocalDate"/>), value objects with parameterized constructors
/// (<see cref="TeamCode"/>, <see cref="LocalDateRange"/>), a value type with a private constructor and
/// init-only properties (<see cref="IntegrationState{TId}"/>), and collection members.
///
/// The serializer configuration here mirrors what the Wolverine host registers
/// (<c>UseSystemTextJsonForSerialization(json =&gt; json.ConfigureForNodaTime(...))</c>) so a member that
/// would silently fail to round-trip through the outbox fails here first, in a fast unit test, rather than
/// only when an event is routed durably.
/// </summary>
public sealed class DomainEventSerializationTests
{
    // Same configuration the Wolverine durable outbox uses (WolverineConfiguration.ConfigureWayd).
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    [Fact]
    public void TeamCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — a value object (TeamCode), an enum, NodaTime LocalDate/Instant, and primitives.
        var original = new TeamCreatedEvent(
            id: Guid.NewGuid(),
            key: 42,
            code: new TeamCode("ABC123"),
            name: "Replication Test Team",
            description: "A team used to exercise durable serialization.",
            type: TeamType.Team,
            activeDate: new LocalDate(2026, 1, 15),
            inactiveDate: new LocalDate(2026, 12, 31),
            isActive: true,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.Code.Value.Should().Be(original.Code.Value);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().Be(original.Description);
        roundTripped.Type.Should().Be(original.Type);
        roundTripped.ActiveDate.Should().Be(original.ActiveDate);
        roundTripped.InactiveDate.Should().Be(original.InactiveDate);
        roundTripped.IsActive.Should().Be(original.IsActive);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ProjectCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — ProjectKey value object, LocalDateRange value object, and the Dictionary<int, Guid[]>
        // roles collection, alongside NodaTime members.
        var roles = new Dictionary<int, Guid[]>
        {
            [1] = [Guid.NewGuid(), Guid.NewGuid()],
            [2] = [Guid.NewGuid()],
        };
        var strategicThemes = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var original = new ProjectCreatedEvent(
            project: new SimpleProjectStub(Guid.NewGuid(), new ProjectKey("PROJ01"), "Delivery Project", "desc"),
            expenditureCategoryId: 7,
            statusId: 3,
            dateRange: new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 6, 30)),
            portfolioId: Guid.NewGuid(),
            programId: Guid.NewGuid(),
            roles: roles,
            strategicThemes: strategicThemes,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Value.Should().Be(original.Key.Value);
        roundTripped.ExpenditureCategoryId.Should().Be(original.ExpenditureCategoryId);
        roundTripped.StatusId.Should().Be(original.StatusId);
        roundTripped.DateRange!.Start.Should().Be(original.DateRange!.Start);
        roundTripped.DateRange!.End.Should().Be(original.DateRange!.End);
        roundTripped.PortfolioId.Should().Be(original.PortfolioId);
        roundTripped.ProgramId.Should().Be(original.ProgramId);
        roundTripped.Roles.Should().BeEquivalentTo(original.Roles);
        roundTripped.StrategicThemes.Should().BeEquivalentTo(original.StrategicThemes);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ProgramCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — same aggregate-constructor shape as ProjectCreatedEvent (an ISimpleProgram param that
        // STJ cannot bind), fixed with a [JsonConstructor]; int Key rather than a value object.
        var roles = new Dictionary<int, Guid[]> { [1] = [Guid.NewGuid()] };

        var original = new ProgramCreatedEvent(
            project: new SimpleProgramStub(Guid.NewGuid(), 88, "Delivery Program", "desc"),
            statusId: 2,
            dateRange: new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 6, 30)),
            portfolioId: Guid.NewGuid(),
            roles: roles,
            strategicThemes: [Guid.NewGuid()],
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.StatusId.Should().Be(original.StatusId);
        roundTripped.DateRange!.Start.Should().Be(original.DateRange!.Start);
        roundTripped.PortfolioId.Should().Be(original.PortfolioId);
        roundTripped.Roles.Should().BeEquivalentTo(original.Roles);
        roundTripped.StrategicThemes.Should().BeEquivalentTo(original.StrategicThemes);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void IterationCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — an ISimpleIteration aggregate-constructor event (fixed with a [JsonConstructor]) whose
        // IterationDateRange value object carries nullable NodaTime Instants.
        var original = new IterationCreatedEvent(
            iteration: new SimpleIterationStub(
                Guid.NewGuid(),
                7,
                "Sprint 7",
                IterationType.Iteration,
                IterationState.Active,
                new IterationDateRange(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0)),
                Guid.NewGuid()),
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Type.Should().Be(original.Type);
        roundTripped.State.Should().Be(original.State);
        roundTripped.DateRange.Start.Should().Be(original.DateRange.Start);
        roundTripped.DateRange.End.Should().Be(original.DateRange.End);
        roundTripped.TeamId.Should().Be(original.TeamId);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void IntegrationStateChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — the closed-generic event whose IntegrationState<TId> has a private constructor and
        // init-only properties; this is the shape most at risk of failing to deserialize.
        var state = IntegrationState<Guid>.Create(Guid.NewGuid(), isActive: true);
        var original = new IntegrationStateChangedEvent<Guid>(
            SystemContext.WorkWorkProcess,
            state,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.SystemContext.Should().Be(original.SystemContext);
        roundTripped.IntegrationState.InternalId.Should().Be(original.IntegrationState.InternalId);
        roundTripped.IntegrationState.IsActive.Should().Be(original.IntegrationState.IsActive);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void StrategicThemeCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — an IStrategicThemeData aggregate-constructor event (fixed with a [JsonConstructor]);
        // primitives plus an enum State.
        var original = new StrategicThemeCreatedEvent(
            strategicTheme: new StrategicThemeDataStub(Guid.NewGuid(), 12, "Cloud Migration", "desc", StrategicThemeState.Active),
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().Be(original.Description);
        roundTripped.State.Should().Be(original.State);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException($"{typeof(T).Name} deserialized to null.");
    }

    /// <summary>Minimal <see cref="ISimpleProject"/> so the event can be constructed without the full aggregate.</summary>
    private sealed record SimpleProjectStub(Guid Id, ProjectKey Key, string Name, string Description) : ISimpleProject;

    /// <summary>Minimal <see cref="ISimpleProgram"/> so the event can be constructed without the full aggregate.</summary>
    private sealed record SimpleProgramStub(Guid Id, int Key, string Name, string Description) : ISimpleProgram;

    /// <summary>Minimal <see cref="ISimpleIteration"/> so the event can be constructed without the full aggregate.</summary>
    private sealed record SimpleIterationStub(
        Guid Id, int Key, string Name, IterationType Type, IterationState State, IterationDateRange DateRange, Guid? TeamId)
        : ISimpleIteration;

    /// <summary>Minimal <see cref="IStrategicThemeData"/> so the event can be constructed without the full aggregate.</summary>
    private sealed record StrategicThemeDataStub(Guid Id, int Key, string Name, string Description, StrategicThemeState State)
        : IStrategicThemeData;
}
