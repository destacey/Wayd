using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>Runs against real SQL Server, where the teams' sprints are read with their planned dates.</summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsActiveSprintQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ReturnsTheSprintEachTeamIsRunningNow()
    {
        // Arrange — ForecastSeeder.Now is 22 September 2026
        await _seed.Reset(Ct);
        var running = await _seed.AddTeam(Ct);
        var between = await _seed.AddTeam(Ct);
        await _seed.AddSprint(running.Id, new LocalDate(2026, 9, 1), Ct);
        var current = await _seed.AddSprint(running.Id, new LocalDate(2026, 9, 15), Ct);
        await _seed.AddSprint(running.Id, new LocalDate(2026, 9, 29), Ct);
        await _seed.AddSprint(between.Id, new LocalDate(2026, 8, 1), Ct);
        await _seed.AddSprint(between.Id, new LocalDate(2026, 10, 1), Ct);

        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>());
        var schedulingSettings = Mock.Of<ISettings<SchedulingSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new SchedulingSettings()));
        var generalSettings = Mock.Of<ISettings<GeneralSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new GeneralSettings()));

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamsActiveSprintQueryHandler(accessor.Context, dispatcher.Object, schedulingSettings, generalSettings,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var result = await handler.Handle(new GetTeamsActiveSprintQuery([running.Id, between.Id]), Ct);

        // Assert
        result.Keys.Should().Equal(running.Id);
        result[running.Id].Id.Should().Be(current);
    }
}
