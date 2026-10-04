using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetSprintsWorkItemMetricsQueryTests : IDisposable
{
    private static readonly LocalDate SwitchedToEffort = new(2026, 7, 1);

    private readonly Guid _teamId = Guid.NewGuid();
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly GetSprintsWorkItemMetricsQueryHandler _handler;

    public GetSprintsWorkItemMetricsQueryTests()
    {
        // The team sized in story points, then in effort from SwitchedToEffort.
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>
            {
                [_teamId] =
                [
                    new(new LocalDate(2026, 1, 1), SwitchedToEffort.PlusDays(-1), "UTC", 1, SizingMethod.StoryPoints),
                    new(SwitchedToEffort, null, "UTC", 1, SizingMethod.Effort),
                ],
            });
        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        _handler = new GetSprintsWorkItemMetricsQueryHandler(
            _dbContext,
            _dispatcher.Object,
            schedulingSettings.Object,
            NullLogger<GetSprintsWorkItemMetricsQueryHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_TeamThatChangedSizingMethod_MeasuresEachSprintInTheMethodOnItsPlannedStart()
    {
        // Arrange
        var before = Sprint(_teamId, SwitchedToEffort.PlusDays(-14));
        var after = Sprint(_teamId, SwitchedToEffort);
        _dbContext.AddIterations([before, after]);
        _dbContext.AddWorkItems(
        [
            new WorkItemFaker().WithIterationId(before.Id).WithActiveState().WithStoryPoints(3).WithEffort(20).Generate(),
            new WorkItemFaker().WithIterationId(after.Id).WithActiveState().WithStoryPoints(5).WithEffort(40).Generate(),
        ]);

        // Act
        var result = await _handler.Handle(new GetSprintsWorkItemMetricsQuery([before.Id, after.Id]), TestContext.Current.CancellationToken);

        // Assert
        var beforeMetrics = result.Single(m => m.SprintId == before.Id);
        beforeMetrics.SizingMethod.Should().Be(SizingMethod.StoryPoints);
        beforeMetrics.TotalEstimate.Should().Be(3);

        var afterMetrics = result.Single(m => m.SprintId == after.Id);
        afterMetrics.SizingMethod.Should().Be(SizingMethod.Effort);
        afterMetrics.TotalEstimate.Should().Be(40);
    }

    [Fact]
    public async Task Handle_SprintWithNoTeam_IsMeasuredByCount()
    {
        // Arrange
        var sprint = Sprint(null, SwitchedToEffort);
        _dbContext.AddIterations([sprint]);
        _dbContext.AddWorkItems(
        [
            new WorkItemFaker().WithIterationId(sprint.Id).WithActiveState().WithStoryPoints(8).Generate(),
            new WorkItemFaker().WithIterationId(sprint.Id).WithActiveState().Generate(),
        ]);

        // Act
        var result = await _handler.Handle(new GetSprintsWorkItemMetricsQuery([sprint.Id]), TestContext.Current.CancellationToken);

        // Assert
        var metrics = result.Should().ContainSingle().Subject;
        metrics.SizingMethod.Should().Be(SizingMethod.Count);
        metrics.TotalEstimate.Should().Be(2);
        metrics.UnestimatedWorkItems.Should().Be(0);
    }

    private static Iteration Sprint(Guid? teamId, LocalDate start) =>
        new IterationFaker()
            .AsSprint()
            .WithTeamId(teamId)
            .WithDateRange(new IterationDateRange(start, start.PlusDays(13)))
            .Generate();
}
