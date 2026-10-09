using FluentAssertions;
using NodaTime;
using Xunit;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Queries;

public class GetSprintQueryHandlerTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new(sprint1Started: InChicago(Sprint1Start, 10));
    private readonly GetSprintQueryHandler _handler;

    public GetSprintQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _handler = new GetSprintQueryHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider);
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_ForTheNextSprint_ReportsDefaultDatesAndTheStartThatCompletesTheOpenSprint()
    {
        // Act
        var result = await _handler.Handle(new GetSprintQuery(new IdOrKey(_scenario.Sprint2.Key.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.State.Id.Should().Be((int)IterationState.Future);
        result.Started.Should().BeNull();
        result.ActiveFrom.Should().Be(InChicago(Sprint2Start, 0));
        result.ActiveUntil.Should().Be(InChicago(Sprint2Start.PlusDays(14), 0));
        result.TimeZone.Should().Be("America/Chicago");
        result.CanStart.Should().BeTrue();
        result.CanComplete.Should().BeFalse();
        result.CanReopen.Should().BeFalse();
        result.OpenSprint!.Id.Should().Be(_scenario.Sprint1.Id);
        result.StartWindow!.Earliest.Should().Be(InChicago(Sprint1Start, 10).Plus(Duration.FromMilliseconds(1)));
        result.StartWindow.Latest.Should().BeNull();
        result.CompleteWindow.Should().BeNull();
        result.CanManageSprint.Should().BeTrue();
        result.OverlapsPreviousSprint.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForTheOpenSprint_ReportsItsActualStartAndThatItCanBeCompleted()
    {
        // Act
        var result = await _handler.Handle(new GetSprintQuery(new IdOrKey(_scenario.Sprint1.Key.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result!.State.Id.Should().Be((int)IterationState.Active);
        result.Started.Should().Be(InChicago(Sprint1Start, 10));
        result.ActiveFrom.Should().Be(result.Started);
        result.CanComplete.Should().BeTrue();
        result.CanStart.Should().BeFalse();
        result.OpenSprint.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAMember_CannotManageTheSprint()
    {
        // Arrange
        _scenario.IsMember = false;

        // Act
        var result = await _handler.Handle(new GetSprintQuery(new IdOrKey(_scenario.Sprint2.Key.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result!.CanManageSprint.Should().BeFalse();
        result.CanStart.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForASprintNeitherSetNorMapped_IsStandardByDefault()
    {
        // Act
        var result = await _handler.Handle(new GetSprintQuery(new IdOrKey(_scenario.Sprint2.Key.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result!.SprintType.Should().Be(SprintType.Standard);
        result.SprintTypeSource.Should().Be(SprintTypeSource.Default);
    }

    [Fact]
    public async Task Handle_ForASprintMappedToAnIpIteration_IsNonStandardFromThePlanningInterval()
    {
        // Arrange
        _scenario.MappedCategories[_scenario.Sprint2.Id] = IterationCategory.InnovationAndPlanning;

        // Act
        var result = await _handler.Handle(new GetSprintQuery(new IdOrKey(_scenario.Sprint2.Key.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result!.SprintType.Should().Be(SprintType.NonStandard);
        result.SprintTypeSource.Should().Be(SprintTypeSource.PlanningInterval);
    }

    [Fact]
    public async Task Handle_WhenTheTeamSetTheType_ItOverridesTheMapping()
    {
        // Arrange
        _scenario.MappedCategories[_scenario.Sprint2.Id] = IterationCategory.InnovationAndPlanning;
        _scenario.Sprint2.SetSprintType(SprintType.Standard, EventActor.System, _scenario.DateTimeProvider.Now);

        // Act
        var result = await _handler.Handle(new GetSprintQuery(new IdOrKey(_scenario.Sprint2.Key.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result!.SprintType.Should().Be(SprintType.Standard);
        result.SprintTypeSource.Should().Be(SprintTypeSource.Team);
    }
}
