using FluentAssertions;
using Mapster;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Dtos;

/// <summary>
/// The CanManagePortfolio hint drives whether the UI offers status and edit controls. It must agree with
/// ProjectPortfolio.CanManagePortfolio — a hint that says false where the aggregate says true hides
/// controls the server would accept, and the reverse shows controls that fail on submit.
/// </summary>
public class ProjectPortfolioDetailsDtoTests
{
    private readonly ProjectPortfolioFaker _portfolioFaker = new();

    /// <summary>
    /// Runs the DTO's real projection. Against a database EF loads RoleAssignment.Employee; in memory the
    /// faker leaves it null and the role-to-employee maps NRE before the hint is evaluated, so the
    /// navigation is populated here to keep the mapping the same shape a real query would see.
    /// </summary>
    private static ProjectPortfolioDetailsDto Map(ProjectPortfolio portfolio, Guid? employeeId, bool isPpmAdministrator)
    {
        foreach (var role in portfolio.Roles)
        {
            role.Employee ??= new EmployeeFaker().Generate();
        }

        return portfolio.Adapt<ProjectPortfolioDetailsDto>(
            ProjectPortfolioDetailsDto.CreateTypeAdapterConfig(employeeId, isPpmAdministrator));
    }

    [Fact]
    public void CanManagePortfolio_ShouldBeTrue_ForPortfolioOwner()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [employeeId] })
            .Generate();

        // Act
        var dto = Map(portfolio, employeeId, isPpmAdministrator: false);

        // Assert
        dto.CanManagePortfolio.Should().BeTrue();
    }

    [Fact]
    public void CanManagePortfolio_ShouldBeTrue_ForPortfolioManager()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Manager] = [employeeId] })
            .Generate();

        // Act
        var dto = Map(portfolio, employeeId, isPpmAdministrator: false);

        // Assert
        dto.CanManagePortfolio.Should().BeTrue();
    }

    [Fact]
    public void CanManagePortfolio_ShouldBeFalse_ForSponsor()
    {
        // Arrange — sponsors are excluded from delivery management.
        var employeeId = Guid.NewGuid();
        var portfolio = _portfolioFaker
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Sponsor] = [employeeId] })
            .Generate();

        // Act
        var dto = Map(portfolio, employeeId, isPpmAdministrator: false);

        // Assert
        dto.CanManagePortfolio.Should().BeFalse();
    }

    [Fact]
    public void CanManagePortfolio_ShouldBeFalse_ForNonMember()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithRoles(null).Generate();

        // Act
        var dto = Map(portfolio, Guid.NewGuid(), isPpmAdministrator: false);

        // Assert
        dto.CanManagePortfolio.Should().BeFalse();
    }

    [Fact]
    public void CanManagePortfolio_ShouldBeTrue_ForPpmAdministratorWithNoMembership()
    {
        // Arrange
        var portfolio = _portfolioFaker.WithRoles(null).Generate();

        // Act
        var dto = Map(portfolio, Guid.NewGuid(), isPpmAdministrator: true);

        // Assert
        dto.CanManagePortfolio.Should().BeTrue();
    }

    [Fact]
    public void CanManagePortfolio_ShouldBeFalse_WhenUnauthenticated()
    {
        // Arrange
        var portfolio = _portfolioFaker
            .WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [Guid.NewGuid()] })
            .Generate();

        // Act
        var dto = Map(portfolio, employeeId: null, isPpmAdministrator: false);

        // Assert
        dto.CanManagePortfolio.Should().BeFalse();
    }
}
