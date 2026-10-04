using FluentAssertions;
using NodaTime;
using Xunit;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Tests.Data;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Queries;

public class GetIterationStatesQueryHandlerTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new(sprint1Started: InChicago(Sprint1Start, 10));
    private readonly GetIterationStatesQueryHandler _handler;

    public GetIterationStatesQueryHandlerTests()
    {
        _handler = new GetIterationStatesQueryHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.DateTimeProvider);
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_WorksOutEachIterationsStateNow_AndLeavesOutUnknownIds()
    {
        // Arrange
        var unknown = Guid.NewGuid();

        // Act — the scenario's clock is the Friday before sprint 2
        var result = await _handler.Handle(
            new GetIterationStatesQuery([_scenario.Sprint1.Id, _scenario.Sprint2.Id, unknown]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result[_scenario.Sprint1.Id].Should().Be(new IterationStateDto(
            IterationState.Active, InChicago(Sprint1Start, 10), InChicago(Sprint2Start, 0), "America/Chicago"));
        result[_scenario.Sprint2.Id].Should().Be(new IterationStateDto(
            IterationState.Future, InChicago(Sprint2Start, 0), InChicago(Sprint2Start.PlusDays(14), 0), "America/Chicago"));
    }

    [Fact]
    public async Task Handle_WhenTheTeamCompletedASprintEarly_ReadsTheRecordedCompletion()
    {
        // Arrange — sprint 1 completed on the Friday morning before sprint 2's planned Monday
        using var scenario = new SprintLifecycleScenario(
            sprint1Started: InChicago(Sprint1Start, 10),
            sprint1Completed: InChicago(Sprint2Start.PlusDays(-3), 11));
        var handler = new GetIterationStatesQueryHandler(
            scenario.DbContext, scenario.Dispatcher.Object, scenario.SchedulingSettings.Object, scenario.DateTimeProvider);

        // Act — the scenario's clock is that Friday afternoon
        var result = await handler.Handle(
            new GetIterationStatesQuery([scenario.Sprint1.Id, scenario.Sprint2.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result[scenario.Sprint1.Id].State.Should().Be(IterationState.Completed);
        result[scenario.Sprint1.Id].ActiveUntil.Should().Be(InChicago(Sprint2Start.PlusDays(-3), 11));
        result[scenario.Sprint2.Id].State.Should().Be(IterationState.Future);
    }

    [Fact]
    public async Task Handle_ForIterationsOutsideATeamTimeline_ReadsTheirPlannedDays()
    {
        // Arrange — the scenario's clock is Friday Sep 25: a release iteration around it, and a sprint with
        // a start but no end
        var release = new IterationFaker().AsIteration().WithTeamId(null)
            .WithDateRange(new IterationDateRange(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 30))).Generate();
        var openEnded = new IterationFaker().AsSprint().WithTeam(_scenario.Team)
            .WithDateRange(new IterationDateRange(new LocalDate(2026, 10, 12), null)).Generate();
        _scenario.DbContext.AddIterations([release, openEnded]);

        // Act
        var result = await _handler.Handle(new GetIterationStatesQuery([release.Id, openEnded.Id]), TestContext.Current.CancellationToken);

        // Assert
        result[release.Id].Should().Be(new IterationStateDto(IterationState.Active, null, null, null));
        result[openEnded.Id].Should().Be(new IterationStateDto(IterationState.Future, null, null, null));
    }

    [Fact]
    public async Task Handle_WithNoIds_ReturnsNothing()
    {
        // Act
        var result = await _handler.Handle(new GetIterationStatesQuery([]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
