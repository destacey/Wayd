using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Controllers.Planning;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Planning;

public sealed class ObjectiveHealthCheckEvidenceComposerTests
{
    private static readonly LocalDate PiStart = new(2026, 7, 1);
    private static readonly LocalDate PiEnd = new(2026, 9, 30);
    private static readonly Instant Now = Instant.FromUtc(2026, 8, 15, 12, 0);

    private readonly AutoMocker _mocker = new();
    private readonly Guid _piId = Guid.NewGuid();
    private readonly Guid _objectiveId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();

    public ObjectiveHealthCheckEvidenceComposerTests()
    {
        var clock = _mocker.GetMock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        clock.Setup(c => c.Today).Returns(new LocalDate(2026, 8, 15));

        SetupPlanningInterval();
        SetupObjective(startDate: null, targetDate: null, progress: 40);
        Dispatcher.Setup(d => d.Send(It.IsAny<GetExternalObjectWorkItemMetricsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Dispatcher.Setup(d => d.Send(It.IsAny<GetRisksByPlanningIntervalQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<RiskListDto>());
        SetupHealthChecks();
        SetupActivity();
    }

    private Mock<IDispatcher> Dispatcher => _mocker.GetMock<IDispatcher>();

    private ObjectiveHealthCheckEvidenceComposer CreateComposer() => _mocker.CreateInstance<ObjectiveHealthCheckEvidenceComposer>();

    private void SetupForecasting(bool enabled) =>
        _mocker.GetMock<IFeatureFlags>()
            .Setup(f => f.IsEnabled(FeatureFlags.Names.DeliveryForecasting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabled);

    private void SetupPlanningInterval() =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetPlanningIntervalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanningIntervalDetailsDto
            {
                Id = _piId,
                Key = 3,
                Name = "PI 3",
                Start = PiStart,
                End = PiEnd,
                State = new SimpleNavigationDto { Id = 2, Name = "Active" },
            });

    private void SetupObjective(LocalDate? startDate, LocalDate? targetDate, double progress) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetPlanningIntervalObjectiveQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanningIntervalObjectiveDetailsDto
            {
                Id = _objectiveId,
                Key = 12,
                Name = "Ship checkout",
                Status = new SimpleNavigationDto { Id = 2, Name = "In Progress" },
                Progress = progress,
                PlanningInterval = new NavigationDto { Id = _piId, Key = 3, Name = "PI 3" },
                Team = new PlanningTeamNavigationDto { Id = _teamId, Key = 1, Name = "Team Atlas", Code = "ATL", Type = "Team" },
                Type = new SimpleNavigationDto { Id = 1, Name = "Team" },
                StartDate = startDate,
                TargetDate = targetDate,
            });

    private void SetupHealthChecks(params Instant[] reportedOn) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetPlanningIntervalObjectiveHealthChecksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. reportedOn.Select(r => new PlanningIntervalObjectiveHealthCheckDetailsDto
            {
                Id = Guid.NewGuid(),
                PlanningIntervalObjectiveId = _objectiveId,
                Status = new SimpleNavigationDto { Id = 1, Name = "Healthy" },
                ReportedBy = new NavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Jordan Example" },
                ReportedOn = r,
                Expiration = r.Plus(Duration.FromDays(14)),
            })]);

