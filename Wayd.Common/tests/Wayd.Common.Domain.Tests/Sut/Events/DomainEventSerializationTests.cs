using System.Text.Json;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Common.Domain.Events.Planning.Roadmaps;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.Scoring;
using Wayd.Common.Domain.Events.Settings;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.Events.WorkManagement.WorkIterations;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Settings;
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
    public void ApplicationUserLockedOutEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — an Instant in the payload, beside the one every event carries.
        var original = new ApplicationUserLockedOutEvent(
            Guid.NewGuid().ToString(),
            Instant.FromUtc(2026, 1, 15, 9, 45, 0),
            EventActor.System,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.UserId.Should().Be(original.UserId);
        roundTripped.LockedUntil.Should().Be(original.LockedUntil);
    }

    [Fact]
    public void ApplicationUserRolesChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — string arrays bound through the constructor, one of them empty.
        var original = new ApplicationUserRolesChangedEvent(
            Guid.NewGuid().ToString(),
            ["role-c"],
            [],
            ["role-a", "role-c"],
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Added.Should().Equal(original.Added);
        roundTripped.Removed.Should().BeEmpty();
        roundTripped.Roles.Should().Equal(original.Roles);
    }

    [Fact]
    public void ApplicationRoleDetailsUpdatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — a nested record whose own members can be null.
        var original = new ApplicationRoleDetailsUpdatedEvent(
            Guid.NewGuid().ToString(),
            "Lead Planner",
            null,
            new ApplicationRoleDetails("Planner", "Plans things"),
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().BeNull();
        roundTripped.Previous.Should().Be(original.Previous);
    }

    [Fact]
    public void SystemSettingsSectionValuesChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — JsonElement members, which must come back as the objects they were, not as strings.
        var original = new SystemSettingsSectionValuesChangedEvent(
            Guid.NewGuid(),
            "scheduling",
            SettingsScope.System,
            1,
            JsonSerializer.SerializeToElement(new { defaultTimeZone = "UTC", defaultCommitmentGraceDays = 1 }),
            JsonSerializer.SerializeToElement(new { defaultTimeZone = "Europe/London", defaultCommitmentGraceDays = 2 }),
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 9, 26, 12, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Key.Should().Be("scheduling");
        roundTripped.Scope.Should().Be(SettingsScope.System);
        roundTripped.SchemaVersion.Should().Be(1);
        roundTripped.Previous.GetProperty("defaultTimeZone").GetString().Should().Be("UTC");
        roundTripped.Current.GetProperty("defaultTimeZone").GetString().Should().Be("Europe/London");
        roundTripped.Current.GetProperty("defaultCommitmentGraceDays").GetInt32().Should().Be(2);
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
    public void IterationCreatedEventV3_RoundTripsThroughDurableSerializer()
    {
        // Arrange — an ISimpleIteration aggregate-constructor event (fixed with a [JsonConstructor]) whose
        // IterationDateRange value object carries nullable NodaTime LocalDates.
        var original = new IterationCreatedEventV3(
            iteration: new SimpleIterationStub(
                Guid.NewGuid(),
                7,
                "Sprint 7",
                IterationType.Iteration,
                new IterationDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 14)),
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
        roundTripped.DateRange.Start.Should().Be(original.DateRange.Start);
        roundTripped.DateRange.End.Should().Be(original.DateRange.End);
        roundTripped.TeamId.Should().Be(original.TeamId);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
        roundTripped.EventVersion.Should().Be("3.0");
    }

    [Fact]
    public void IterationCreatedEventV2_RoundTripsThroughDurableSerializer()
    {
        // Arrange
#pragma warning disable CS0618 // retired, but payloads written as it must still round-trip
        var original = new IterationCreatedEventV2(
            Guid.NewGuid(),
            7,
            "Sprint 7",
            IterationType.Iteration,
            IterationState.Active,
            new IterationDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 14)),
            Guid.NewGuid(),
            EventActor.System,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));
