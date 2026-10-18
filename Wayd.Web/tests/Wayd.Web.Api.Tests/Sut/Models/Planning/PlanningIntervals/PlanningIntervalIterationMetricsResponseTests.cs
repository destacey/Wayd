using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningSprints.Dtos;
using Wayd.Web.Api.Models.Planning.PlanningIntervals;
using Wayd.Work.Application.WorkItems.Dtos;
using Xunit;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.PlanningIntervals;

/// <summary>
/// A PI iteration rolls its sprints and Kanban teams up together, but only sprints have scope.
/// </summary>
public sealed class PlanningIntervalIterationMetricsResponseTests
{
    private static readonly Guid ScrumTeamId = Guid.NewGuid();
    private static readonly Guid KanbanTeamId = Guid.NewGuid();

    private static PlanningIntervalIterationSprintsDto Iteration(params Guid[] sprintTeamIds) =>
        new()
        {
            Id = Guid.NewGuid(),
            Key = 1,
            Name = "Iteration 1",
            Start = new LocalDate(2026, 9, 7),
            End = new LocalDate(2026, 9, 20),
            Category = new SimpleNavigationDto { Id = 1, Name = "Development" },
            Sprints = [.. sprintTeamIds.Select(teamId => new PlanningSprintListDto
            {
                Id = Guid.NewGuid(),
                Name = "Sprint",
                State = new SimpleNavigationDto { Id = 1, Name = "Completed" },
                Team = new PlanningTeamNavigationDto { Id = teamId, Key = 1, Name = "Team", Code = "T", Type = "Team" },
            })],
        };

    private static SprintMetricsSummary Sprint(SizingMethod sizingMethod = SizingMethod.StoryPoints) =>
        new()
        {
            SprintName = "Sprint",
            State = new SimpleNavigationDto { Id = 1, Name = "Completed" },
            Team = new NavigationDto { Id = ScrumTeamId, Key = 1, Name = "Scrum Team" },
            SizingMethod = sizingMethod,
            TotalWorkItems = 10,
            TotalEstimate = 30,
            CompletedWorkItems = 6,
            CompletedEstimate = 18,
            InProgressWorkItems = 3,
            InProgressEstimate = 9,
            NotStartedWorkItems = 1,
            NotStartedEstimate = 3,
            UnestimatedWorkItems = 1,
            CycleTime = new CycleTimeSummary { WorkItemsCount = 6, TotalCycleTimeDays = 12 },
        };

    private static DateMeasuredTeamMetricsSummary KanbanTeam(SizingMethod sizingMethod = SizingMethod.StoryPoints) =>
        new()
        {
            Team = new NavigationDto { Id = KanbanTeamId, Key = 2, Name = "Kanban Team" },
            TimeZone = "UTC",
            SizingMethod = sizingMethod,
            CompletedWorkItems = 4,
            CompletedEstimate = 10,
            InProgressWorkItems = 2,
            InProgressEstimate = 5,
            UnestimatedWorkItems = 2,
            CycleTime = new CycleTimeSummary { WorkItemsCount = 4, TotalCycleTimeDays = 20 },
        };

    [Fact]
    public void TeamsWithoutSprint_LeavesOutTeamsWithAMappedSprint()
    {
        // Arrange
        var iteration = Iteration(ScrumTeamId);

        // Act
        var result = PlanningIntervalIterationMetricsResponse.TeamsWithoutSprint([ScrumTeamId, KanbanTeamId, KanbanTeamId], iteration);

        // Assert
        result.Should().Equal(KanbanTeamId);
    }

    [Fact]
    public void Create_AddsKanbanTeamsToCompletedAndInProgress_ButNotToScope()
    {
        // Act
        var result = PlanningIntervalIterationMetricsResponse.Create(Iteration(ScrumTeamId), [Sprint()], [KanbanTeam()]);

        // Assert
        result.TeamCount.Should().Be(2);
        result.SprintCount.Should().Be(1);
        result.TotalWorkItems.Should().Be(10);
        result.TotalEstimate.Should().Be(30);
        result.NotStartedWorkItems.Should().Be(1);
        result.NotStartedEstimate.Should().Be(3);
        result.CompletedWorkItems.Should().Be(10);
        result.CompletedEstimate.Should().Be(28);
        result.InProgressWorkItems.Should().Be(5);
        result.InProgressEstimate.Should().Be(14);
        result.UnestimatedWorkItems.Should().Be(3);
        result.CycleTime.WorkItemsCount.Should().Be(10);
        result.CycleTime.TotalCycleTimeDays.Should().Be(32);
        result.DateMeasuredTeamMetrics.Should().ContainSingle();
    }

    [Fact]
    public void Create_KanbanTeamInAnotherUnit_NullsOnlyTheCombinedEstimates()
    {
        // Act
        var result = PlanningIntervalIterationMetricsResponse.Create(Iteration(ScrumTeamId), [Sprint(SizingMethod.Effort)], [KanbanTeam(SizingMethod.Count)]);

        // Assert
        result.SizingMethod.Should().BeNull();
        result.CompletedEstimate.Should().BeNull();
        result.InProgressEstimate.Should().BeNull();
        result.SprintSizingMethod.Should().Be(SizingMethod.Effort);
        result.TotalEstimate.Should().Be(30);
        result.NotStartedEstimate.Should().Be(3);
        result.CompletedWorkItems.Should().Be(10);
    }

    [Fact]
    public void Create_WithOnlyKanbanTeams_HasNoSprintUnitOrScope()
    {
        // Act
        var result = PlanningIntervalIterationMetricsResponse.Create(Iteration(), [], [KanbanTeam(SizingMethod.Effort)]);

        // Assert
        result.SizingMethod.Should().Be(SizingMethod.Effort);
        result.SprintSizingMethod.Should().BeNull();
        result.TotalEstimate.Should().BeNull();
        result.TotalWorkItems.Should().Be(0);
        result.CompletedEstimate.Should().Be(10);
        result.TeamCount.Should().Be(1);
    }
}
