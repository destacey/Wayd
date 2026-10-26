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
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;
using Wayd.Web.Api.Controllers.Ppm;
using Wayd.Web.Api.Dtos.Ppm;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Ppm;

public sealed class ProjectHealthCheckEvidenceComposerTests
{
    private static readonly LocalDate Today = new(2026, 8, 15);
    private static readonly LocalDate ProjectEnd = new(2026, 12, 31);

    private readonly AutoMocker _mocker = new();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectHealthCheckEvidenceComposerTests()
    {
        _mocker.GetMock<IDateTimeProvider>().Setup(c => c.Today).Returns(Today);

        SetupProject(withLifecycle: false);
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectPlanSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectPlanSummaryDto { Overdue = 2, TotalLeafTasks = 10 });
        SetupTasks();
        SetupStages();
        SetupHealthChecks();
        SetupActivity();
    }

    private Mock<IDispatcher> Dispatcher => _mocker.GetMock<IDispatcher>();

    private ProjectHealthCheckEvidenceComposer CreateComposer() => _mocker.CreateInstance<ProjectHealthCheckEvidenceComposer>();

    private void SetupForecasting(bool enabled) =>
        _mocker.GetMock<IFeatureFlags>()
            .Setup(f => f.IsEnabled(FeatureFlags.Names.DeliveryForecasting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabled);

    private void SetupProject(bool withLifecycle) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailsDto
            {
                Id = _projectId,
                Key = "APOLLO",
                Name = "Apollo",
                Description = "A test project.",
                Status = new LifecycleNavigationDto { Id = 2, Name = "Active", LifecycleCategory = "Active" },
                ExpenditureCategory = new SimpleNavigationDto { Id = 1, Name = "Opex" },
                Start = new LocalDate(2026, 1, 1),
                End = ProjectEnd,
                Portfolio = new NavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Portfolio" },
                ProjectSponsors = [],
                ProjectOwners = [],
                ProjectManagers = [],
                ProjectMembers = [],
                StrategicThemes = [],
                StrategicInitiatives = [],
                ProjectLifecycle = withLifecycle
                    ? new DescriptiveNavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Waterfall", Description = "Stages" }
                    : null,
            });

    private void SetupTasks(params ProjectTaskListDto[] tasks) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

    private void SetupStages(params ProjectStageListDto[] stages) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectStagesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. stages]);

    private void SetupHealthChecks(params Instant[] reportedOnNewestFirst) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectHealthChecksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. reportedOnNewestFirst.Select(r => new ProjectHealthCheckDetailsDto
            {
                Id = Guid.NewGuid(),
                ProjectId = _projectId,
                Status = new SimpleNavigationDto { Id = 1, Name = "Healthy" },
                ReportedBy = new NavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Jordan Example" },
                ReportedOn = r,
                Expiration = r.Plus(Duration.FromDays(14)),
            })]);

    private void SetupActivity(params ActivityLogDto[] newestFirst) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PagedResponse<ActivityLogDto>?>(new PagedResponse<ActivityLogDto>([.. newestFirst], newestFirst.Length, 1, 100)));

    private static ProjectTaskListDto TaskItem(string key, TaskStatus status, LocalDate? plannedEnd) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Name = key,
        Type = new SimpleNavigationDto { Id = 1, Name = "Task" },
        Status = new SimpleNavigationDto { Id = (int)status, Name = status.ToString() },
        Priority = new SimpleNavigationDto { Id = 2, Name = "Medium" },
        Assignees = [],
        PlannedEnd = plannedEnd,
    };

    private static ProjectStageListDto Stage(string name, TaskStatus status, LocalDate? end, int order) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Status = new SimpleNavigationDto { Id = (int)status, Name = status.ToString() },
        Order = order,
        End = end,
    };

    private static ActivityLogDto Entry(Instant timestamp) => new()
    {
        Id = timestamp.ToUnixTimeTicks(),
        EventType = "ProjectDetailsUpdatedEvent",
        DomainArea = "ProjectPortfolioManagement",
        AggregateType = "Project",
        Timestamp = timestamp,
        Payload = "{}",
    };

    private Task<ProjectHealthCheckEvidenceDto?> Compose() =>
        CreateComposer().Compose("APOLLO", ForecastOptions.Default, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Compose_ShouldReturnNull_WhenProjectDoesNotExist()
    {
        // Arrange
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDetailsDto?)null);

        // Act
        var evidence = await Compose();

        // Assert
        evidence.Should().BeNull();
        Dispatcher.Verify(d => d.Send(It.IsAny<GetProjectTasksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        Dispatcher.Verify(d => d.Send(It.IsAny<GetProjectActivitiesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compose_ShouldListOnlyOpenTasksPastTheirPlannedEnd_EarliestFirst()
    {
        // Arrange
        SetupForecasting(false);
        SetupTasks(
            TaskItem("APOLLO-1", TaskStatus.InProgress, Today.PlusDays(-1)),
            TaskItem("APOLLO-2", TaskStatus.NotStarted, Today.PlusDays(-10)),
            TaskItem("APOLLO-3", TaskStatus.Completed, Today.PlusDays(-5)),
            TaskItem("APOLLO-4", TaskStatus.Canceled, Today.PlusDays(-5)),
            TaskItem("APOLLO-5", TaskStatus.NotStarted, Today),
            TaskItem("APOLLO-6", TaskStatus.InProgress, Today.PlusDays(3)),
            TaskItem("APOLLO-7", TaskStatus.NotStarted, null));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.AsOf.Should().Be(Today);
        evidence.OverdueTasks.Select(t => t.Key).Should().Equal("APOLLO-2", "APOLLO-1");
    }

    [Fact]
    public async Task Compose_ShouldReadNoStages_WhenProjectHasNoLifecycle()
    {
        // Arrange
        SetupForecasting(false);

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.Stages.Should().BeEmpty();
        Dispatcher.Verify(d => d.Send(It.IsAny<GetProjectStagesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compose_ShouldFlagStagesPastTheirEndThatAreNotFinished()
    {
        // Arrange
        SetupForecasting(false);
        SetupProject(withLifecycle: true);
        SetupStages(
            Stage("Initiate", TaskStatus.Completed, Today.PlusDays(-30), 1),
            Stage("Plan", TaskStatus.Canceled, Today.PlusDays(-20), 2),
            Stage("Design", TaskStatus.InProgress, Today.PlusDays(-1), 3),
            Stage("Build", TaskStatus.NotStarted, Today, 4),
            Stage("Close", TaskStatus.NotStarted, null, 5));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.Stages.Where(s => s.PastEndNotComplete).Select(s => s.Name).Should().Equal("Design");
        evidence.Stages.Should().HaveCount(5);
    }

    [Fact]
    public async Task Compose_ShouldStopActivityAtTheLatestCheck()
    {
        // Arrange
        SetupForecasting(false);
        var latestCheck = Instant.FromUtc(2026, 8, 1, 9, 0);
        SetupHealthChecks(latestCheck, Instant.FromUtc(2026, 7, 15, 9, 0));
        SetupActivity(
            Entry(Instant.FromUtc(2026, 8, 10, 9, 0)),
            Entry(Instant.FromUtc(2026, 8, 5, 9, 0)),
            Entry(latestCheck),
            Entry(Instant.FromUtc(2026, 7, 20, 9, 0)));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.LatestHealthCheck!.ReportedOn.Should().Be(latestCheck);
        evidence.ActivitySinceLatestCheck.Select(a => a.Timestamp)
            .Should().Equal(Instant.FromUtc(2026, 8, 10, 9, 0), Instant.FromUtc(2026, 8, 5, 9, 0));
        evidence.ActivityTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Compose_ShouldFlagActivityTruncated_WhenThePageLimitIsReachedBeforeTheLatestCheck()
    {
        // Arrange
        SetupForecasting(false);
        SetupHealthChecks(Instant.FromUtc(2026, 1, 1, 9, 0));
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetProjectActivitiesQuery q, CancellationToken _) => Result.Success<PagedResponse<ActivityLogDto>?>(
                new PagedResponse<ActivityLogDto>([Entry(Instant.FromUtc(2026, 8, 10, 9, 0).Minus(Duration.FromHours(q.Paging.Page)))], 10_000, q.Paging.Page, 100)));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.ActivityTruncated.Should().BeTrue();
        evidence.ActivitySinceLatestCheck.Should().HaveCount(5);
        Dispatcher.Verify(d => d.Send(It.IsAny<GetProjectActivitiesQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task Compose_ShouldReadOnePageOfActivity_WhenProjectWasNeverChecked()
    {
        // Arrange
        SetupForecasting(false);
        SetupActivity(Entry(Instant.FromUtc(2026, 8, 10, 9, 0)));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.LatestHealthCheck.Should().BeNull();
        evidence.ActivitySinceLatestCheck.Should().HaveCount(1);
        evidence.ActivityTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Compose_ShouldReturnNoActivity_WhenTheActivityReadFails()
    {
        // Arrange
        SetupForecasting(false);
        Dispatcher.Setup(d => d.Send(It.IsAny<GetProjectActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagedResponse<ActivityLogDto>?>("Project not found."));

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.ActivitySinceLatestCheck.Should().BeEmpty();
        evidence.ActivityTruncated.Should().BeFalse();
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
        Dispatcher.Verify(d => d.Send(It.IsAny<GetProjectForecastQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compose_ShouldForecastAgainstTheProjectEnd_WhenDeliveryForecastingIsOn()
    {
        // Arrange
        SetupForecasting(true);

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.ForecastUnavailableReason.Should().BeNull();
        Dispatcher.Verify(d => d.Send(
            It.Is<GetProjectForecastQuery>(q => q.ProjectId == _projectId && q.TargetDate == ProjectEnd),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Compose_ShouldAlwaysReportCriticalPathUnavailable()
    {
        // Arrange
        SetupForecasting(false);

        // Act
        var evidence = await Compose();

        // Assert
        evidence!.CriticalPathUnavailableReason.Should().Be(ProjectHealthCheckEvidenceComposer.CriticalPathUnavailable);
    }
}