#pragma warning restore CS0618

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.State.Should().Be(original.State);
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.TeamId.Should().Be(original.TeamId);
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
        // Arrange - a LocalDateRange and the roles collection.
        var initiativeId = Guid.NewGuid();
        var original = new StrategicInitiativeCreatedEvent(
            portfolioId: Guid.NewGuid(),
            strategicInitiativeId: initiativeId,
            key: 12,
            name: "Cloud Migration",
            description: "Move the estate.",
            status: 1,
            dateRange: new LocalDateRange(new LocalDate(2026, 3, 1), new LocalDate(2026, 12, 31)),
            roles: new Dictionary<int, Guid[]> { [1] = [Guid.NewGuid()] },
            actor: EventActor.System,
            timestamp: Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.PortfolioId.Should().Be(original.PortfolioId);
        roundTripped.StrategicInitiativeId.Should().Be(original.StrategicInitiativeId);
        roundTripped.Key.Should().Be(12);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().Be(original.Description);
        roundTripped.Status.Should().Be(1);
        roundTripped.DateRange.Start.Should().Be(original.DateRange.Start);
        roundTripped.Roles.Should().BeEquivalentTo(original.Roles);
        roundTripped.AggregateId.Should().Be(initiativeId);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void StrategicInitiativeCreatedEvent_PayloadWrittenBefore1_1_ReadsNewFieldsAsNotRecorded()
    {
        // Arrange - a 1.0 payload, written before Key, Description and Status were added
        const string payload = """
            {
              "PortfolioId": "019f2a10-0000-7000-8000-000000000001",
              "StrategicInitiativeId": "019f2a10-0000-7000-8000-000000000002",
              "Name": "Cloud Migration",
              "DateRange": { "Start": "2026-03-01", "End": "2026-12-31" },
              "Roles": { "1": ["019f2a10-0000-7000-8000-000000000004"] },
              "Timestamp": "2026-03-01T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
        var restored = JsonSerializer.Deserialize<StrategicInitiativeCreatedEvent>(payload, Options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Key.Should().Be(0);
        restored.Description.Should().BeNull();
        restored.Status.Should().Be(0);
        restored.Name.Should().Be("Cloud Migration");
        restored.DateRange.End.Should().Be(new LocalDate(2026, 12, 31));
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void StrategicInitiativeKpiCheckpointPlanChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - nested records carrying Instants, including a revision holding both ends.
        var checkpoint = new StrategicInitiativeKpiCheckpointValues(
            Guid.NewGuid(), 50, 40, Instant.FromUtc(2026, 6, 30, 0, 0), "Q2");
        var revised = checkpoint with { TargetValue = 60, AtRiskValue = null };
        var original = new StrategicInitiativeKpiCheckpointPlanChangedEvent(
            Guid.NewGuid(), 12, Guid.NewGuid(),
            added: [],
            removed: [],
            revised: [new StrategicInitiativeKpiCheckpointRevision(checkpoint, revised)],
            checkpoints: [revised],
            EventActor.System,
            Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Revised.Should().ContainSingle().Which.Should().Be(original.Revised[0]);
        roundTripped.Checkpoints.Should().Equal(original.Checkpoints);
        roundTripped.KpiId.Should().Be(original.KpiId);
    }

    [Fact]
    public void StrategicInitiativeKpiMeasurementAddedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new StrategicInitiativeKpiMeasurementAddedEvent(
            Guid.NewGuid(), 12, Guid.NewGuid(), Guid.NewGuid(), 42.5,
            Instant.FromUtc(2026, 2, 27, 0, 0), Guid.NewGuid(), "Month end.",
            EventActor.System, Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void WorkflowCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - the status collection, whose elements carry an enum and a nullable description.
        var original = new WorkflowCreatedEvent(
            Guid.NewGuid(), 3, "Widget Workflow", null, "test.widget", isSystem: false, sourceWorkflowId: Guid.NewGuid(),
            statuses:
            [
                new WorkflowStatusValues(Guid.NewGuid(), "Proposed", null, StatusCategory.Proposed, 0, 1),
                new WorkflowStatusValues(Guid.NewGuid(), "Done", "Finished.", StatusCategory.Done, 12, 2),
            ],
            EventActor.System,
            Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Statuses.Should().Equal(original.Statuses);
        roundTripped.SourceWorkflowId.Should().Be(original.SourceWorkflowId);
        roundTripped.Description.Should().BeNull();
    }

    [Fact]
    public void ScoringModelCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - scales nesting their levels, decimals, and nullable weights, descriptions and scale ids.
        var scaleId = Guid.NewGuid();
        var original = new ScoringModelCreatedEvent(
            Guid.NewGuid(), 3, "WSJF", "Weighted shortest job first.",
            scales: [new ScoringScaleValues(scaleId, "Impact", 1,
                [new ScoringRatingLevelValues(Guid.NewGuid(), "High", 8.5m, 1), new ScoringRatingLevelValues(Guid.NewGuid(), "Low", 1m, 2)])],
            criteria:
            [
                new ScoringCriterionValues(Guid.NewGuid(), "Business Value", "BV", "Value delivered.", 1.25m, scaleId, 1),
                new ScoringCriterionValues(Guid.NewGuid(), "Job Size", "JS", null, null, null, 2),
            ],
            outputs: [new ScoringOutputValues(Guid.NewGuid(), "WSJF", "WSJF", "BV / JS", true, 1)],
            EventActor.System,
            Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ScoringModelPrimaryOutputChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - a nullable previous end.
        var original = new ScoringModelPrimaryOutputChangedEvent(
            Guid.NewGuid(), 3, previousOutputId: null, outputId: Guid.NewGuid(), EventActor.System, Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void PlanningIntervalCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - iteration records carrying an enum and a LocalDateRange, and the team and sprint collections.
        var iterationId = Guid.NewGuid();
        var original = new PlanningIntervalCreatedEvent(
            Guid.NewGuid(), 7, "PI 26.1", null,
            new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 29)),
            objectivesLocked: false,
            iterations:
            [
                new PlanningIntervalIterationValues(iterationId, "Iteration 1", IterationCategory.Development,
                    new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 2, 1))),
            ],
            teamIds: [Guid.NewGuid()],
            sprintMappings: [new PlanningIntervalSprintMapping(iterationId, Guid.NewGuid())],
            EventActor.System,
            Instant.FromUtc(2026, 1, 2, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Iterations.Should().Equal(original.Iterations);
        roundTripped.TeamIds.Should().Equal(original.TeamIds);
        roundTripped.SprintMappings.Should().Equal(original.SprintMappings);
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.Description.Should().BeNull();
    }

    [Fact]
    public void RoadmapCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - item records with an enum, a LocalDateRange and a nullable order, and color records.
        var activityId = Guid.NewGuid();
        var original = new RoadmapCreatedEvent(
            Guid.NewGuid(), 12, "Platform 2026", null,
            new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31)),
            Visibility.Public,
            RoadmapState.Active,
            managerIds: [Guid.NewGuid()],
            colors: [new RoadmapColorValues("#4096FF", "Committed", 1, true)],
            items:
            [
                new RoadmapItemValues(activityId, RoadmapItemType.Activity, "Discovery", null, null, "#4096FF",
                    new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31)), 1),
                new RoadmapItemValues(Guid.NewGuid(), RoadmapItemType.Milestone, "Beta", "Public beta", activityId, null,
                    new LocalDateRange(new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 1)), null),
            ],
            EventActor.System,
            Instant.FromUtc(2026, 1, 2, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Items.Should().Equal(original.Items);
        roundTripped.Colors.Should().Equal(original.Colors);
        roundTripped.ManagerIds.Should().Equal(original.ManagerIds);
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.Description.Should().BeNull();
    }

    [Fact]
    public void RoadmapItemDatesChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - a collection of change records carrying both ends as LocalDateRanges.
        var original = new RoadmapItemDatesChangedEvent(
            Guid.NewGuid(), 12,
            changes:
            [
                new RoadmapItemDateChange(Guid.NewGuid(), RoadmapItemType.Timebox,
                    new LocalDateRange(new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 14)),
                    new LocalDateRange(new LocalDate(2026, 2, 8), new LocalDate(2026, 2, 21))),
                new RoadmapItemDateChange(Guid.NewGuid(), RoadmapItemType.Activity,
                    new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 2, 14)),
                    new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 2, 21))),
            ],
            EventActor.System,
            Instant.FromUtc(2026, 1, 2, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Changes.Should().Equal(original.Changes);
    }

    [Fact]
    public void RoadmapItemDetailsUpdatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - a nullable color at both ends, and the replaced details as a record.
        var original = new RoadmapItemDetailsUpdatedEvent(
            Guid.NewGuid(), 12, Guid.NewGuid(), RoadmapItemType.Activity, "Discovery", null, null,
            new RoadmapItemDetails("Research", "Early research", "#4096FF"),
            EventActor.System,
            Instant.FromUtc(2026, 1, 2, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void PlanningIntervalObjectiveStatusChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - nullable Instants at both ends.
        var original = new PlanningIntervalObjectiveStatusChangedEvent(
            Guid.NewGuid(), 31, ObjectiveStatus.Completed, ObjectiveStatus.InProgress,
            previousClosedDate: Instant.FromUtc(2026, 2, 27, 16, 30), closedDate: null,
            EventActor.System, Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void PlanningIntervalObjectiveCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - nullable LocalDates, a nullable Instant and a fractional progress.
        var original = new PlanningIntervalObjectiveCreatedEvent(
            Guid.NewGuid(), 31, Guid.NewGuid(), Guid.NewGuid(), "Ship the thing", "Because.",
            PlanningIntervalObjectiveType.Team, ObjectiveStatus.Completed, 62.5, isStretch: true,
            new LocalDate(2026, 1, 12), targetDate: null, Instant.FromUtc(2026, 2, 27, 16, 30), order: 2,
            EventActor.System, Instant.FromUtc(2026, 3, 1, 12, 0, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void RiskCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange - the ROAM grades, a follow-up LocalDate and optional people.
        var original = new RiskCreatedEvent(
            Guid.NewGuid(), 14, "Vendor slip", null, Guid.NewGuid(), Instant.FromUtc(2026, 2, 1, 9, 0), Guid.NewGuid(),
            RiskStatus.Open, RiskCategory.Owned, RiskGrade.High, RiskGrade.Medium, assigneeId: null,
            new LocalDate(2026, 2, 8), "Escalated", closedDate: null,
            EventActor.System, Instant.FromUtc(2026, 2, 1, 9, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void TeamDetailsUpdatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — the TeamCode value object appears both on the event and inside the nested Previous record.
        var original = new TeamDetailsUpdatedEvent(
            id: Guid.NewGuid(),
            key: 42,
            code: new TeamCode("BOR"),
            name: "Borealis",
            description: null,
            previous: new TeamDetails(new TeamCode("ATL"), "Atlas", "The first team."),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Key.Should().Be(42);
        roundTripped.Code.Should().Be(original.Code);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().BeNull();
        roundTripped.Previous.Should().Be(original.Previous);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void TeamMemberRolesChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — three Guid collections, one of them empty
        var original = new TeamMemberRolesChangedEvent(
            id: Guid.NewGuid(),
            key: 42,
            employeeId: Guid.NewGuid(),
            addedRoleIds: [Guid.NewGuid()],
            removedRoleIds: [],
            roleIds: [Guid.NewGuid(), Guid.NewGuid()],
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void TeamMembershipDatesChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — one range open-ended, the other closed
        var original = new TeamMembershipDatesChangedEvent(
            id: Guid.NewGuid(),
            key: 42,
            parentTeamId: Guid.NewGuid(),
            dateRange: new FlexibleDateRange(new LocalDate(2025, 1, 1), new LocalDate(2026, 6, 30)),
            previousDateRange: new FlexibleDateRange(new LocalDate(2025, 1, 1)),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.PreviousDateRange.Should().Be(original.PreviousDateRange);
        roundTripped.ParentTeamId.Should().Be(original.ParentTeamId);
    }

    [Fact]
    public void TeamOperatingModelSetEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — the nested settings record carries two enums
        var original = new TeamOperatingModelSetEvent(
            id: Guid.NewGuid(),
            key: 42,
            period: new FlexibleDateRange(new LocalDate(2026, 1, 1)),
            settings: new TeamOperatingModelSettings(Methodology.Kanban, SizingMethod.Size, "America/Chicago", 2, WorkingWeek.MondayToFriday.Days),
            supersededPeriod: new FlexibleDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31)),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Period.Should().Be(original.Period);
        roundTripped.Settings.Should().Be(original.Settings);
        roundTripped.SupersededPeriod.Should().Be(original.SupersededPeriod);
    }

    [Fact]
    public void TeamOperatingModelCorrectedEvent_PayloadWrittenBeforeHolidayCalendars_StillDeserializes()
    {
        // Arrange — the 1.1 shape, which the working-days backfill writes: working days, no holiday calendar
        var payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 42,
              "Period": { "Start": "2026-01-01", "End": null },
              "Settings": { "Methodology": 1, "SizingMethod": 1, "TimeZone": "UTC", "CommitmentGraceDays": 1, "WorkingDays": [1, 2, 3, 4, 5] },
              "Previous": { "Methodology": 1, "SizingMethod": 1, "TimeZone": "UTC", "CommitmentGraceDays": 1, "WorkingDays": null },
              "Timestamp": "2026-10-08T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 1, "UserId": "11111111-1111-1111-1111-111111111111", "EmployeeId": null },
              "EventVersion": "1.1"
            }
            """;

        // Act
        var restored = JsonSerializer.Deserialize<TeamOperatingModelCorrectedEvent>(payload, Options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Settings.Should().Be(new TeamOperatingModelSettings(Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday.Days));
        restored.Settings.HolidayCalendarId.Should().BeNull();
        restored.Previous.WorkingDays.Should().BeNull();
        restored.EventVersion.Should().Be("1.1");
    }

    [Fact]
    public void TeamOperatingModelSetEvent_PayloadWrittenBeforeWorkingDays_StillDeserializes()
    {
        // Arrange
        var payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 42,
              "Period": { "Start": "2026-01-01", "End": null },
              "Settings": { "Methodology": 2, "SizingMethod": 4, "TimeZone": "America/Chicago", "CommitmentGraceDays": 2 },
              "SupersededPeriod": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
        var restored = JsonSerializer.Deserialize<TeamOperatingModelSetEvent>(payload, Options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Settings.Should().Be(new TeamOperatingModelSettings(Methodology.Kanban, SizingMethod.Size, "America/Chicago", 2));
        restored.Settings.WorkingDays.Should().BeNull();
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void TeamOperatingModelCorrectedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new TeamOperatingModelCorrectedEvent(
            id: Guid.NewGuid(),
            key: 42,
            period: new FlexibleDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31)),
            settings: new TeamOperatingModelSettings(Methodology.Scrum, SizingMethod.Effort, "Europe/London", 1, WorkingWeek.MondayToFriday.Days),
            previous: new TeamOperatingModelSettings(Methodology.Scrum, SizingMethod.Count, "UTC", 0, WorkingWeek.MondayToFriday.Days),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Period.Should().Be(original.Period);
        roundTripped.Settings.Should().Be(original.Settings);
        roundTripped.Previous.Should().Be(original.Previous);
    }

    [Fact]
    public void TeamOfTeamsOperatingModelSetEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new TeamOfTeamsOperatingModelSetEvent(
            id: Guid.NewGuid(),
            key: 42,
            period: new FlexibleDateRange(new LocalDate(2026, 1, 1)),
            settings: new TeamOfTeamsOperatingModelSettings("America/Chicago"),
            supersededPeriod: new FlexibleDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31)),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Period.Should().Be(original.Period);
        roundTripped.Settings.Should().Be(original.Settings);
        roundTripped.SupersededPeriod.Should().Be(original.SupersededPeriod);
    }

    [Fact]
    public void TeamOfTeamsOperatingModelCorrectedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new TeamOfTeamsOperatingModelCorrectedEvent(
            id: Guid.NewGuid(),
            key: 42,
            period: new FlexibleDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31)),
            settings: new TeamOfTeamsOperatingModelSettings("Europe/London"),
            previous: new TeamOfTeamsOperatingModelSettings("UTC"),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Period.Should().Be(original.Period);
        roundTripped.Settings.Should().Be(original.Settings);
        roundTripped.Previous.Should().Be(original.Previous);
    }

    [Fact]
    public void TeamOfTeamsOperatingModelRemovedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new TeamOfTeamsOperatingModelRemovedEvent(
            id: Guid.NewGuid(),
            key: 42,
            period: new FlexibleDateRange(new LocalDate(2026, 1, 1)),
            settings: new TeamOfTeamsOperatingModelSettings("Asia/Tokyo"),
            reinstatedPeriod: new FlexibleDateRange(new LocalDate(2025, 1, 1)),
            EventActor.System,
            timestamp: Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Period.Should().Be(original.Period);
        roundTripped.Settings.Should().Be(original.Settings);
        roundTripped.ReinstatedPeriod.Should().Be(original.ReinstatedPeriod);
    }

    [Fact]
    public void TeamDeactivatedEvent_PayloadWrittenBefore1_1_ReadsKeyAndCodeAsNotRecorded()
    {
        // Arrange - a 1.0 payload, written before Key and Code were added
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "InactiveDate": "2026-09-30",
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
        var restored = JsonSerializer.Deserialize<TeamDeactivatedEvent>(payload, Options);

        // Assert
        restored.Should().NotBeNull();
        restored!.Key.Should().Be(0);
        restored.Code.Should().BeNull();
        restored.InactiveDate.Should().Be(new LocalDate(2026, 9, 30));
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void TeamActivatedEvent_RoundTripsKeyAndCode()
    {
        // Arrange
        var original = new TeamActivatedEvent(Guid.NewGuid(), 42, new TeamCode("ATL"), EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Key.Should().Be(42);
        roundTripped.Code.Should().Be(new TeamCode("ATL"));
        roundTripped.EventVersion.Should().Be("1.1");
    }

    [Fact]
    public void IterationDateRangeChangedEventV2_RoundTripsBothEnds()
    {
        // Arrange — an open-ended range on one side, so a null date inside the value object is covered.
        var original = new IterationDateRangeChangedEventV2(
            Guid.NewGuid(),
            7,
            new IterationDateRange(new LocalDate(2026, 1, 1), null),
            new IterationDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 14)),
            EventActor.System,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Key.Should().Be(7);
        roundTripped.PreviousDateRange.Should().Be(original.PreviousDateRange);
        roundTripped.PreviousDateRange.End.Should().BeNull();
        roundTripped.DateRange.Should().Be(original.DateRange);
    }

    [Fact]
    public void SprintActualDatesCorrectedEvent_RoundTripsBothEnds()
    {
        // Arrange — nulls on both sides, so "takes the default" survives the round trip
        var original = new SprintActualDatesCorrectedEvent(
            Guid.NewGuid(),
            7,
            new SprintActualDates(null, Instant.FromUtc(2026, 9, 28, 14, 0)),
            new SprintActualDates(Instant.FromUtc(2026, 9, 14, 15, 0), null),
            EventActor.System,
            Instant.FromUtc(2026, 10, 2, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Key.Should().Be(7);
        roundTripped.Previous.Should().Be(original.Previous);
        roundTripped.Current.Should().Be(original.Current);
    }

    [Fact]
    public void WorkIterationDateRangeChangedEventV2_RoundTripsBothEnds()
    {
        // Arrange
#pragma warning disable CS0618 // retired, but payloads written as it must still round-trip
        var original = new WorkIterationDateRangeChangedEventV2(
            Guid.NewGuid(),
            7,
            new IterationDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 14)),
            new IterationDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 16)),
            EventActor.System,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));
