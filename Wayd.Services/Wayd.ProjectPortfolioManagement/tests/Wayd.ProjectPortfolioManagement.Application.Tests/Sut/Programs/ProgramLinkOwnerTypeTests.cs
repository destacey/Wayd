using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs;

public sealed class ProgramLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.Programs.View";
    private const string UpdatePermission = "Permissions.Programs.Update";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public ProgramLinkOwnerTypeTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Grant(UpdatePermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
    }

    private ProgramLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object, _currentUser.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal.Setup(p => p.HasPermission(permission, It.IsAny<CancellationToken>())).ReturnsAsync(granted);

    /// <summary>
    /// A program in a portfolio. The Portfolio navigation is populated directly because the fake context ignores
    /// Include.
    /// </summary>
    private Program SeedProgram(
        Dictionary<ProgramRole, HashSet<Guid>>? roles = null,
        Dictionary<ProjectPortfolioRole, HashSet<Guid>>? portfolioRoles = null)
    {
        var portfolio = new ProjectPortfolioFaker().WithRoles(portfolioRoles).Generate();
        var program = new ProgramFaker().WithPortfolioId(portfolio.Id).WithRoles(roles).Generate();
        program.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProgram(program);
        return program;
    }

    private Task<LinkOwnerAccess?> Resolve(Program program) =>
        CreateSut().Resolve(program.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void OwnerType_ShouldBeTheProgramsFollowOwnerType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("ppm.program");
        PpmLinkOwners.Program.ActivityAggregateType.Should().Be("Program");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewPrograms()
    {
        // Arrange
        var program = SeedProgram(roles: new() { [ProgramRole.Owner] = [_callerId] });
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheProgramDoesNotExist()
    {
        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Theory]
    [InlineData(ProgramRole.Owner, true)]
    [InlineData(ProgramRole.Manager, true)]
    [InlineData(ProgramRole.Sponsor, false)]
    public async Task Resolve_ShouldLetOnlyTheProgramsLeadersManage(ProgramRole role, bool expected)
    {
        // Arrange
        var program = SeedProgram(roles: new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new LinkOwnerAccess(expected));
    }

    [Fact]
    public async Task Resolve_ShouldLetAPortfolioManagerManage()
    {
        // Arrange
        var program = SeedProgram(portfolioRoles: new() { [ProjectPortfolioRole.Manager] = [_callerId] });

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new LinkOwnerAccess(true));
    }

    [Fact]
    public async Task Resolve_ShouldLetThePpmAdministratorManage_WithoutARole()
    {
        // Arrange
        var program = SeedProgram();
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new LinkOwnerAccess(true));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseALeader_WithoutTheUpdatePermission()
    {
        // Arrange
        var program = SeedProgram(roles: new() { [ProgramRole.Owner] = [_callerId] });
        Grant(UpdatePermission, false);

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseALeader_WithNoLinkedEmployee()
    {
        // Arrange
        var program = SeedProgram();
        Grant(AdministerPermission, true);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    public void Dispose() => _dbContext.Dispose();
}
