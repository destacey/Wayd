using FluentAssertions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Web.Api.Models.Planning.PlanningIntervals;
using Wayd.Work.Application.WorkItems.Dtos;
using Xunit;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.PlanningIntervals;

/// <summary>
/// A PI iteration's estimate totals are added up only across sprints measured in one sizing method.
/// </summary>
public sealed class SprintMetricsSummaryTests
{
    private static SprintMetricsSummary Sprint(SizingMethod sizingMethod) =>
        new()
        {
            SprintName = "Sprint",
            State = new SimpleNavigationDto { Id = 1, Name = "Active" },
            Team = new NavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Team" },
            SizingMethod = sizingMethod,
            CycleTime = CycleTimeSummary.Empty,
        };

    private static DateMeasuredTeamMetricsSummary KanbanTeam(SizingMethod sizingMethod) =>
        new()
        {
            Team = new NavigationDto { Id = Guid.NewGuid(), Key = 2, Name = "Kanban Team" },
            TimeZone = "UTC",
            SizingMethod = sizingMethod,
            CycleTime = CycleTimeSummary.Empty,
        };

    [Fact]
    public void CommonSizingMethod_WhenEverySprintShares_ReturnsIt()
    {
        // Arrange
        SprintMetricsSummary[] sprints = [Sprint(SizingMethod.Effort), Sprint(SizingMethod.Effort)];

        // Act
        var result = SprintMetricsSummary.CommonSizingMethod(sprints);

        // Assert
        result.Should().Be(SizingMethod.Effort);
    }

    [Fact]
    public void CommonSizingMethod_WhenSprintsDiffer_ReturnsNull()
    {
        // Arrange
        SprintMetricsSummary[] sprints = [Sprint(SizingMethod.StoryPoints), Sprint(SizingMethod.Count)];

        // Act
        var result = SprintMetricsSummary.CommonSizingMethod(sprints);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CommonSizingMethod_WhenAKanbanTeamDiffersFromTheSprints_ReturnsNull()
    {
        // Arrange
        SprintMetricsSummary[] sprints = [Sprint(SizingMethod.StoryPoints)];
        DateMeasuredTeamMetricsSummary[] teams = [KanbanTeam(SizingMethod.Count)];

        // Act
        var result = SprintMetricsSummary.CommonSizingMethod(sprints, teams);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CommonSizingMethod_WithOnlyKanbanTeams_ReturnsTheirs()
    {
        // Arrange
        DateMeasuredTeamMetricsSummary[] teams = [KanbanTeam(SizingMethod.Effort), KanbanTeam(SizingMethod.Effort)];

        // Act
        var result = SprintMetricsSummary.CommonSizingMethod([], teams);

        // Assert
        result.Should().Be(SizingMethod.Effort);
    }

    [Fact]
    public void CommonSizingMethod_WithNoSprints_ReturnsNull()
    {
        // Act
        var result = SprintMetricsSummary.CommonSizingMethod([]);

        // Assert
        result.Should().BeNull();
    }
}
