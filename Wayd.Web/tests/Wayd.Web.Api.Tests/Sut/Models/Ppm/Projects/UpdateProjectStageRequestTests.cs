using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.Web.Api.Models.Ppm.Projects;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.Projects;

/// <summary>
/// The project stage update request as the stage patch endpoint builds it from the current stage.
/// </summary>
public sealed class UpdateProjectStageRequestTests
{
    private static ProjectStageDetailsDto FullyPopulatedStage() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Execution",
        Description = "Build and test the release.",
        Status = SimpleNavigationDto.Create(2, "In Progress"),
        Order = 2,
        Start = new LocalDate(2026, 4, 1),
        End = new LocalDate(2026, 6, 30),
        Progress = 55m,
        Assignees =
        [
            new EmployeeNavigationDto { Id = Guid.NewGuid(), Key = 21, Name = "Morgan Hale" },
            new EmployeeNavigationDto { Id = Guid.NewGuid(), Key = 22, Name = "Riley Chen" },
        ],
    };

    [Fact]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedStage()
    {
        // Arrange
        var stage = FullyPopulatedStage();

        // Act
        var request = UpdateProjectStageRequest.FromDto(stage);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Fact]
    public void FromDto_MapsTheFieldsWhoseNamesDiffer()
    {
        // Arrange
        var stage = FullyPopulatedStage();

        // Act
        var request = UpdateProjectStageRequest.FromDto(stage);

        // Assert
        request.Description.Should().Be(stage.Description);
        request.Status.Should().Be(2);
        request.PlannedStart.Should().Be(stage.Start);
        request.PlannedEnd.Should().Be(stage.End);
        request.Progress.Should().Be(55m);
        request.AssigneeIds.Should().Equal(stage.Assignees.Select(a => a.Id));
    }
}
