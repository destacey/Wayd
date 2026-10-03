using FluentAssertions;
using NodaTime;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Application.Tests.Infrastructure;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Commands;

public class CompleteSprintCommandHandlerTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new(sprint1Started: InChicago(Sprint1Start, 10));
    private readonly CompleteSprintCommandHandler _handler;

    public CompleteSprintCommandHandlerTests()
    {
        _handler = new CompleteSprintCommandHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.CurrentUser.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider,
            Mock.Of<ILogger<CompleteSprintCommandHandler>>());
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_WhenAMemberCompletesTheOpenSprint_RecordsTheInstant()
    {
        // Arrange
        var now = _scenario.DateTimeProvider.Now;

        // Act
        var result = await _handler.Handle(new CompleteSprintCommand(_scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().Be(now);
        _scenario.Sprint1.DomainEvents.OfType<SprintCompletedEvent>().Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAnEarlierCompletion_RecordsIt()
    {
        // Arrange
        var completedAt = InChicago(Sprint2Start.PlusDays(-4), 17);

        // Act
        var result = await _handler.Handle(new CompleteSprintCommand(_scenario.Sprint1.Id, completedAt), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().Be(completedAt);
    }

    [Fact]
    public async Task Handle_WithACompletionBeforeTheStart_Fails()
    {
        // Act
        var result = await _handler.Handle(new CompleteSprintCommand(_scenario.Sprint1.Id, InChicago(Sprint1Start, 9)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheSprintHasNotReachedItsStart_FailsAndSavesNothing()
    {
        // Act — sprint 2's default start is the end of its first planned day, still three days away
        var result = await _handler.Handle(new CompleteSprintCommand(_scenario.Sprint2.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint2.Completed.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
