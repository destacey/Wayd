using FluentAssertions;
using Mapster;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Dtos;

/// <summary>
/// The CanManageProgram hint drives whether the UI offers status and edit controls. It must agree with
/// Program.CanManageProgram, including the rule that leadership inherits down from the parent portfolio.
/// </summary>
public class ProgramDetailsDtoTests
{
    private readonly ProgramFaker _programFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    /// <summary>
    /// Builds a program with its Portfolio navigation populated, since the hint reads portfolio roles.
    /// </summary>
    private Program ProgramWithPortfolio(ProgramRole? programRole = null, Guid? employeeId = null, ProjectPortfolioRole? portfolioRole = null)
    {
        var portfolioFaker = _portfolioFaker;
        if (portfolioRole.HasValue && employeeId.HasValue)
        {
            portfolioFaker = portfolioFaker.WithRoles(
                new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [portfolioRole.Value] = [employeeId.Value] });
        }
        else
        {
            portfolioFaker = portfolioFaker.WithRoles(null);
        }

        var portfolio = portfolioFaker.Generate();

        var faker = _programFaker.WithPortfolioId(portfolio.Id);
        faker = programRole.HasValue && employeeId.HasValue
            ? faker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>> { [programRole.Value] = [employeeId.Value] })
            : faker.WithRoles(null);

        var program = faker.Generate();
        program.SetPrivate(p => p.Portfolio, portfolio);

        return program;
    }

    /// <summary>
    /// Runs the DTO's real projection. Against a database EF loads RoleAssignment.Employee; in memory the
    /// faker leaves it null and the role-to-employee maps NRE before the hint is evaluated, so the
    /// navigation is populated here to keep the mapping the same shape a real query would see.
    /// </summary>
    private static ProgramDetailsDto Map(Program program, Guid? employeeId, bool isPpmAdministrator)
    {
        foreach (var role in program.Roles)
        {
            role.Employee ??= new EmployeeFaker().Generate();
        }

        foreach (var role in program.Portfolio?.Roles ?? [])
        {
            role.Employee ??= new EmployeeFaker().Generate();
        }

        return program.Adapt<ProgramDetailsDto>(
            ProgramDetailsDto.CreateTypeAdapterConfig(employeeId, isPpmAdministrator));
    }

    [Fact]
    public void CanManageProgram_ShouldBeTrue_ForProgramOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = ProgramWithPortfolio(ProgramRole.Owner, employeeId);

        // Act
        var dto = Map(program, employeeId, isPpmAdministrator: false);

        // Assert
        dto.CanManageProgram.Should().BeTrue();
    }

    [Fact]
    public void CanManageProgram_ShouldBeTrue_ForPortfolioOwner()
    {
        // Arrange — leadership inherits downward from the parent portfolio.
        var employeeId = Guid.NewGuid();
        var program = ProgramWithPortfolio(employeeId: employeeId, portfolioRole: ProjectPortfolioRole.Owner);

        // Act
        var dto = Map(program, employeeId, isPpmAdministrator: false);

        // Assert
        dto.CanManageProgram.Should().BeTrue();
    }

    [Fact]
    public void CanManageProgram_ShouldBeFalse_ForProgramSponsor()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var program = ProgramWithPortfolio(ProgramRole.Sponsor, employeeId);

        // Act
        var dto = Map(program, employeeId, isPpmAdministrator: false);

        // Assert
        dto.CanManageProgram.Should().BeFalse();
    }

    [Fact]
    public void CanManageProgram_ShouldBeFalse_ForNonMember()
    {
        // Arrange
        var program = ProgramWithPortfolio();

        // Act
        var dto = Map(program, Guid.NewGuid(), isPpmAdministrator: false);

        // Assert
        dto.CanManageProgram.Should().BeFalse();
    }

    [Fact]
    public void CanManageProgram_ShouldBeTrue_ForPpmAdministratorWithNoMembership()
    {
        // Arrange
        var program = ProgramWithPortfolio();

        // Act
        var dto = Map(program, Guid.NewGuid(), isPpmAdministrator: true);

        // Assert
        dto.CanManageProgram.Should().BeTrue();
    }

    [Fact]
    public void CanManageProgram_ShouldBeFalse_WhenUnauthenticated()
    {
        // Arrange
        var program = ProgramWithPortfolio(ProgramRole.Owner, Guid.NewGuid());

        // Act
        var dto = Map(program, employeeId: null, isPpmAdministrator: false);

        // Assert
        dto.CanManageProgram.Should().BeFalse();
    }
}