    private void SetupActivity(params ActivityLogDto[] newestFirst) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetPlanningIntervalObjectiveActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PagedResponse<ActivityLogDto>?>(new PagedResponse<ActivityLogDto>([.. newestFirst], newestFirst.Length, 1, 50)));

    private static ActivityLogDto Entry(Instant timestamp, string eventType, string payload = "{}") => new()
    {
        Id = timestamp.ToUnixTimeTicks(),
        EventType = eventType,
        DomainArea = "Planning",
        AggregateType = "PlanningIntervalObjective",
        Timestamp = timestamp,
        Payload = payload,
    };

    private Task<Web.Api.Dtos.Planning.ObjectiveHealthCheckEvidenceDto?> Compose(bool includeClosedRisks = false) =>
        CreateComposer().Compose("3", "12", ForecastOptions.Default, includeClosedRisks, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Compose_ShouldReturnNull_WhenObjectiveIsNotInThePlanningInterval()
    {
        // Arrange
        Dispatcher.Setup(d => d.Send(It.IsAny<GetPlanningIntervalObjectiveQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanningIntervalObjectiveDetailsDto?)null);

        // Act
        var evidence = await Compose();

        // Assert
        evidence.Should().BeNull();
        Dispatcher.Verify(d => d.Send(It.IsAny<GetExternalObjectWorkItemsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compose_ShouldReportForecastUnavailable_WhenDeliveryForecastingIsOff()
    {
        // Arrange
        SetupForecasting(false);

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.Forecast.Should().BeNull();
        evidence.ForecastUnavailableReason.Should().Contain("turned off");
        Dispatcher.Verify(d => d.Send(It.IsAny<GetExternalObjectForecastQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compose_ShouldForecastAgainstThePlanningIntervalEnd_WhenObjectiveHasNoTargetDate()
    {
        // Arrange
        SetupForecasting(true);

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.ForecastUnavailableReason.Should().BeNull();
        evidence.Schedule.TargetDate.Should().Be(PiEnd);
        evidence.Schedule.TargetDateDefaulted.Should().BeTrue();
        evidence.Schedule.StartDate.Should().Be(PiStart);
        evidence.Schedule.StartDateDefaulted.Should().BeTrue();
        Dispatcher.Verify(d => d.Send(
            It.Is<GetExternalObjectForecastQuery>(q => q.ObjectId == _objectiveId && q.TargetDate == PiEnd),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Compose_ShouldUseTheObjectivesOwnDates_WhenItHasThem()
    {
        // Arrange
        SetupForecasting(true);
        SetupObjective(new LocalDate(2026, 8, 1), new LocalDate(2026, 8, 31), progress: 40);

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.Schedule.StartDateDefaulted.Should().BeFalse();
        evidence.Schedule.TargetDateDefaulted.Should().BeFalse();
        evidence.Schedule.ElapsedShare.Should().Be(Math.Round(14d / 30, 4));
        Dispatcher.Verify(d => d.Send(
            It.Is<GetExternalObjectForecastQuery>(q => q.TargetDate == new LocalDate(2026, 8, 31)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Compose_ShouldReadMetricsFromPlanningIntervalStartToToday()
    {
        // Arrange
        SetupForecasting(false);

        // Act
        await Compose();

        // Assert
        Dispatcher.Verify(d => d.Send(
            It.Is<GetExternalObjectWorkItemMetricsQuery>(q =>
                q.ObjectiveId == _objectiveId && q.Start == new DateOnly(2026, 7, 1) && q.End == new DateOnly(2026, 8, 15)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Compose_ShouldAskForTheTeamsRisks()
    {
        // Arrange
        SetupForecasting(false);

        // Act
        await Compose(includeClosedRisks: true);

        // Assert
        Dispatcher.Verify(d => d.Send(
            It.Is<GetRisksByPlanningIntervalQuery>(q => q.TeamId == _teamId && q.IncludeClosed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Compose_ShouldDeriveProgressAtLastCheck_FromTheFirstProgressChangeAfterIt()
    {
        // Arrange
        SetupForecasting(false);
        var lastCheck = Instant.FromUtc(2026, 8, 1, 9, 0);
        SetupHealthChecks(lastCheck, Instant.FromUtc(2026, 7, 15, 9, 0));
        SetupActivity(
            Entry(Instant.FromUtc(2026, 8, 10, 9, 0), "PlanningIntervalObjectiveProgressChangedEvent", """{"previousProgress":30,"progress":40}"""),
            Entry(Instant.FromUtc(2026, 8, 5, 9, 0), "PlanningIntervalObjectiveProgressChangedEvent", """{"previousProgress":25,"progress":30}"""),
            Entry(lastCheck, "PlanningIntervalObjectiveHealthCheckCreatedEvent"),
            Entry(Instant.FromUtc(2026, 7, 20, 9, 0), "PlanningIntervalObjectiveProgressChangedEvent", """{"previousProgress":10,"progress":25}"""));

        // Act
        var evidence = await Compose();

        // Assert
        var since = evidence!.SinceLastCheck;
        since.LastCheckReportedOn.Should().Be(lastCheck);
        since.Activity.Should().HaveCount(2);
        since.ActivityTruncated.Should().BeFalse();
        since.ProgressAtLastCheck.Should().Be(25);
        since.ProgressChange.Should().Be(15);
    }

    [Fact]
    public async Task Compose_ShouldReportNoProgressChange_WhenNothingWasRecordedSinceTheLastCheck()
    {
        // Arrange
        SetupForecasting(false);
        var lastCheck = Instant.FromUtc(2026, 8, 1, 9, 0);
        SetupHealthChecks(lastCheck);
        SetupActivity(Entry(lastCheck, "PlanningIntervalObjectiveHealthCheckCreatedEvent"));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.SinceLastCheck.Activity.Should().BeEmpty();
        evidence.SinceLastCheck.ProgressAtLastCheck.Should().Be(40);
        evidence.SinceLastCheck.ProgressChange.Should().Be(0);
    }

    [Fact]
    public async Task Compose_ShouldLeaveProgressChangeUnknown_WhenNeverChecked()
    {
        // Arrange
        SetupForecasting(false);
        SetupActivity(Entry(Instant.FromUtc(2026, 8, 10, 9, 0), "PlanningIntervalObjectiveProgressChangedEvent", """{"previousProgress":30,"progress":40}"""));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.SinceLastCheck.LastCheckReportedOn.Should().BeNull();
        evidence.SinceLastCheck.ProgressChange.Should().BeNull();
        evidence.SinceLastCheck.Activity.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(2026, 7, 31, 0)]
    [InlineData(2026, 8, 1, 0)]
    [InlineData(2026, 8, 11, 0.5)]
    [InlineData(2026, 8, 21, 1)]
    [InlineData(2026, 9, 30, 1)]
    public void ElapsedShare_ShouldMeasureTodayAgainstTheWindow(int year, int month, int day, double expected)
    {
        // Arrange
        var start = new LocalDate(2026, 8, 1);
        var target = new LocalDate(2026, 8, 21);

        // Act
        var share = ObjectiveHealthCheckEvidenceComposer.ElapsedShare(start, target, new LocalDate(year, month, day));

        // Assert
        share.Should().Be(expected);
    }
}
