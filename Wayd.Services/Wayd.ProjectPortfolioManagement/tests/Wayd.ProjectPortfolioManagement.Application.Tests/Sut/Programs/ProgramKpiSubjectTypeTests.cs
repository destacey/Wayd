using FluentAssertions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs;

public sealed class ProgramKpiSubjectTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.Programs.View";
    private const string UpdatePermission = "Permissions.Programs.Update";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public ProgramKpiSubjectTypeTests()
    {
        Grant(ViewPermission, true);
        Grant(UpdatePermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    public void Dispose() => _dbContext.Dispose();

    private ProgramKpiSubjectType CreateSut() => new(_dbContext, _currentPrincipal.Object, _currentUser.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal
            .Setup(p => p.HoldsPermission(permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    /// <summary>A program in a portfolio, with the portfolio's navigation loaded.</summary>
    private Program SeedProgram(
        Dictionary<ProgramRole, HashSet<Guid>>? roles = null,
        Dictionary<ProjectPortfolioRole, HashSet<Guid>>? portfolioRoles = null)
    {
        var portfolio = new ProjectPortfolioFaker().WithRoles(portfolioRoles).Generate();
        var program = new ProgramFaker().WithPortfolio(portfolio).WithRoles(roles).Generate();
        _dbContext.AddProgram(program);
        return program;
    }

    private Task<KpiSubjectAccess?> Resolve(Program program) =>
        CreateSut().Resolve(program.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void SubjectType_ShouldBeTheProgramDescriptorsKey()
    {
        // Act
        var subjectType = CreateSut().SubjectType;

        // Assert
        subjectType.Should().Be("ppm.program");
        subjectType.Should().Be(PpmKpiSubjects.Program.Key);
        PpmKpiSubjects.Program.ActivityAggregateType.Should().Be("Program");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewPrograms()
    {
        // Arrange
        var program = SeedProgram(roles: new() { [ProgramRole.Owner] = [_callerId] });
        Grant(ViewPermission, false);

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

    [Fact]
    public async Task Resolve_ShouldNameTheProgram_AndLetItsManagerManage()
    {
        // Arrange
        var program = SeedProgram(roles: new() { [ProgramRole.Manager] = [_callerId] });

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new KpiSubjectAccess(program.Name, program.Key.ToString(), CanManage: true));
    }

    [Fact]
    public async Task Resolve_ShouldLetThePortfoliosManagerManage()
    {
        // Arrange
        var program = SeedProgram(portfolioRoles: new() { [ProjectPortfolioRole.Manager] = [_callerId] });

        // Act
        var access = await Resolve(program);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldNotLetASponsorManage()
    {
        // Arrange
        var program = SeedProgram(roles: new() { [ProgramRole.Sponsor] = [_callerId] });

        // Act
        var access = await Resolve(program);

        // Assert
        access!.CanManage.Should().BeFalse();
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
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldNotLetALeaderManage_WithoutTheUpdatePermission()
    {
        // Arrange
        var program = SeedProgram(roles: new() { [ProgramRole.Owner] = [_callerId] });
        Grant(UpdatePermission, false);

        // Act
        var access = await Resolve(program);

        // Assert
        access.Should().Be(new KpiSubjectAccess(program.Name, program.Key.ToString(), CanManage: false));
    }

    [Fact]
    public async Task Describe_ShouldLinkTheProgramsThatExist()
    {
        // Arrange
        var program = SeedProgram();

        // Act
        var links = await CreateSut().Describe([program.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        links.Should().ContainSingle();
        links[program.Id].Should().Be(new ImportRecordLink(ImportRecordType.Program, program.Key.ToString(), program.Name));
    }
}
