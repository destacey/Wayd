using FluentAssertions;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.WorkManagement.WorkIterations;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkIterationTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public WorkIterationTests()
    {
        _dateTimeProvider = new(new DateTime(2026, 04, 12, 12, 0, 0));
    }

    [Fact]
    public void Update_WhenValuesChange_RaisesUpdatedEvent()
    {
        // Arrange
        var iteration = new WorkIterationFaker().Generate();
        var source = new WorkIterationFaker()
            .WithId(iteration.Id)
            .WithKey(iteration.Key)
            .WithName(iteration.Name)
            .WithDateRange(iteration.DateRange)
            .WithTeamId(iteration.TeamId)
            .WithState(IterationState.Active)
            .Generate();

        // Act
        var result = iteration.Update(source, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.State.Should().Be(IterationState.Active);
        iteration.DomainEvents.Should().ContainSingle(e => e is WorkIterationUpdatedEvent);
    }

    [Fact]
    public void Update_WhenNothingChanged_RaisesNoEvent()
    {
        // Arrange
        var iteration = new WorkIterationFaker().Generate();
        var source = new WorkIterationFaker()
            .WithId(iteration.Id)
            .WithKey(iteration.Key)
            .WithName(iteration.Name)
            .WithType(iteration.Type)
            .WithState(iteration.State)
            .WithDateRange(iteration.DateRange)
            .WithTeamId(iteration.TeamId)
            .Generate();

        // Act
        var result = iteration.Update(source, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithADifferentId_ReturnsFailure()
    {
        // Arrange
        var iteration = new WorkIterationFaker().Generate();
        var source = new WorkIterationFaker().Generate();

        // Act
        var result = iteration.Update(source, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        iteration.DomainEvents.Should().BeEmpty();
    }
}
