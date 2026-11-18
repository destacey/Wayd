using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects;

public sealed class ProjectLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.Projects.View";
    private const string UpdatePermission = "Permissions.Projects.Update";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public ProjectLinkOwnerTypeTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Grant(UpdatePermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
    }

    private ProjectLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object, _currentUser.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal.Setup(p => p.HasPermission(permission, It.IsAny<CancellationToken>())).ReturnsAsync(granted);

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
            var program = new ProgramFaker().WithPortfolioId(portfolio.Id).WithRoles(programRoles).Generate();
            faker = faker.WithProgram(program);
        }

        var project = faker.Generate();
        _dbContext.AddProject(project);
        return project;
    }

    private Task<LinkOwnerAccess?> Resolve(Project project) =>
        CreateSut().Resolve(project.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void OwnerType_ShouldBeTheProjectsFollowOwnerType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every link, and filed under the project's own activity type.
        ownerType.Should().Be("ppm.project");
        PpmLinkOwners.Project.ActivityAggregateType.Should().Be("Project");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewProjects()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

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

    [Theory]
    [InlineData(ProjectRole.Owner, true)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Sponsor, false)]
    [InlineData(ProjectRole.Member, false)]
    public async Task Resolve_ShouldLetOnlyTheProjectsLeadersManage(ProjectRole role, bool expected)
    {
        // Arrange
        var project = SeedProject(roles: new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new LinkOwnerAccess(expected));
    }

    [Fact]
    public async Task Resolve_ShouldLetAProgramManagerManage()
    {
        // Arrange
        var project = SeedProject(programRoles: new() { [ProgramRole.Manager] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new LinkOwnerAccess(true));
    }

    [Fact]
    public async Task Resolve_ShouldLetAPortfolioOwnerManage()
    {
        // Arrange
        var project = SeedProject(portfolioRoles: new() { [ProjectPortfolioRole.Owner] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new LinkOwnerAccess(true));
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
        access.Should().Be(new LinkOwnerAccess(true));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseALeader_WithoutTheUpdatePermission()
    {
        // Arrange — leadership never replaces the permission that edits a project
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        Grant(UpdatePermission, false);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseAnAdministrator_WithNoLinkedEmployee()
    {
        // Arrange — a project change is always attributed to an employee
        var project = SeedProject();
        Grant(AdministerPermission, true);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    [Fact]
    public async Task Resolve_ShouldRefuseALeader_OnAReadOnlyToken()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        _currentPrincipal.SetupGet(p => p.IsReadOnly).Returns(true);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    public void Dispose() => _dbContext.Dispose();
}
