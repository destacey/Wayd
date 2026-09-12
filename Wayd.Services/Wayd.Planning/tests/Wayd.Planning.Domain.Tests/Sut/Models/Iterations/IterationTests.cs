using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
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
    public void Update_WhenValuesChange_RaisesUpdatedEvent()
    {
        // Arrange
        var iteration = _faker.AsFuture().Generate();

        // Act
        var result = iteration.Update("Sprint 42 ", iteration.Type, IterationState.Active, iteration.DateRange, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.Name.Should().Be("Sprint 42");
        iteration.State.Should().Be(IterationState.Active);
        iteration.DomainEvents.Should().ContainSingle(e => e is IterationUpdatedEvent);
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
}
