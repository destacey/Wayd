using FluentAssertions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Models.Ppm.Portfolios;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.Portfolios;

/// <summary>
/// The portfolio update request as the portfolio patch endpoint builds it from the current portfolio.
/// </summary>
public sealed class UpdatePortfolioRequestTests
{
    private static EmployeeNavigationDto Employee(int key, string name) => new() { Id = Guid.NewGuid(), Key = key, Name = name };

    private static ProjectPortfolioDetailsDto FullyPopulatedPortfolio() => new()
    {
        Id = Guid.NewGuid(),
        Key = 3,
        Name = "Finance",
        Description = "Every finance-led initiative.",
        Status = LifecycleNavigationDto.FromEnum(ProjectPortfolioStatus.Active),
        PortfolioSponsors = [Employee(1, "Avery Lane")],
        PortfolioOwners = [Employee(2, "Jordan Reyes")],
        PortfolioManagers = [Employee(3, "Casey Moore"), Employee(4, "Riley Park")],
    };

    [Fact]
    public void FromDto_SetsEveryPropertyFromAFullyPopulatedPortfolio()
    {
        // Arrange
        var portfolio = FullyPopulatedPortfolio();

        // Act
        var request = UpdatePortfolioRequest.FromDto(portfolio);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
    }

    [Fact]
    public void FromDto_MapsTheRoleHoldersToEmployeeIds()
    {
        // Arrange
        var portfolio = FullyPopulatedPortfolio();

        // Act
        var request = UpdatePortfolioRequest.FromDto(portfolio);

        // Assert
        request.Id.Should().Be(portfolio.Id);
        request.SponsorIds.Should().Equal(portfolio.PortfolioSponsors.Select(e => e.Id));
        request.OwnerIds.Should().Equal(portfolio.PortfolioOwners.Select(e => e.Id));
        request.ManagerIds.Should().Equal(portfolio.PortfolioManagers.Select(e => e.Id));
    }
}
