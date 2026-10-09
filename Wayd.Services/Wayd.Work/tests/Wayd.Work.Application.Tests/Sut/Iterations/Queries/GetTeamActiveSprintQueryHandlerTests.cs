using FluentAssertions;
using Xunit;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Queries;

public class GetTeamActiveSprintQueryHandlerTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new();
    private readonly GetTeamActiveSprintQueryHandler _handler;

    public GetTeamActiveSprintQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _handler = new GetTeamActiveSprintQueryHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider);
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_DuringASprintsPlannedDays_ReturnsItWithItsLifecycle()
    {
        // Act — the scenario's clock is the Friday before sprint 2
        var result = await _handler.Handle(new GetTeamActiveSprintQuery(_scenario.Team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(_scenario.Sprint1.Id);
        result.Team.Id.Should().Be(_scenario.Team.Id);
        result.State.Id.Should().Be((int)IterationState.Active);
        result.ActiveFrom.Should().Be(InChicago(Sprint1Start, 0));
        result.ActiveUntil.Should().Be(InChicago(Sprint2Start, 0));
        result.TimeZone.Should().Be("America/Chicago");
        result.CanComplete.Should().BeTrue();
        result.CanManageSprint.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FromTheStartOfTheNextSprintsFirstPlannedDayInTheTeamsZone_ReturnsIt()
    {
        // Arrange — midnight in Chicago, before any sync could have run
        _scenario.Clock.Reset(InChicago(Sprint2Start, 0));

        // Act
        var result = await _handler.Handle(new GetTeamActiveSprintQuery(_scenario.Team.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Id.Should().Be(_scenario.Sprint2.Id);
    }

    [Fact]
    public async Task Handle_ForAnActiveSprintMappedToAnIpIteration_IsNonStandardFromThePlanningInterval()
    {
        // Arrange
        _scenario.MappedCategories[_scenario.Sprint1.Id] = IterationCategory.InnovationAndPlanning;

        // Act
        var result = await _handler.Handle(new GetTeamActiveSprintQuery(_scenario.Team.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Id.Should().Be(_scenario.Sprint1.Id);
        result.SprintType.Should().Be(SprintType.NonStandard);
        result.SprintTypeSource.Should().Be(SprintTypeSource.PlanningInterval);
    }

    [Fact]
    public async Task Handle_AfterTheLastSprintEnded_ReturnsNothing()
    {
        // Arrange
        _scenario.Clock.Reset(InChicago(Sprint2Start.PlusDays(14), 0));

        // Act
        var result = await _handler.Handle(new GetTeamActiveSprintQuery(_scenario.Team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
