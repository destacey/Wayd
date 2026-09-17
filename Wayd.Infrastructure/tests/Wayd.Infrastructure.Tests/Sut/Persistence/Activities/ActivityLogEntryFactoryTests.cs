using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.Tests.Sut.Persistence.Activities;

/// <summary>
/// The summary is what the Activity section shows as an entry's title, so it is the one part of an entry
/// a reader sees before opening anything.
/// </summary>
public sealed class ActivityLogEntryFactoryTests
{
    [Fact]
    public void CreateActivityLogEntry_ForAnEventNamedAfterItsAggregate_DoesNotRepeatTheAggregate()
    {
        // Arrange — a multi-word aggregate, where the event name and the aggregate name only match once
        // the spaces the summary inserts are ignored.
        var raised = new ProjectPortfolioStubEvent("ProjectPortfolio", Guid.CreateVersion7());

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.Summary.Should().Be("Project Portfolio Stub");
    }

    [Fact]
    public void CreateActivityLogEntry_ForAnEventAboutSomethingElse_NamesTheAggregateInWords()
    {
        // Arrange — an event named for one record but recorded against another.
        var raised = new StrategicInitiativeStubEvent("ProjectPortfolio", Guid.CreateVersion7());

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.Summary.Should().Be("Strategic Initiative Stub on Project Portfolio");
    }

    [Fact]
    public void CreateActivityLogEntry_TakesTheAggregateFromTheEvent_NotTheRaisingType()
    {
        // Arrange
        var portfolioId = Guid.CreateVersion7();
        var raised = new StrategicInitiativeStubEvent("ProjectPortfolio", portfolioId);

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.AggregateType.Should().Be("ProjectPortfolio");
        entry.AggregateId.Should().Be(portfolioId);
        entry.DomainArea.Should().Be("App", "the stub lives outside every module namespace");
    }

    [Fact]
    public void CreateActivityLogEntry_ForASupersedingType_SummarisesWithoutTheGenerationSuffix()
    {
        // Arrange
        var raised = new ProjectPortfolioStubEventV2("ProjectPortfolio", Guid.CreateVersion7());

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert — the type name keeps the suffix, because consumers dispatch on it
        entry.Summary.Should().Be("Project Portfolio Stub");
        entry.EventType.Should().Be(nameof(ProjectPortfolioStubEventV2));
    }

    [Fact]
    public void CreateActivityLogEntry_RecordsTheCategoryTheEventTypeDeclares()
    {
        // Arrange
        var raised = new StrategicInitiativeStubEvent("ProjectPortfolio", Guid.CreateVersion7());

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.Category.Should().Be(ActivityCategory.Created);
    }

    [Fact]
    public void CreateActivityLogEntry_ForABaseline_SummarisesAsTheRecordBaselined()
    {
        // Arrange
        var raised = new ProjectBaselinedEvent(Guid.CreateVersion7(), new ProjectKey("APOLLO"), "Apollo", "A project.",
            1, 1, null, Guid.CreateVersion7(), null, null, null, [], [], null, null, Instant.FromUtc(2026, 9, 13, 8, 0));

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.Summary.Should().Be("Project Baselined");
        entry.DomainArea.Should().Be("Ppm");
        entry.EventVersion.Should().Be("1.1");
    }

    [Fact]
    public void CreateActivityLogEntry_ForAnEventNamingRelatedAggregates_RecordsThem()
    {
        // Arrange
        var first = new AggregateReference("ProjectPortfolio", Guid.CreateVersion7());
        var second = new AggregateReference("ProjectPortfolio", Guid.CreateVersion7());
        var raised = new RelatedStubEvent("ProjectPortfolio", Guid.CreateVersion7(), [first, second]);

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.RelatedAggregates.Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public void CreateActivityLogEntry_DropsTheEventsOwnAggregateAndRepeats_FromItsRelatedAggregates()
    {
        // Arrange — a record is never related to itself, and a record named twice is one row.
        var ownId = Guid.CreateVersion7();
        var other = new AggregateReference("ProjectPortfolio", Guid.CreateVersion7());
        var raised = new RelatedStubEvent("ProjectPortfolio", ownId,
            [new AggregateReference("ProjectPortfolio", ownId), other, other with { }]);

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.RelatedAggregates.Should().ContainSingle().Which.Should().BeEquivalentTo(other);
    }

    [Fact]
    public void CreateActivityLogEntry_TreatsTypesDifferingOnlyInCaseAsTheSameRecord()
    {
        // Arrange — the database compares types ignoring case, so either casing is one key in the related table.
        var ownId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();
        var raised = new RelatedStubEvent("ProjectPortfolio", ownId,
        [
            new AggregateReference("projectportfolio", ownId),
            new AggregateReference("Program", otherId),
            new AggregateReference("PROGRAM", otherId),
        ]);

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.RelatedAggregates.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new AggregateReference("Program", otherId));
    }

    [Fact]
    public void CreateActivityLogEntry_KeepsARecordOfAnotherTypeSharingTheOwnersId()
    {
        // Arrange — the log files entries by type and id together, so only both matching makes it the owner.
        var sharedId = Guid.CreateVersion7();
        var raised = new RelatedStubEvent("ProjectPortfolio", sharedId, [new AggregateReference("Program", sharedId)]);

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.RelatedAggregates.Should().ContainSingle().Which.AggregateType.Should().Be("Program");
    }

    [Fact]
    public void CreateActivityLogEntry_ForAnEventNamingNoRelatedAggregates_RecordsNone()
    {
        // Arrange
        var raised = new StrategicInitiativeStubEvent("ProjectPortfolio", Guid.CreateVersion7());

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        // Assert
        entry.RelatedAggregates.Should().BeEmpty();
    }

    private sealed record RelatedStubEvent(string AggregateType, Guid AggregateId, IReadOnlyCollection<AggregateReference> RelatedAggregates)
        : DomainEvent<RelatedStubEvent>(EventActor.System, "1.0"), IDomainEventDescriptor, IRelatedAggregateEvent
    {
        public static ActivityCategory ActivityCategory => ActivityCategory.Updated;
    }

    private sealed record ProjectPortfolioStubEventV2(string AggregateType, Guid AggregateId)
        : DomainEvent<ProjectPortfolioStubEventV2>(EventActor.System, "2.0"), IDomainEventDescriptor, IAggregateEvent
    {
        public static ActivityCategory ActivityCategory => ActivityCategory.Updated;
    }

    private sealed record ProjectPortfolioStubEvent(string AggregateType, Guid AggregateId)
        : DomainEvent<ProjectPortfolioStubEvent>(EventActor.System, "1.0"), IDomainEventDescriptor, IAggregateEvent
    {
        public static ActivityCategory ActivityCategory => ActivityCategory.Updated;
    }

    private sealed record StrategicInitiativeStubEvent(string AggregateType, Guid AggregateId)
        : DomainEvent<StrategicInitiativeStubEvent>(EventActor.System, "1.0"), IDomainEventDescriptor, IAggregateEvent
    {
        public static ActivityCategory ActivityCategory => ActivityCategory.Created;
    }
}
