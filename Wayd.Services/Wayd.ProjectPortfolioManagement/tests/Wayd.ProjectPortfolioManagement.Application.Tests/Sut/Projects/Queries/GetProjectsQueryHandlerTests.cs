using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class GetProjectsQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly Mock<ICurrentPrincipal> _currentPrincipalMock;
    private readonly GetProjectsQueryHandler _handler;
    private readonly ProjectPortfolio _portfolio = new ProjectPortfolioFaker().Generate();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _otherEmployeeId = Guid.NewGuid();

    public GetProjectsQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _currentPrincipalMock = new Mock<ICurrentPrincipal>();
        _currentPrincipalMock.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _currentPrincipalMock.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var clock = new FakeClock(new LocalDate(2026, 3, 18).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant());
        _handler = new GetProjectsQueryHandler(_dbContext, _currentPrincipalMock.Object, new TestingDateTimeProvider(clock));
    }

    /// <summary>
    /// Against a database EF loads Project.Portfolio and RoleAssignment.Employee; in memory the faker
    /// leaves both null and the list projection NREs on them, so they are populated here to keep the
    /// mapping the same shape a real query would see.
    /// </summary>
    private Project AddProject(string name, Dictionary<ProjectRole, HashSet<Guid>>? roles = null)
    {
        var project = new ProjectFaker()
            .WithName(name)
            .WithStatus(ProjectStatus.Active)
            .WithPortfolioId(_portfolio.Id)
            .WithRoles(roles)
            .Generate();

        typeof(Project).GetProperty(nameof(Project.Portfolio))!.SetValue(project, _portfolio);
        foreach (var role in project.Roles)
        {
            role.Employee ??= new EmployeeFaker().WithId(role.EmployeeId).Generate();
        }

        _dbContext.AddProject(project);
        return project;
    }

    [Fact]
    public async Task Handle_RoleFilterWithoutEmployeeId_ShouldUseThePrincipalsEmployee()
    {
        // Arrange
        var mine = AddProject("Mine", new() { [ProjectRole.Manager] = [_employeeId] });
        AddProject("Theirs", new() { [ProjectRole.Manager] = [_otherEmployeeId] });

        // Act
        var result = await _handler.Handle(
            new GetProjectsQuery(RoleFilter: [ProjectMemberRole.Manager]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task Handle_RoleFilterWithEmployeeId_ShouldUseThatEmployeeInsteadOfThePrincipal()
    {
        // Arrange
        AddProject("Mine", new() { [ProjectRole.Manager] = [_employeeId] });
        var theirs = AddProject("Theirs", new() { [ProjectRole.Owner] = [_otherEmployeeId] });

        // Act
        var result = await _handler.Handle(
            new GetProjectsQuery(RoleFilter: [ProjectMemberRole.Manager, ProjectMemberRole.Owner], EmployeeId: _otherEmployeeId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(theirs.Id);
    }

    [Fact]
    public async Task Handle_RoleFilterWithUnlinkedPrincipalAndNoEmployeeId_ShouldReturnEmpty()
    {
        // Arrange
        _currentPrincipalMock.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        AddProject("Theirs", new() { [ProjectRole.Manager] = [_otherEmployeeId] });

        // Act
        var result = await _handler.Handle(
            new GetProjectsQuery(RoleFilter: [ProjectMemberRole.Manager]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmployeeIdWithoutRoleFilter_ShouldListEverythingTheEmployeeIsInvolvedIn()
    {
        // Arrange: naming an employee asks for their projects in any role, not for everyone's
        AddProject("Mine", new() { [ProjectRole.Manager] = [_employeeId] });
        var owned = AddProject("Theirs, owned", new() { [ProjectRole.Owner] = [_otherEmployeeId] });
        var member = AddProject("Theirs, member", new() { [ProjectRole.Member] = [_otherEmployeeId] });

        // Act
        var result = await _handler.Handle(
            new GetProjectsQuery(EmployeeId: _otherEmployeeId),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Select(p => p.Id).Should().BeEquivalentTo([owned.Id, member.Id]);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
