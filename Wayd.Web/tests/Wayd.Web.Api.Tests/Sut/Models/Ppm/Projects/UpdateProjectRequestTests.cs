using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Models.Ppm.Projects;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.Projects;

/// <summary>
/// The project update request as the project patch endpoint builds it from the current project.
/// </summary>
public sealed class UpdateProjectRequestTests
{
    private static EmployeeNavigationDto Employee(int key, string name) => new() { Id = Guid.NewGuid(), Key = key, Name = name };

    private static ProjectDetailsDto FullyPopulatedProject() => new()
    {
        Id = Guid.NewGuid(),
        Key = "APOLLO",
        Name = "Apollo",
        Description = "Moves billing to the new platform.",
        BusinessCase = "The old platform reaches end of support next year.",
        ExpectedBenefits = "Lower hosting cost.",
        Status = LifecycleNavigationDto.FromEnum(ProjectStatus.Active),
        ExpenditureCategory = SimpleNavigationDto.Create(4, "Capital"),
        Start = new LocalDate(2026, 1, 5),
        End = new LocalDate(2026, 9, 30),
        Portfolio = NavigationDto.Create(Guid.NewGuid(), 1, "Finance"),
        Program = NavigationDto.Create(Guid.NewGuid(), 2, "Modernization"),
        ProjectSponsors = [Employee(1, "Avery Lane")],
        ProjectOwners = [Employee(2, "Jordan Reyes")],
        ProjectManagers = [Employee(3, "Casey Moore"), Employee(4, "Riley Park")],
        ProjectMembers = [Employee(5, "Morgan Ellis")],
        StrategicThemes = [NavigationDto.Create(Guid.NewGuid(), 7, "Operational excellence")],
        StrategicInitiatives = [],
    };

    [Fact]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedProject()
    {
        // Arrange
        var project = FullyPopulatedProject();

        // Act
        var request = UpdateProjectRequest.FromDto(project);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Fact]
    public void FromDto_MapsTheFieldsWhoseNamesDiffer()
    {
        // Arrange
        var project = FullyPopulatedProject();

        // Act
        var request = UpdateProjectRequest.FromDto(project);

        // Assert
        request.Id.Should().Be(project.Id);
        request.ExpenditureCategoryId.Should().Be(4);
        request.Start.Should().Be(project.Start);
        request.End.Should().Be(project.End);
        request.SponsorIds.Should().Equal(project.ProjectSponsors.Select(e => e.Id));
        request.OwnerIds.Should().Equal(project.ProjectOwners.Select(e => e.Id));
        request.ManagerIds.Should().Equal(project.ProjectManagers.Select(e => e.Id));
        request.MemberIds.Should().Equal(project.ProjectMembers.Select(e => e.Id));
        request.StrategicThemeIds.Should().Equal(project.StrategicThemes.Select(t => t.Id));
    }
}
