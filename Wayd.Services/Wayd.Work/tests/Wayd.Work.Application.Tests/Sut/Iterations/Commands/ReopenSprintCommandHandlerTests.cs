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

public class ReopenSprintCommandHandlerTests : IDisposable
{
    private static readonly Instant Sprint1Completed = InChicago(Sprint2Start.PlusDays(-4), 12);

    private readonly SprintLifecycleScenario _scenario = new(sprint1Started: InChicago(Sprint1Start, 10), sprint1Completed: Sprint1Completed);
    private readonly ReopenSprintCommandHandler _handler;

    public ReopenSprintCommandHandlerTests()
    {
        _handler = new ReopenSprintCommandHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.CurrentUser.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider,
            Mock.Of<ILogger<ReopenSprintCommandHandler>>());
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_WhenTheTeamHasNotMovedOn_ClearsTheCompletion()
    {
        // Act
        var result = await _handler.Handle(new ReopenSprintCommand(_scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().BeNull();
        _scenario.Sprint1.DomainEvents.OfType<SprintReopenedEvent>().Should().ContainSingle()
            .Which.PreviousCompleted.Should().Be(Sprint1Completed);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheSprintIsNotCompleted_Fails()
    {
        // Act
        var result = await _handler.Handle(new ReopenSprintCommand(_scenario.Sprint2.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
