using FluentAssertions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Portfolios;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios;

public sealed class PortfolioKpiSubjectTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.ProjectPortfolios.View";
    private const string UpdatePermission = "Permissions.ProjectPortfolios.Update";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public PortfolioKpiSubjectTypeTests()
    {
        Grant(ViewPermission, true);
        Grant(UpdatePermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    public void Dispose() => _dbContext.Dispose();

    private PortfolioKpiSubjectType CreateSut() => new(_dbContext, _currentPrincipal.Object, _currentUser.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal
            .Setup(p => p.HoldsPermission(permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private ProjectPortfolio SeedPortfolio(Dictionary<ProjectPortfolioRole, HashSet<Guid>>? roles = null)
    {
        var portfolio = new ProjectPortfolioFaker().WithRoles(roles).Generate();
        _dbContext.AddPortfolio(portfolio);
        return portfolio;
    }

    private Task<KpiSubjectAccess?> Resolve(ProjectPortfolio portfolio) =>
        CreateSut().Resolve(portfolio.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void SubjectType_ShouldBeThePortfolioDescriptorsKey()
    {
        // Act
        var subjectType = CreateSut().SubjectType;

        // Assert
        subjectType.Should().Be("ppm.portfolio");
        subjectType.Should().Be(PpmKpiSubjects.Portfolio.Key);
        // The portfolio's own activity is filed under its aggregate's name.
        PpmKpiSubjects.Portfolio.ActivityAggregateType.Should().Be("ProjectPortfolio");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewPortfolios()
    {
        // Arrange
        var portfolio = SeedPortfolio(new() { [ProjectPortfolioRole.Owner] = [_callerId] });
        Grant(ViewPermission, false);

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
    public async Task Resolve_ShouldLetOnlyDeliveryLeadersManage(ProjectPortfolioRole role, bool expected)
    {
        // Arrange
        var portfolio = SeedPortfolio(new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access.Should().Be(new KpiSubjectAccess(portfolio.Name, portfolio.Key.ToString(), expected));
    }

    [Fact]
    public async Task Resolve_ShouldLetThePpmAdministratorManage_WithoutARole()
    {
        // Arrange
        var portfolio = SeedPortfolio();
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldNotLetALeaderManage_WithoutTheUpdatePermission()
    {
        // Arrange
        var portfolio = SeedPortfolio(new() { [ProjectPortfolioRole.Owner] = [_callerId] });
        Grant(UpdatePermission, false);

        // Act
        var access = await Resolve(portfolio);

        // Assert
        access!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Describe_ShouldLinkThePortfoliosThatExist()
    {
        // Arrange
        var portfolio = SeedPortfolio();

        // Act
        var links = await CreateSut().Describe([portfolio.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        links.Should().ContainSingle();
        links[portfolio.Id].Should().Be(new ImportRecordLink(ImportRecordType.Portfolio, portfolio.Key.ToString(), portfolio.Name));
    }
}
