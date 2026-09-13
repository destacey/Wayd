using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Domain.Tests.Sut.Models.Iterations;

public class IterationTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly IterationFaker _faker = new();

    public IterationTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    }

    [Fact]
    public void Update_WhenOnlyTheNameChanges_RaisesOnlyTheDetailsEvent()
    {
        // Arrange
        var iteration = _faker.Generate();
        var previous = new IterationDetails(iteration.Name, iteration.Type);

        // Act
        var result = iteration.Update("Sprint 42 ", iteration.Type, iteration.State, iteration.DateRange, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.Name.Should().Be("Sprint 42");
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationDetailsUpdatedEvent>().Subject;
        raised.Id.Should().Be(iteration.Id);
        raised.Key.Should().Be(iteration.Key);
        raised.Name.Should().Be("Sprint 42");
        raised.Type.Should().Be(iteration.Type);
        raised.Previous.Should().Be(previous);
    }

    [Fact]
    public void Update_WhenOnlyTheStateChanges_RaisesOnlyTheStateEvent()
    {
        // Arrange — the sync derives state from the dates, so it moves on its own as they pass
        var iteration = _faker.AsFuture().Generate();

        // Act
        iteration.Update(iteration.Name, iteration.Type, IterationState.Active, iteration.DateRange, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationStateChangedEvent>().Subject;
        raised.FromState.Should().Be(IterationState.Future);
        raised.ToState.Should().Be(IterationState.Active);
    }

    [Fact]
    public void Update_WhenTheDatesMove_RaisesTheDateRangeEventWithBothEnds()
    {
        // Arrange
        var iteration = _faker.Generate();
        var previous = iteration.DateRange;
        var moved = new IterationDateRange(previous.Start, previous.EffectiveEnd.Plus(Duration.FromDays(7)));

        // Act
        iteration.Update(iteration.Name, iteration.Type, iteration.State, moved, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationDateRangeChangedEvent>().Subject;
        raised.PreviousDateRange.Should().Be(previous);
        raised.DateRange.Should().Be(moved);
    }

    [Fact]
    public void Update_WhenTheTeamChanges_RaisesTheTeamEventWithBothEnds()
    {
        // Arrange
        var iteration = _faker.Generate();
        var previousTeamId = iteration.TeamId;
        var teamId = Guid.NewGuid();

        // Act
        iteration.Update(iteration.Name, iteration.Type, iteration.State, iteration.DateRange, teamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationTeamChangedEvent>().Subject;
        raised.PreviousTeamId.Should().Be(previousTeamId);
        raised.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void Update_WhenEverythingChanges_RaisesOneEventPerPart()
    {
        // Arrange
        var iteration = _faker.AsFuture().Generate();
        var moved = new IterationDateRange(iteration.DateRange.Start, iteration.DateRange.EffectiveEnd.Plus(Duration.FromDays(7)));
        var type = iteration.Type == IterationType.Sprint ? IterationType.Iteration : IterationType.Sprint;

        // Act
        iteration.Update("Renamed", type, IterationState.Active, moved, Guid.NewGuid(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        iteration.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(IterationDetailsUpdatedEvent),
            typeof(IterationDateRangeChangedEvent),
            typeof(IterationStateChangedEvent),
            typeof(IterationTeamChangedEvent));
    }

    [Fact]
    public void Update_WhenNothingChanged_RaisesNoEvent()
    {
        // Arrange — the values a sync sends back, before the setter trims the name
        var iteration = _faker.Generate();

        // Act
        var result = iteration.Update($"{iteration.Name} ", iteration.Type, iteration.State, iteration.DateRange, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_RaisesDeletedEvent()
    {
        // Arrange
        var iteration = _faker.Generate();

        // Act
        iteration.Delete(EventActor.Sync(null), _dateTimeProvider.Now);

        // Assert
        iteration.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IterationDeletedEvent>()
            .Which.Id.Should().Be(iteration.Id);
    }

    [Fact]
    public void Update_BeforeTheFirstSave_WaitsForTheKey()
    {
        // Arrange
        var range = new IterationDateRange(_dateTimeProvider.Now, _dateTimeProvider.Now.Plus(Duration.FromDays(14)));
        var iteration = Iteration.Create("Sprint 1", IterationType.Sprint, IterationState.Future, range, null,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, _dateTimeProvider.Now);

        // Act
        iteration.Update("Sprint 1a", IterationType.Sprint, IterationState.Future, range, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        iteration.DomainEvents.Should().BeEmpty();
        iteration.ExecutePostPersistenceActions();
        iteration.DomainEvents.OfType<IterationCreatedEvent>().Should().ContainSingle().Which.Name.Should().Be("Sprint 1");
        iteration.DomainEvents.OfType<IterationDetailsUpdatedEvent>().Should().ContainSingle().Which.Name.Should().Be("Sprint 1a");
    }
}
