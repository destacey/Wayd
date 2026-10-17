using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler filters on the work type's tier through navigations, which
/// the fakes do not translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamCadenceMetricsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // Monday 31 August 2026; ForecastSeeder.Now is Tuesday 22 September, inside the third two-week period.
    private static readonly LocalDate Anchor = new(2026, 8, 31);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static Mock<IDispatcher> DispatcherWith(params TeamSchedulePeriodDto[] schedule)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);
        return dispatcher;
    }

    [Fact]
    public async Task Handle_ReportsCompletedRequirementWorkPerTwoWeekPeriodFromTheAnchor()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);

        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Instant.FromUtc(2026, 9, 1, 15, 0), storyPoints: 3);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Instant.FromUtc(2026, 9, 13, 23, 0), storyPoints: 5);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Instant.FromUtc(2026, 9, 14, 9, 0), storyPoints: 2);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Instant.FromUtc(2026, 9, 21, 9, 0));
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Instant.FromUtc(2026, 9, 2, 9, 0), epic: true);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Removed, done: Instant.FromUtc(2026, 9, 2, 9, 0));

        var schedule = new TeamSchedulePeriodDto(Anchor, null, "UTC", 1, SizingMethod.StoryPoints)
        {
            Cadence = ReportingCadence.Create(2, Anchor).Value,
        };
        var settings = Mock.Of<ISettings<SprintMetricsSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new SprintMetricsSettings()));

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamCadenceMetricsQueryHandler(context, DispatcherWith(schedule).Object, settings,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var result = await handler.Handle(new GetTeamCadenceMetricsQuery(teamId), cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.CadenceWeeks.Should().Be(2);
        result.Periods.Select(p => (p.Start, p.IsCurrent, p.CompletedCount, p.CompletedEstimate)).Should().Equal(
            (new LocalDate(2026, 9, 14), true, 2, 2.0),
            (Anchor, false, 2, 8.0));
        result.Periods[0].UnestimatedCount.Should().Be(1);
        result.ByEstimate!.Average.Should().Be(8);
        result.ByEstimate.Periods.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamWithNoCadence_HasNoPeriods()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        var settings = Mock.Of<ISettings<SprintMetricsSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new SprintMetricsSettings()));

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamCadenceMetricsQueryHandler(context,
            DispatcherWith(new TeamSchedulePeriodDto(Anchor, null, "UTC", 1, SizingMethod.Count)).Object, settings,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var result = await handler.Handle(new GetTeamCadenceMetricsQuery(teamId), cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.CadenceWeeks.Should().BeNull();
        result.Periods.Should().BeEmpty();
        result.ByCount.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var settings = Mock.Of<ISettings<SprintMetricsSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new SprintMetricsSettings()));

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamCadenceMetricsQueryHandler(context, DispatcherWith().Object, settings,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var result = await handler.Handle(new GetTeamCadenceMetricsQuery(Guid.NewGuid()), cancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
