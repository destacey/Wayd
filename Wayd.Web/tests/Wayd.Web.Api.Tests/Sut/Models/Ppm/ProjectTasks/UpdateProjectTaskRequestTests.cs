using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.ProjectTasks;

/// <summary>
/// The project task update request as the task patch endpoint builds it from the current task.
/// </summary>
public sealed class UpdateProjectTaskRequestTests
{
    private static ProjectTaskDto FullyPopulatedTask() => new()
    {
        Id = Guid.NewGuid(),
        Key = "APOLLO-7",
        ProjectId = Guid.NewGuid(),
        Name = "Draft the launch plan",
        Description = "Covers the go-live checklist.",
        Type = SimpleNavigationDto.Create(1, "Task"),
        Status = SimpleNavigationDto.Create(2, "In Progress"),
        Priority = SimpleNavigationDto.Create(3, "High"),
        Assignees =
        [
            new EmployeeNavigationDto { Id = Guid.NewGuid(), Key = 11, Name = "Avery Lane" },
            new EmployeeNavigationDto { Id = Guid.NewGuid(), Key = 12, Name = "Jordan Reyes" },
        ],
        Progress = 40m,
        Order = 3,
        ParentId = Guid.NewGuid(),
        ProjectStageId = Guid.NewGuid(),
        Parent = new ProjectTaskNavigationDto { Id = Guid.NewGuid(), Key = "APOLLO-2", Name = "Plan" },
        PlannedStart = new LocalDate(2026, 3, 2),
        PlannedEnd = new LocalDate(2026, 3, 20),
        PlannedDate = new LocalDate(2026, 3, 21),
        EstimatedEffortHours = 16m,
    };

    [Fact]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedTask()
    {
        // Arrange
        var task = FullyPopulatedTask();

        // Act
        var request = UpdateProjectTaskRequest.FromDto(task);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Fact]
    public void FromDto_MapsTheFieldsWhoseNamesDiffer()
    {
        // Arrange
        var task = FullyPopulatedTask();

        // Act
        var request = UpdateProjectTaskRequest.FromDto(task);

        // Assert
        request.Id.Should().Be(task.Id);
        request.StatusId.Should().Be(2);
        request.PriorityId.Should().Be(3);
        request.AssigneeIds.Should().Equal(task.Assignees.Select(a => a.Id));
        request.ParentId.Should().Be(task.ParentId!.Value);
        request.Progress.Should().Be(40m);
        request.PlannedStart.Should().Be(task.PlannedStart);
        request.PlannedEnd.Should().Be(task.PlannedEnd);
        request.PlannedDate.Should().Be(task.PlannedDate);
        request.EstimatedEffortHours.Should().Be(16m);
    }

    [Fact]
    public void FromDto_UsesTheStageAsParentForATopLevelTask()
    {
        // Arrange
        var task = FullyPopulatedTask() with { ParentId = null, Parent = null };

        // Act
        var request = UpdateProjectTaskRequest.FromDto(task);

        // Assert
        request.ParentId.Should().Be(task.ProjectStageId);
    }
}
