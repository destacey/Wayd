using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Queries;

public class GetSprintsQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 12, 0);

    private readonly FakeWorkDbContext _dbContext;
    private readonly GetSprintsQueryHandler _handler;
    private readonly IterationFaker _iterationFaker;

    public GetSprintsQueryHandlerTests()
    {
        _dbContext = new FakeWorkDbContext();

        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>());
        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        _handler = new GetSprintsQueryHandler(_dbContext, dispatcher.Object, schedulingSettings.Object, new TestingDateTimeProvider(new FakeClock(Now)));
        _iterationFaker = new IterationFaker();
    }

    [Fact]
    public async Task Handle_ReturnsOnlySprints_WhenMixedIterationTypesExist()
    {
        // Arrange
        var sprint = _iterationFaker.AsSprint().Generate();
        var nonSprint = _iterationFaker.AsIteration().Generate();
        _dbContext.AddIterations([sprint, nonSprint]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(s => s.Id == sprint.Id);
        result.Should().NotContain(s => s.Id == nonSprint.Id);
    }

    [Fact]
    public async Task Handle_ExcludesSprints_WhenTeamIdIsNull()
    {
        // Arrange
        var sprintWithTeam = _iterationFaker.AsSprint().Generate(); // TeamId is random Guid by default
        var sprintWithoutTeam = _iterationFaker.AsSprint().WithTeamId(null).Generate();
        _dbContext.AddIterations([sprintWithTeam, sprintWithoutTeam]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(s => s.Id == sprintWithTeam.Id);
        result.Should().NotContain(s => s.Id == sprintWithoutTeam.Id);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTeamSprints_WhenTeamIdFilterProvided()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var teamSprint = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();
        var otherSprint = _iterationFaker.AsSprint().WithTeamId(Guid.NewGuid()).Generate();
        _dbContext.AddIterations([teamSprint, otherSprint]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(s => s.Id == teamSprint.Id);
        result.Should().NotContain(s => s.Id == otherSprint.Id);
    }

    [Fact]
    public async Task Handle_WorksOutEachSprintsStateNow()
    {
        // Arrange — on Oct 1, one sprint has ended, one is running and one is still to come
        var teamId = Guid.NewGuid();
        var ended = NewSprint(teamId, 1, new LocalDate(2026, 9, 14));
        var running = NewSprint(teamId, 2, new LocalDate(2026, 9, 28));
        var next = NewSprint(teamId, 3, new LocalDate(2026, 10, 12));
        _dbContext.AddIterations([ended, running, next]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Single(s => s.Id == ended.Id).State.Id.Should().Be((int)IterationState.Completed);
        result.Single(s => s.Id == running.Id).State.Id.Should().Be((int)IterationState.Active);
        result.Single(s => s.Id == next.Id).State.Id.Should().Be((int)IterationState.Future);
    }

    [Fact]
    public async Task Handle_ForASprintWithNoPlannedEnd_ReadsItsStateFromItsStart()
    {
        // Arrange — outside the team's timeline, so its planned days decide
        var teamId = Guid.NewGuid();
        var openEnded = new IterationFaker().AsSprint().WithTeamId(teamId)
            .WithDateRange(new IterationDateRange(new LocalDate(2026, 9, 28), null))
            .Generate();
        _dbContext.AddIterations([openEnded]);

        // Act
        var result = await _handler.Handle(new GetSprintsQuery(teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.State.Id.Should().Be((int)IterationState.Active);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoSprintsExist()
    {
        // Act
        var result = await _handler.Handle(new GetSprintsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    private static Wayd.Work.Domain.Models.Iteration NewSprint(Guid teamId, int key, LocalDate start) =>
        new IterationFaker().AsSprint().WithKey(key).WithTeamId(teamId)
            .WithDateRange(new IterationDateRange(start, start.PlusDays(13)))
            .Generate();

    public void Dispose() => _dbContext.Dispose();
}
