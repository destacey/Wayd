using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.Tests.Infrastructure;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class TimeAwayAuthorizationTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();

    private readonly Employee _director;
    private readonly Employee _manager;
    private readonly Employee _employee;
    private readonly Employee _peer;

    public TimeAwayAuthorizationTests()
    {
        _director = new EmployeeFaker().Generate();
        _manager = new EmployeeFaker().WithManagerId(_director.Id).Generate();
        _employee = new EmployeeFaker().WithManagerId(_manager.Id).Generate();
        _peer = new EmployeeFaker().WithManagerId(_manager.Id).Generate();

        _dbContext.AddEmployees([_director, _manager, _employee, _peer]);
    }

    [Fact]
    public async Task CanManageTimeAway_ForTheirOwn_IsTrue()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_employee.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _employee.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageTimeAway_ForSomeoneWhoReportsToThem_IsTrue()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_manager.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _employee.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageTimeAway_ForSomeoneFurtherDownTheirChain_IsTrue()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_director.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _employee.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageTimeAway_ForAPeer_IsFalse()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_peer.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _employee.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageTimeAway_ForTheirOwnManager_IsFalse()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_employee.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _manager.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageTimeAway_WithoutALinkedEmployee_IsFalse()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(null);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _employee.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageTimeAway_ForATimeAwayAdministrator_IsTrueForAnyone()
    {
        // Arrange — a peer, and the director nobody is above
        var principal = TestPrincipal.LinkedTo(_peer.Id, timeAwayAdministrator: true);

        // Act
        var forAPeer = await principal.CanManageTimeAway(_dbContext, _employee.Id, TestContext.Current.CancellationToken);
        var forTheDirector = await principal.CanManageTimeAway(_dbContext, _director.Id, TestContext.Current.CancellationToken);

        // Assert
        forAPeer.Should().BeTrue();
        forTheDirector.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageTimeAway_ForSomeoneWithNoManager_IsFalseForEveryoneElse()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_manager.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _director.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageTimeAway_ForSomeoneWhoManagesNobody_IsFalseWithoutWalkingAnyChain()
    {
        // Arrange — the employee reports to nobody the caller is above, and the caller has no reports
        var principal = TestPrincipal.LinkedTo(_employee.Id);

        // Act
        var canManage = await principal.CanManageTimeAway(_dbContext, _peer.Id, TestContext.Current.CancellationToken);

        // Assert
        canManage.Should().BeFalse();
    }

    [Fact]
    public async Task EmployeesWithManageableTimeAway_ForAManager_IsThemselvesAndThoseBelowThem()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_manager.Id);

        // Act
        var manageable = await principal.EmployeesWithManageableTimeAway(
            _dbContext, [_director.Id, _manager.Id, _employee.Id, _peer.Id], TestContext.Current.CancellationToken);

        // Assert
        manageable.Should().BeEquivalentTo([_manager.Id, _employee.Id, _peer.Id]);
    }

    [Fact]
    public async Task EmployeesWithManageableTimeAway_ForSomeoneWhoManagesNobody_IsOnlyThemselves()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(_employee.Id);

        // Act
        var manageable = await principal.EmployeesWithManageableTimeAway(
            _dbContext, [_manager.Id, _employee.Id, _peer.Id], TestContext.Current.CancellationToken);

        // Assert
        manageable.Should().BeEquivalentTo([_employee.Id]);
    }

    [Fact]
    public async Task EmployeesWithManageableTimeAway_ForAnAdministrator_IsEveryone()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(null, timeAwayAdministrator: true);

        // Act
        var manageable = await principal.EmployeesWithManageableTimeAway(
            _dbContext, [_director.Id, _peer.Id], TestContext.Current.CancellationToken);

        // Assert
        manageable.Should().BeEquivalentTo([_director.Id, _peer.Id]);
    }

    [Fact]
    public async Task EmployeesWithManageableTimeAway_ForACallerLinkedToNoEmployee_IsNobody()
    {
        // Arrange
        var principal = TestPrincipal.LinkedTo(null);

        // Act
        var manageable = await principal.EmployeesWithManageableTimeAway(
            _dbContext, [_employee.Id], TestContext.Current.CancellationToken);

        // Assert
        manageable.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
