using NodaTime;
using Wayd.Common.Domain.Events;
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
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, correlationId: null);

        // Assert
        entry.Summary.Should().Be("Project Portfolio Stub");
    }

    [Fact]
    public void CreateActivityLogEntry_ForAnEventAboutSomethingElse_NamesTheAggregateInWords()
    {
        // Arrange — a strategic initiative is created through, and recorded against, its portfolio.
        var raised = new StrategicInitiativeStubEvent("ProjectPortfolio", Guid.CreateVersion7());

        // Act
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, correlationId: null);

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
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, correlationId: null);

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
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, correlationId: null);

        // Assert — the type name keeps the suffix, because consumers dispatch on it
        entry.Summary.Should().Be("Project Portfolio Stub");
        entry.EventType.Should().Be(nameof(ProjectPortfolioStubEventV2));
    }

    private sealed record ProjectPortfolioStubEventV2(string AggregateType, Guid AggregateId)
        : DomainEvent(EventActor.System), IAggregateEvent;

    private sealed record ProjectPortfolioStubEvent(string AggregateType, Guid AggregateId)
        : DomainEvent(EventActor.System), IAggregateEvent;

    private sealed record StrategicInitiativeStubEvent(string AggregateType, Guid AggregateId)
        : DomainEvent(EventActor.System), IAggregateEvent;
}