#pragma warning restore CS0618

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.PreviousDateRange.Should().Be(original.PreviousDateRange);
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.EventVersion.Should().Be("2.0");
    }

    // The first generation serialized every public getter of the old value object, so its payloads carry
    // EffectiveStart, EffectiveEnd and Days beside the instants. They must still bind.
    private const string IterationDateRangeV1Json =
        """{ "Start": "2026-01-05T00:00:00Z", "End": "2026-01-16T00:00:00Z", "EffectiveStart": "2026-01-05T00:00:00Z", "EffectiveEnd": "2026-01-16T00:00:00Z", "Days": 12 }""";

    [Fact]
    public void IterationCreatedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange
        var payload = $$"""
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 7,
              "Name": "Sprint 7",
              "Type": 2,
              "State": 2,
              "DateRange": {{IterationDateRangeV1Json}},
              "TeamId": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<IterationCreatedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Key.Should().Be(7);
        restored.DateRange.Start.Should().Be(Instant.FromUtc(2026, 1, 5, 0, 0));
        restored.DateRange.ToIterationDateRange().Should().Be(new IterationDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 16)));
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void IterationDateRangeChangedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange
        var payload = $$"""
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 7,
              "PreviousDateRange": { "Start": "2026-01-01T00:00:00Z", "End": null, "EffectiveStart": "2026-01-01T00:00:00Z", "EffectiveEnd": "9999-12-31T23:59:59.999999999Z", "Days": 2916000 },
              "DateRange": {{IterationDateRangeV1Json}},
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<IterationDateRangeChangedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.PreviousDateRange.End.Should().BeNull();
        restored.PreviousDateRange.ToIterationDateRange().Should().Be(new IterationDateRange(new LocalDate(2026, 1, 1), null));
        restored.DateRange.End.Should().Be(Instant.FromUtc(2026, 1, 16, 0, 0));
    }

    [Fact]
    public void WorkIterationDateRangeChangedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange
        var payload = $$"""
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 7,
              "PreviousDateRange": {{IterationDateRangeV1Json}},
              "DateRange": {{IterationDateRangeV1Json}},
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<WorkIterationDateRangeChangedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.DateRange.Start.Should().Be(Instant.FromUtc(2026, 1, 5, 0, 0));
        restored.DateRange.End.Should().Be(Instant.FromUtc(2026, 1, 16, 0, 0));
    }

    [Fact]
    public void IterationBaselinedEventV3_RoundTripsThroughDurableSerializer()
    {
        // Arrange
        var original = new IterationBaselinedEventV3(
            Guid.NewGuid(),
            7,
            "Sprint 7",
            IterationType.Sprint,
            new IterationDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 16)),
            Guid.NewGuid(),
            recordCreatedOn: Instant.FromUtc(2025, 12, 20, 8, 0),
            recordCreatedById: null,
            timestamp: Instant.FromUtc(2026, 9, 7, 12, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.EventId.Should().Be(original.EventId);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.TeamId.Should().Be(original.TeamId);
        roundTripped.RecordCreatedOn.Should().Be(original.RecordCreatedOn);
        roundTripped.EventVersion.Should().Be("3.0");
    }

    [Fact]
    public void IterationBaselinedEventV2_RoundTripsThroughDurableSerializer()
    {
        // Arrange
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var original = new IterationBaselinedEventV2(
            Guid.NewGuid(),
            7,
            "Sprint 7",
            IterationType.Sprint,
            IterationState.Completed,
            new IterationDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 16)),
            Guid.NewGuid(),
            recordCreatedOn: Instant.FromUtc(2025, 12, 20, 8, 0),
            recordCreatedById: null,
            timestamp: Instant.FromUtc(2026, 9, 7, 12, 0));
#pragma warning restore CS0618

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.EventId.Should().Be(original.EventId);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.State.Should().Be(original.State);
        roundTripped.DateRange.Should().Be(original.DateRange);
        roundTripped.TeamId.Should().Be(original.TeamId);
        roundTripped.RecordCreatedOn.Should().Be(original.RecordCreatedOn);
        roundTripped.EventVersion.Should().Be("2.0");
    }

    [Fact]
    public void IterationBaselinedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange
        var payload = $$"""
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 7,
              "Name": "Sprint 7",
              "Type": 2,
              "State": 3,
              "DateRange": {{IterationDateRangeV1Json}},
              "TeamId": "019f2a10-0000-7000-8000-000000000002",
              "RecordCreatedOn": "2025-12-20T08:00:00Z",
              "RecordCreatedById": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": null, "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<IterationBaselinedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Sprint 7");
        restored.DateRange.End.Should().Be(Instant.FromUtc(2026, 1, 16, 0, 0));
        restored.RecordCreatedOn.Should().Be(Instant.FromUtc(2025, 12, 20, 8, 0));
    }

    [Fact]
    public void IterationDetailsUpdatedEvent_RoundTripsThePreviousDetails()
    {
        // Arrange
        var original = new IterationDetailsUpdatedEvent(
            Guid.NewGuid(), 7, "Sprint 7a", IterationType.Sprint, new IterationDetails("Sprint 7", IterationType.Iteration),
            EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Name.Should().Be("Sprint 7a");
        roundTripped.Type.Should().Be(IterationType.Sprint);
        roundTripped.Previous.Should().Be(original.Previous);
    }

    [Fact]
    public void IterationUpdatedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen whole-record contract; its enums were written as numbers
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Key": 7,
              "Name": "Sprint 7",
              "Type": 2,
              "State": 2,
              "DateRange": { "Start": "2026-01-01T00:00:00Z", "End": "2026-01-14T00:00:00Z" },
              "TeamId": "019f2a10-0000-7000-8000-000000000002",
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<IterationUpdatedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Key.Should().Be(7);
        restored.Type.Should().Be(IterationType.Sprint);
        restored.State.Should().Be(IterationState.Active);
        restored.DateRange.End.Should().Be(Instant.FromUtc(2026, 1, 14, 0, 0));
        restored.TeamId.Should().Be(Guid.Parse("019f2a10-0000-7000-8000-000000000002"));
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void WorkIterationUpdatedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen whole-record contract, which never carried Key
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Name": "Sprint 7",
              "Type": 1,
              "State": 3,
              "DateRange": { "Start": "2026-01-01T00:00:00Z", "End": null },
              "TeamId": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<WorkIterationUpdatedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Sprint 7");
        restored.Type.Should().Be(IterationType.Iteration);
        restored.State.Should().Be(IterationState.Future);
        restored.DateRange.End.Should().BeNull();
        restored.TeamId.Should().BeNull();
        restored.EventVersion.Should().Be("1.0");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StrategicThemeDetailsUpdatedEvent_RoundTripsThePreviousDetails(bool recorded)
    {
        // Arrange
        var original = new StrategicThemeDetailsUpdatedEvent(
            Guid.NewGuid(), 7, "Cloud Migration", "Move every workload off the data centre.",
            recorded ? new StrategicThemeDetails("Cloud", "Move workloads.") : null,
            EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Key.Should().Be(7);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().Be(original.Description);
        roundTripped.Previous.Should().Be(original.Previous);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnvironmentDetailsUpdatedEvent_RoundTripsThePreviousDetails(bool recorded)
    {
        // Arrange
        var original = new EnvironmentDetailsUpdatedEvent(
            Guid.NewGuid(), 7, "Production EU", 5,
            recorded ? new EnvironmentDetails("prod-eu", 2) : null,
            EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Key.Should().Be(7);
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.RingOrder.Should().Be(original.RingOrder);
        roundTripped.Previous.Should().Be(original.Previous);
    }

    [Fact]
    public void StrategicThemeUpdatedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen whole-record contract; its State was written as a number
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Name": "Cloud Migration",
              "Description": "Move every workload off the data centre.",
              "State": 2,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<StrategicThemeUpdatedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Cloud Migration");
        restored.Description.Should().Be("Move every workload off the data centre.");
        restored.State.Should().Be(StrategicThemeState.Active);
        restored.EventVersion.Should().Be("1.0");
    }

    [Fact]
    public void TeamUpdatedEvent_PayloadWrittenBeforeItWasSuperseded_StillDeserializes()
    {
        // Arrange - the frozen whole-record contract, as it was written before TeamDetailsUpdatedEvent replaced it
        const string payload = """
            {
              "Id": "019f2a10-0000-7000-8000-000000000001",
              "Code": "ATLAS",
              "Name": "Atlas",
              "Description": null,
              "Timestamp": "2026-09-07T12:00:00Z",
              "EventId": "019f2a10-0000-7000-8000-000000000003",
              "Actor": { "Kind": 0, "UserId": "user-42", "EmployeeId": null },
              "EventVersion": "1.0"
            }
            """;

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        var restored = JsonSerializer.Deserialize<TeamUpdatedEvent>(payload, Options);
#pragma warning restore CS0618

        // Assert
        restored.Should().NotBeNull();
        restored!.Code.Value.Should().Be("ATLAS");
        restored.Name.Should().Be("Atlas");
        restored.Description.Should().BeNull();
        restored.EventVersion.Should().Be("1.0");
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

    [Fact]
    public void PersonalAccessTokenExpirationChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — two Instants in the payload, beside the one every event carries.
        var original = new PersonalAccessTokenExpirationChangedEvent(
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Instant.FromUtc(2026, 3, 1, 0, 0, 0),
            Instant.FromUtc(2026, 6, 1, 0, 0, 0),
            EventActor.User("user-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.UserId.Should().Be(original.UserId);
        roundTripped.PreviousExpiresAt.Should().Be(original.PreviousExpiresAt);
        roundTripped.ExpiresAt.Should().Be(original.ExpiresAt);
    }

    [Fact]
    public void OidcProviderCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — an enum and nested records holding string arrays, one of them null.
        var original = new OidcProviderCreatedEvent(
            Guid.NewGuid(),
            "acme-okta",
            "Acme Okta",
            OidcProviderType.GenericOidc,
            new OidcProviderConfiguration("https://acme.example", "client-1", "api://client-1", ["openid", "profile"], null, 60),
            new OidcProviderRegistrationPolicy(true, false, "role-1"),
            true,
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Label.Should().Be(original.Label);
        roundTripped.ProviderType.Should().Be(original.ProviderType);
        roundTripped.Configuration.IsEquivalentTo(original.Configuration).Should().BeTrue();
        roundTripped.Configuration.AllowedTenantIds.Should().BeNull();
        roundTripped.RegistrationPolicy.Should().Be(original.RegistrationPolicy);
        roundTripped.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void OidcProviderConfigurationChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — both ends as nested records with arrays.
        var original = new OidcProviderConfigurationChangedEvent(
            Guid.NewGuid(),
            new OidcProviderConfiguration("https://login.example", "client-1", "client-1", ["openid"], ["tenant-1", "tenant-2"], 120),
            new OidcProviderConfiguration("https://login.example", "client-1", "client-1", ["openid"], ["tenant-1"], 60),
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Configuration.AllowedTenantIds.Should().Equal(original.Configuration.AllowedTenantIds);
        roundTripped.Configuration.ClockSkewSeconds.Should().Be(120);
        roundTripped.Previous.AllowedTenantIds.Should().Equal(original.Previous.AllowedTenantIds);
    }

    [Fact]
    public void ConnectionConfigurationChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — arrays of records whose values can be null.
        var original = new ConnectionConfigurationChangedEvent(
            Guid.NewGuid(),
            [new ConnectionSetting("TenantId", "tenant-1"), new ConnectionSetting("AllUsersGroupObjectId", "group-1")],
            [new ConnectionSetting("TenantId", "tenant-1"), new ConnectionSetting("AllUsersGroupObjectId", null)],
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Settings.Should().Equal(original.Settings);
        roundTripped.Previous.Should().Equal(original.Previous);
    }

    [Fact]
    public void ConnectionCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — an enum, a nullable description and an array of records.
        var original = new ConnectionCreatedEvent(
            Guid.NewGuid(),
            "Boards",
            null,
            Connector.AzureDevOps,
            true,
            [new ConnectionSetting("Organization", "acme")],
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Description.Should().BeNull();
        roundTripped.Connector.Should().Be(Connector.AzureDevOps);
        roundTripped.Settings.Should().Equal(original.Settings);
    }

    [Fact]
    public void ExternalIdentityMappingCreatedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — two enums and a nullable employee id.
        var original = new ExternalIdentityMappingCreatedEvent(
            Guid.NewGuid(),
            Connector.AzureDevOps,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExternalIdentityMappingStatus.AutoMatched,
            EventActor.System,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Connector.Should().Be(Connector.AzureDevOps);
        roundTripped.ConnectionId.Should().Be(original.ConnectionId);
        roundTripped.EmployeeId.Should().Be(original.EmployeeId);
        roundTripped.Status.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void EmployeeManagerChangedEvent_RoundTripsThroughDurableSerializer()
    {
        // Arrange — two nullable ids, one of them null.
        var original = new EmployeeManagerChangedEvent(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            EventActor.User("admin-1"),
            Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Act
        var roundTripped = RoundTrip(original);

        // Assert
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.PreviousManagerId.Should().BeNull();
        roundTripped.ManagerId.Should().Be(original.ManagerId);
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
        Guid Id, int Key, string Name, IterationType Type, IterationDateRange DateRange, Guid? TeamId)
        : ISimpleIteration;

    /// <summary>Minimal <see cref="IStrategicThemeData"/> so the event can be constructed without the full aggregate.</summary>
    private sealed record StrategicThemeDataStub(Guid Id, int Key, string Name, string Description, StrategicThemeState State)
        : IStrategicThemeData;
}
