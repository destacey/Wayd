using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Models.Ppm.Programs;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.Programs;

/// <summary>
/// The program update request as the program patch endpoint builds it from the current program.
/// </summary>
public sealed class UpdateProgramRequestTests
{
    private static EmployeeNavigationDto Employee(int key, string name) => new() { Id = Guid.NewGuid(), Key = key, Name = name };

    private static ProgramDetailsDto FullyPopulatedProgram() => new()
    {
        Id = Guid.NewGuid(),
        Key = 12,
        Name = "Modernization",
        Description = "Retires the legacy platforms.",
        Status = LifecycleNavigationDto.FromEnum(ProgramStatus.Active),
        Start = new LocalDate(2026, 1, 5),
        End = new LocalDate(2027, 3, 31),
        Portfolio = NavigationDto.Create(Guid.NewGuid(), 1, "Finance"),
        ProgramSponsors = [Employee(1, "Avery Lane")],
        ProgramOwners = [Employee(2, "Jordan Reyes")],
        ProgramManagers = [Employee(3, "Casey Moore"), Employee(4, "Riley Park")],
        StrategicThemes = [NavigationDto.Create(Guid.NewGuid(), 7, "Operational excellence")],
    };

    [Fact]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedProgram()
    {
        // Arrange
        var program = FullyPopulatedProgram();

        // Act
        var request = UpdateProgramRequest.FromDto(program);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Fact]
    public void FromDto_MapsTheFieldsWhoseNamesDiffer()
    {
        // Arrange
        var program = FullyPopulatedProgram();

        // Act
        var request = UpdateProgramRequest.FromDto(program);

        // Assert
        request.Id.Should().Be(program.Id);
        request.Start.Should().Be(program.Start);
        request.End.Should().Be(program.End);
        request.SponsorIds.Should().Equal(program.ProgramSponsors.Select(e => e.Id));
        request.OwnerIds.Should().Equal(program.ProgramOwners.Select(e => e.Id));
        request.ManagerIds.Should().Equal(program.ProgramManagers.Select(e => e.Id));
        request.StrategicThemeIds.Should().Equal(program.StrategicThemes.Select(t => t.Id));
    }
}
