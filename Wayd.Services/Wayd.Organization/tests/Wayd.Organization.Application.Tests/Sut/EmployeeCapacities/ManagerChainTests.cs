using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class ManagerChainTests
{
    private static readonly Guid Employee = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Director = Guid.NewGuid();
    private static readonly Guid Peer = Guid.NewGuid();

    // Employee and Peer report to Manager, who reports to Director, who reports to nobody.
    private static readonly Dictionary<Guid, Guid?> Reporting = new()
    {
        [Employee] = Manager,
        [Peer] = Manager,
        [Manager] = Director,
        [Director] = null,
    };

    [Fact]
    public async Task IsAbove_ForTheDirectManager_IsTrue()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Manager, Employee, Lookup(managerOf));

        // Assert
        isAbove.Should().BeTrue();
    }

    [Fact]
    public async Task IsAbove_ForAManagerFurtherUp_IsTrue()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Director, Employee, Lookup(managerOf));

        // Assert
        isAbove.Should().BeTrue();
    }

    [Fact]
    public async Task IsAbove_ForAPeer_IsFalse()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Peer, Employee, Lookup(managerOf));

        // Assert
        isAbove.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_ForSomeoneBelow_IsFalse()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Employee, Manager, Lookup(managerOf));

        // Assert
        isAbove.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_ForThemselves_IsFalse()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Employee, Employee, Lookup(managerOf));

        // Assert
        isAbove.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_ForSomeoneWithNoManager_IsFalse()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Manager, Director, Lookup(managerOf));

        // Assert
        isAbove.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_ForSomeoneNotInTheReporting_IsFalse()
    {
        // Arrange
        var managerOf = Reporting;

        // Act
        var isAbove = await ManagerChain.IsAbove(Manager, Guid.NewGuid(), Lookup(managerOf));

        // Assert
        isAbove.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_WhenTheChainLoops_EndsAndGrantsOnlyThoseInTheLoop()
    {
        // Arrange — Employee reports to Manager, who reports to Director, who reports back to Manager
        Dictionary<Guid, Guid?> managerOf = new()
        {
            [Employee] = Manager,
            [Manager] = Director,
            [Director] = Manager,
        };

        // Act
        var director = await ManagerChain.IsAbove(Director, Employee, Lookup(managerOf));
        var outsider = await ManagerChain.IsAbove(Peer, Employee, Lookup(managerOf));

        // Assert
        director.Should().BeTrue();
        outsider.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_WhenSomeoneManagesThemselves_IsFalseForThem()
    {
        // Arrange
        Dictionary<Guid, Guid?> managerOf = new() { [Employee] = Employee };

        // Act
        var isAbove = await ManagerChain.IsAbove(Employee, Employee, Lookup(managerOf));

        // Assert
        isAbove.Should().BeFalse();
    }

    [Fact]
    public async Task IsAbove_WhenTwoPeopleManageEachOther_IsTrueBothWays()
    {
        // Arrange
        Dictionary<Guid, Guid?> managerOf = new() { [Employee] = Manager, [Manager] = Employee };

        // Act
        var managerOverEmployee = await ManagerChain.IsAbove(Manager, Employee, Lookup(managerOf));
        var employeeOverManager = await ManagerChain.IsAbove(Employee, Manager, Lookup(managerOf));

        // Assert
        managerOverEmployee.Should().BeTrue();
        employeeOverManager.Should().BeTrue();
    }

    private static Func<Guid, Task<Guid?>> Lookup(Dictionary<Guid, Guid?> managerOf) =>
        id => Task.FromResult(managerOf.GetValueOrDefault(id));
}
