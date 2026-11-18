using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.ProjectPortfolioManagement.Application.Portfolios;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios;

public sealed class PortfolioLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.ProjectPortfolios.View";
    private const string UpdatePermission = "Permissions.ProjectPortfolios.Update";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 17, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public PortfolioLinkOwnerTypeTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Grant(UpdatePermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
    }

    private PortfolioLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object, _currentUser.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal.Setup(p => p.HasPermission(permission, It.IsAny<CancellationToken>())).ReturnsAsync(granted);

    private ProjectPortfolio Seed(ProjectPortfolio portfolio)
    {
        _dbContext.AddPortfolio(portfolio);
        return portfolio;
    }

    private ProjectPortfolioFaker LedBy(ProjectPortfolioRole role) =>
        new ProjectPortfolioFaker().WithRoles(new() { [role] = [_callerId] });

    private Task<LinkOwnerAccess?> Resolve(ProjectPortfolio portfolio) =>
        CreateSut().Resolve(portfolio.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void OwnerType_ShouldBeFiledUnderThePortfoliosActivityType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("ppm.portfolio");
        PpmLinkOwners.Portfolio.ActivityAggregateType.Should().Be("ProjectPortfolio");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewPortfolios()
    {
        // Arrange
        var portfolio = Seed(LedBy(ProjectPortfolioRole.Owner).Generate());
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenThePortfolioDoesNotExist()
    {
        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Theory]
    [InlineData(ProjectPortfolioRole.Owner, true)]
    [InlineData(ProjectPortfolioRole.Manager, true)]
    [InlineData(ProjectPortfolioRole.Sponsor, false)]
    public async Task Resolve_ShouldLetOnlyThePortfoliosLeadersManage(ProjectPortfolioRole role, bool expected)
    {
        // Arrange
        var portfolio = Seed(LedBy(role).Generate());

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access.Should().Be(new LinkOwnerAccess(expected));
    }

    [Fact]
    public async Task Resolve_ShouldLetThePpmAdministratorManage_WithoutARole()
    {
        // Arrange
        var portfolio = Seed(new ProjectPortfolioFaker().Generate());
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access.Should().Be(new LinkOwnerAccess(true));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseALeader_WithoutTheUpdatePermission()
    {
        // Arrange
        var portfolio = Seed(LedBy(ProjectPortfolioRole.Owner).Generate());
        Grant(UpdatePermission, false);

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseEveryone_OnAnArchivedPortfolio()
    {
        // Arrange — an archived portfolio is read-only, as its own edit is
        var portfolio = Seed(LedBy(ProjectPortfolioRole.Owner).AsArchived(_dateTimeProvider));
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(portfolio);

        // Assert
        portfolio.IsReadOnly.Should().BeTrue();
        access.Should().Be(new LinkOwnerAccess(false));
    }

    public void Dispose() => _dbContext.Dispose();
}
