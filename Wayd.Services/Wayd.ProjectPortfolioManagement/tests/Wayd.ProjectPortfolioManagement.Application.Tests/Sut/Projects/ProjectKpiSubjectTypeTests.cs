using FluentAssertions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects;

public sealed class ProjectKpiSubjectTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.Projects.View";
    private const string UpdatePermission = "Permissions.Projects.Update";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public ProjectKpiSubjectTypeTests()
    {
        Grant(ViewPermission, true);
        Grant(UpdatePermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    public void Dispose() => _dbContext.Dispose();

    private ProjectKpiSubjectType CreateSut() => new(_dbContext, _currentPrincipal.Object, _currentUser.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal
            .Setup(p => p.HoldsPermission(permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    /// <summary>A project in a portfolio, and a program when given, with the parents' navigations loaded.</summary>
    private Project SeedProject(
        Dictionary<ProjectRole, HashSet<Guid>>? roles = null,
        Dictionary<ProjectPortfolioRole, HashSet<Guid>>? portfolioRoles = null,
        Dictionary<ProgramRole, HashSet<Guid>>? programRoles = null)
    {
        var portfolio = new ProjectPortfolioFaker().WithRoles(portfolioRoles).Generate();
        var faker = new ProjectFaker().WithPortfolio(portfolio).WithRoles(roles);
        if (programRoles is not null)
        {
            var program = new ProgramFaker().WithPortfolio(portfolio).WithRoles(programRoles).Generate();
            faker = faker.WithProgram(program);
        }

        var project = faker.Generate();
        _dbContext.AddProject(project);
        return project;
    }

    private Task<KpiSubjectAccess?> Resolve(Project project) =>
        CreateSut().Resolve(project.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void SubjectType_ShouldBeTheProjectDescriptorsKey()
    {
        // Act
        var subjectType = CreateSut().SubjectType;

        // Assert
        // Persisted on every KPI.
        subjectType.Should().Be("ppm.project");
        subjectType.Should().Be(PpmKpiSubjects.Project.Key);
        PpmKpiSubjects.Project.ActivityAggregateType.Should().Be("Project");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewProjects()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        Grant(ViewPermission, false);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheProjectDoesNotExist()
    {
        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldNameTheProject_AndLetItsOwnerManage()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new KpiSubjectAccess(project.Name, project.Key.Value, CanManage: true));
    }

    [Theory]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Sponsor, false)]
    [InlineData(ProjectRole.Member, false)]
    public async Task Resolve_ShouldLetOnlyDeliveryLeadersOnTheProjectManage(ProjectRole role, bool expected)
    {
        // Arrange
        var project = SeedProject(roles: new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access!.CanManage.Should().Be(expected);
    }

    [Fact]
    public async Task Resolve_ShouldLetTheProgramsManagerManage()
    {
        // Arrange
        var project = SeedProject(programRoles: new() { [ProgramRole.Manager] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldLetThePortfoliosOwnerManage()
    {
        // Arrange
        var project = SeedProject(portfolioRoles: new() { [ProjectPortfolioRole.Owner] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldLetThePpmAdministratorManage_WithoutARole()
    {
        // Arrange
        var project = SeedProject();
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(project);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldNotLetALeaderManage_WithoutTheUpdatePermission()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        Grant(UpdatePermission, false);
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new KpiSubjectAccess(project.Name, project.Key.Value, CanManage: false));
    }

    [Fact]
    public async Task Resolve_ShouldNotLetACallerWithoutAnEmployeeManage()
    {
        // Arrange
        var project = SeedProject();
        Grant(AdministerPermission, true);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().NotBeNull();
        access!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_ShouldLetALeaderManage_WhenTheProjectIsCompleted()
    {
        // Arrange
        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = new ProjectFaker()
            .WithPortfolio(portfolio)
            .WithStatus(ProjectStatus.Completed)
            .WithRoles(new() { [ProjectRole.Owner] = [_callerId] })
            .Generate();
        _dbContext.AddProject(project);

        // Act
        var access = await Resolve(project);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Describe_ShouldLinkTheProjectsThatExist()
    {
        // Arrange
        var project = SeedProject();
        var missing = Guid.NewGuid();

        // Act
        var links = await CreateSut().Describe([project.Id, missing], TestContext.Current.CancellationToken);

        // Assert
        links.Should().ContainSingle();
        links[project.Id].Should().Be(new ImportRecordLink(ImportRecordType.Project, project.Key.Value, project.Name));
    }
}
