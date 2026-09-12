using System.Text.Json;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
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
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = RoundTrip(original);

        // Assert
        json.Should().Contain("\"Code\":\"ABC123\"");
        json.Should().NotContain("\"Code\":{\"Value\"");
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
            businessCase: "Replaces the manual reconciliation run each month.",
            expectedBenefits: "Two days of analyst time returned per month.",
            roles: roles,
            strategicThemes: strategicThemes,
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = RoundTrip(original);

        // Assert
        json.Should().Contain("\"Key\":\"PROJ01\"");
        json.Should().NotContain("\"Key\":{\"Value\"");
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
            EventActor.System,
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
            EventActor.System,
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
            EventActor.System,
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
            EventActor.System,
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

    [Fact]
    public void ProjectPortfolioCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - the Dictionary<int, Guid[]> roles collection alongside NodaTime members.
        var roles = new Dictionary<int, Guid[]>
        {
            [1] = [Guid.NewGuid()],
            [2] = [Guid.NewGuid(), Guid.NewGuid()],
        };

        var original = new ProjectPortfolioCreatedEvent(
            id: Guid.NewGuid(),
            key: 12,
            name: "Growth",
            description: "Growth portfolio",
            statusId: 1,
            roles: roles,
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 2, 1, 8, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.StatusId.Should().Be(original.StatusId);
        roundTripped.Roles.Should().BeEquivalentTo(original.Roles);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ProjectPortfolioStatusChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - FlexibleDateRange is the only value object with an optional end date, so it is the
        // one whose round-trip is not already covered by LocalDateRange.
        var original = new ProjectPortfolioStatusChangedEvent(
            id: Guid.NewGuid(),
            key: 12,
            fromStatus: "Active",
            fromCategory: LifecycleCategory.Active,
            toStatus: "Closed",
            toCategory: LifecycleCategory.Completed,
            dateRange: new FlexibleDateRange(new LocalDate(2025, 1, 1), new LocalDate(2026, 6, 30)),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 6, 30, 17, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.FromStatus.Should().Be(original.FromStatus);
        roundTripped.FromCategory.Should().Be(original.FromCategory);
        roundTripped.ToStatus.Should().Be(original.ToStatus);
        roundTripped.ToCategory.Should().Be(original.ToCategory);
        roundTripped.DateRange.Should().NotBeNull();
        roundTripped.DateRange!.Start.Should().Be(original.DateRange!.Start);
        roundTripped.DateRange.End.Should().Be(original.DateRange.End);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ProjectPortfolioStatusChangedEvent_WithAnOpenEndedRange_RoundTripsThroughDurableSerializer()
    {
        // Arrange - an activated portfolio has a start date and no end date yet.
        var original = new ProjectPortfolioStatusChangedEvent(
            id: Guid.NewGuid(),
            key: 12,
            fromStatus: "Proposed",
            fromCategory: LifecycleCategory.NotStarted,
            toStatus: "Active",
            toCategory: LifecycleCategory.Active,
            dateRange: new FlexibleDateRange(new LocalDate(2025, 1, 1)),
            EventActor.System,
            timestamp: Instant.FromUtc(2025, 1, 1, 9, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.DateRange.Should().NotBeNull();
        roundTripped.DateRange!.Start.Should().Be(original.DateRange!.Start);
        roundTripped.DateRange.End.Should().BeNull();
    }

    [Fact]
    public void ProgramTimelineChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - LocalDateRange value object, nullable so a cleared timeline round-trips too.
        var original = new ProgramTimelineChangedEvent(
            id: Guid.NewGuid(),
            key: 7,
            previousDateRange: null,
            dateRange: new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 9, 30)),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 5, 10, 15, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.DateRange.Should().NotBeNull();
        roundTripped.DateRange!.Start.Should().Be(original.DateRange!.Start);
        roundTripped.DateRange.End.Should().Be(original.DateRange.End);
        roundTripped.PreviousDateRange.Should().BeNull();
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void StrategicInitiativeCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - a LocalDateRange and the roles collection, on an event whose aggregate is the
        // portfolio rather than the record it names.
        var portfolioId = Guid.NewGuid();
        var original = new StrategicInitiativeCreatedEvent(
            portfolioId: portfolioId,
            strategicInitiativeId: Guid.NewGuid(),
            name: "Cloud Migration",
            dateRange: new LocalDateRange(new LocalDate(2026, 3, 1), new LocalDate(2026, 12, 31)),
            roles: new Dictionary<int, Guid[]> { [1] = [Guid.NewGuid()] },
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.PortfolioId.Should().Be(original.PortfolioId);
        roundTripped.StrategicInitiativeId.Should().Be(original.StrategicInitiativeId);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.DateRange.Start.Should().Be(original.DateRange.Start);
        roundTripped.Roles.Should().BeEquivalentTo(original.Roles);
        roundTripped.AggregateId.Should().Be(portfolioId);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ProjectReparentedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen V1 contract, as it was written before V2 replaced it. Nothing raises V1 any
        // more, so only a stored payload can prove the retired type still reads what it once wrote.
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": "ATLAS",
              "Name": "Atlas",
              "PortfolioId": "019f2a10-0000-7000-8000-000000000002",
              "ProgramId": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<ProjectReparentedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Atlas");
        restored.Key.Value.Should().Be("ATLAS");
        restored.ProgramId.Should().BeNull();
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void ProductLinkedExternallyEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen V1 contract, including the Description V2 dropped and an unlink's null link
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 42,
              "Name": "Checkout",
              "Description": null,
              "ExternalId": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<ProductLinkedExternallyEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Checkout");
        restored.Key.Should().Be(42);
        restored.Description.Should().BeNull();
        restored.ExternalId.Should().BeNull();
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void ProductLifecycleChangedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen V1 contract; its enums were written as numbers, so a renumbering would misread it
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 42,
              "Name": "Checkout",
              "FromStatusId": "019f2a10-0000-7000-8000-000000000004",
              "FromCategory": 1,
              "FromAlias": 1,
              "ToStatusId": "019f2a10-0000-7000-8000-000000000005",
              "ToCategory": 2,
              "ToAlias": 3,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<ProductLifecycleChangedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Checkout");
        restored.FromCategory.Should().Be(StatusCategory.Active);
        restored.FromAlias.Should().Be(ProductStatusAlias.Active);
        restored.ToCategory.Should().Be(StatusCategory.Done);
        restored.ToAlias.Should().Be(ProductStatusAlias.Retired);
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void StrategicThemeArchivedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new StrategicThemeArchivedEvent(Guid.NewGuid(), EventActor.System, Instant.FromUtc(2026, 9, 12, 9, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
        roundTripped.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void ProjectDetailsUpdatedEvent_PayloadWrittenBeforePreviousWasAdded_ReadsAsNotRecorded()
    {
        // Arrange - a 1.1 payload, written before the event carried the details it replaced
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": "ATLAS",
              "Name": "Atlas",
              "Description": "Atlas description",
              "ExpenditureCategoryId": 3,
              "BusinessCase": null,
              "ExpectedBenefits": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.1"
            }
            """;

        // Act
        var restored = JsonSerializer.Deserialize<ProjectDetailsUpdatedEvent>(payload, Options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Atlas");
        restored.Previous.Should().BeNull("a 1.1 payload did not record the details it replaced");
        restored.EventVersion.Should().Be("1.1", "the stored version says which shape the payload was written in");
    }

    [Fact]
    public void ProjectDetailsUpdatedEvent_RoundTripsAPreviousBusinessCaseThatWasEmpty()
    {
        // Arrange - the case the grouped Previous exists for: a null inside it is a real value
        var original = new ProjectDetailsUpdatedEvent(
            id: Guid.NewGuid(),
            key: new ProjectKey("ATLAS"),
            name: "Atlas",
            description: "Atlas description",
            expenditureCategoryId: 3,
            businessCase: "Consolidates three regional trackers.",
            expectedBenefits: null,
            previous: new ProjectDetails("Atlas", "Atlas description", 3, null, null),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 9, 10, 9, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Previous.Should().Be(original.Previous);
        roundTripped.BusinessCase.Should().Be(original.BusinessCase);
        roundTripped.EventVersion.Should().Be("1.2");
    }

    [Fact]
    public void ProjectReparentedEventV2_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new ProjectReparentedEventV2(
            id: Guid.NewGuid(),
            key: new ProjectKey("ATLAS"),
            portfolioId: Guid.NewGuid(),
            previousProgramId: Guid.NewGuid(),
            programId: Guid.NewGuid(),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 9, 10, 9, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Value.Should().Be(original.Key.Value);
        roundTripped.PortfolioId.Should().Be(original.PortfolioId);
        roundTripped.PreviousProgramId.Should().Be(original.PreviousProgramId);
        roundTripped.ProgramId.Should().Be(original.ProgramId);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
        roundTripped.EventVersion.Should().Be("2.0", "a type's generation and its version's major must agree");
    }

    [Fact]
    public void ProjectRolesChangedEventV2_RoundTripsThroughDurableSerializer()
    {
        // Arrange - the change as positional records, alongside the roster in the Created events' encoding.
        var arriving = Guid.NewGuid();
        var leaving = Guid.NewGuid();
        var original = new ProjectRolesChangedEventV2(
            id: Guid.NewGuid(),
            key: new ProjectKey("ATLAS"),
            added: [new RoleAssignmentChange(2, arriving)],
            removed: [new RoleAssignmentChange(3, leaving)],
            roles: new Dictionary<int, Guid[]> { [2] = [arriving] },
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 9, 10, 9, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Added.Should().Equal(original.Added);
        roundTripped.Removed.Should().Equal(original.Removed);
        roundTripped.Roles.Should().BeEquivalentTo(original.Roles);
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
