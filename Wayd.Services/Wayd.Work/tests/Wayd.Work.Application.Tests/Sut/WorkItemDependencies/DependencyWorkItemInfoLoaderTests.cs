using FluentAssertions;
using Moq;
using NodaTime;
using Xunit;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItemDependencies;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.WorkItemDependencies;

public class DependencyWorkItemInfoLoaderTests : IDisposable
{
    // Sprint 1's last planned day is Sunday Sep 27; 20:00 that evening in Chicago is already Sep 28 in UTC.
    private static readonly LocalDate Sprint1LastDay = Sprint2Start.PlusDays(-1);

    private readonly SprintLifecycleScenario _scenario = new();

    public DependencyWorkItemInfoLoaderTests()
    {
        _scenario.Clock.Reset(InChicago(Sprint1LastDay, 20));
    }

    public void Dispose() => _scenario.Dispose();

    private WorkItem AddWorkItem(Iteration? iteration)
    {
        var workItem = new WorkItemFaker().WithStatusCategory(StatusCategory.Proposed).WithIterationId(iteration?.Id).Generate();
        workItem.Iteration = iteration;
        _scenario.DbContext.AddWorkItem(workItem);
        return workItem;
    }

    private Task<Dictionary<Guid, DependencyWorkItemInfo>> Load(params Guid[] ids) =>
        _scenario.DbContext.LoadDependencyWorkItemInfos(
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.GeneralSettings.Object,
            ids,
            _scenario.DateTimeProvider.Now,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Load_DuringTheSprintsLastDayInItsTeamsZone_KeepsThePlan()
    {
        // Arrange
        var workItem = AddWorkItem(_scenario.Sprint1);

        // Act
        var result = await Load(workItem.Id);

        // Assert
        result[workItem.Id].PlannedOn.Should().Be(Sprint1LastDay);
    }

    [Fact]
    public async Task Load_AfterTheSprintsLastDayInItsTeamsZone_DropsThePlan()
    {
        // Arrange
        var workItem = AddWorkItem(_scenario.Sprint1);
        _scenario.Clock.Reset(InChicago(Sprint2Start, 0));

        // Act
        var result = await Load(workItem.Id);

        // Assert
        result[workItem.Id].PlannedOn.Should().BeNull();
    }

    [Theory]
    [InlineData("UTC", false)]
    [InlineData("America/Chicago", true)]
    public async Task Load_ForASprintWithNoTeam_ReadsItsLastDayInTheSystemTimeZone(string systemTimeZone, bool stillPlanned)
    {
        // Arrange
        _scenario.GeneralSettings
            .Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { TimeZone = systemTimeZone });
        var sprint = new IterationFaker().AsSprint().WithTeamId(null)
            .WithDateRange(new IterationDateRange(Sprint1Start, Sprint1LastDay)).Generate();
        _scenario.DbContext.AddIterations([sprint]);
        var workItem = AddWorkItem(sprint);

        // Act
        var result = await Load(workItem.Id);

        // Assert
        result[workItem.Id].PlannedOn.Should().Be(stillPlanned ? Sprint1LastDay : null);
    }

    [Fact]
    public async Task Load_ForASprintWithNoPlannedStart_ReadsItsLastDayInTheSystemTimeZone()
    {
        // Arrange — the system zone is UTC, where the team's Sep 27 is already over
        var sprint = new IterationFaker().AsSprint().WithTeam(_scenario.Team)
            .WithDateRange(new IterationDateRange(null, Sprint1LastDay)).Generate();
        _scenario.DbContext.AddIterations([sprint]);
        var workItem = AddWorkItem(sprint);

        // Act
        var result = await Load(workItem.Id);

        // Assert
        result[workItem.Id].PlannedOn.Should().BeNull();
    }

    [Fact]
    public async Task Load_ForASprintWithNoPlannedEnd_HasNoPlan()
    {
        // Arrange
        var sprint = new IterationFaker().AsSprint().WithTeam(_scenario.Team)
            .WithDateRange(new IterationDateRange(Sprint2Start.PlusDays(14), null)).Generate();
        _scenario.DbContext.AddIterations([sprint]);
        var workItem = AddWorkItem(sprint);

        // Act
        var result = await Load(workItem.Id);

        // Assert
        result[workItem.Id].PlannedOn.Should().BeNull();
    }

    [Fact]
    public async Task Load_ForItemsSharingASprint_PlansEachOnItsLastDay()
    {
        // Arrange
        var first = AddWorkItem(_scenario.Sprint1);
        var second = AddWorkItem(_scenario.Sprint1);

        // Act
        var result = await Load(first.Id, second.Id);

        // Assert
        result[first.Id].PlannedOn.Should().Be(Sprint1LastDay);
        result[second.Id].PlannedOn.Should().Be(Sprint1LastDay);
    }

    [Fact]
    public async Task Load_ForAnItemInNoSprint_HasNoPlan_AndLeavesOutUnknownIds()
    {
        // Arrange
        var release = new IterationFaker().AsIteration().WithTeamId(null)
            .WithDateRange(new IterationDateRange(Sprint1Start, Sprint2Start.PlusDays(30))).Generate();
        var inRelease = AddWorkItem(release);
        var unscheduled = AddWorkItem(null);
        var unknown = Guid.NewGuid();

        // Act
        var result = await Load(inRelease.Id, unscheduled.Id, unknown);

        // Assert
        result.Should().HaveCount(2);
        result[inRelease.Id].PlannedOn.Should().BeNull();
        result[unscheduled.Id].PlannedOn.Should().BeNull();
        result[unscheduled.Id].StatusCategory.Should().Be(StatusCategory.Proposed);
    }
}
