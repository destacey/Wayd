using FluentAssertions;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class RoleManagerTests
{
    private const int Owner = 1;
    private const int Manager = 2;

    private static readonly Guid Ada = Guid.Parse("00000000-0000-7000-8000-00000000000a");
    private static readonly Guid Ben = Guid.Parse("00000000-0000-7000-8000-00000000000b");
    private static readonly Guid Cy = Guid.Parse("00000000-0000-7000-8000-00000000000c");

    [Fact]
    public void Diff_WithAdditionsRemovalsAndUnchangedAssignments_ReportsOnlyWhatMoved()
    {
        // Arrange
        var before = new Dictionary<int, Guid[]> { [Owner] = [Ada, Ben] };
        var after = new Dictionary<int, Guid[]> { [Owner] = [Ben, Cy] };

        // Act
        var (added, removed) = RoleManager.Diff(before, after);

        // Assert — Ben kept Owner throughout, so he is in neither list
        added.Should().Equal(new RoleAssignmentChange(Owner, Cy));
        removed.Should().Equal(new RoleAssignmentChange(Owner, Ada));
    }

    [Fact]
    public void Diff_WhenAnEmployeeMovesBetweenRoles_ReportsItAsARemovalAndAnAddition()
    {
        // Arrange
        var before = new Dictionary<int, Guid[]> { [Manager] = [Ada] };
        var after = new Dictionary<int, Guid[]> { [Owner] = [Ada] };

        // Act
        var (added, removed) = RoleManager.Diff(before, after);

        // Assert — an assignment is a role held by an employee, so a move is one lost and one gained
        added.Should().Equal(new RoleAssignmentChange(Owner, Ada));
        removed.Should().Equal(new RoleAssignmentChange(Manager, Ada));
    }

    [Fact]
    public void Diff_WithTheSameAssignmentsInADifferentOrder_ReportsNothing()
    {
        // Arrange
        var before = new Dictionary<int, Guid[]> { [Owner] = [Ada, Ben], [Manager] = [Cy] };
        var after = new Dictionary<int, Guid[]> { [Manager] = [Cy], [Owner] = [Ben, Ada] };

        // Act
        var (added, removed) = RoleManager.Diff(before, after);

        // Assert
        added.Should().BeEmpty();
        removed.Should().BeEmpty();
    }

    [Fact]
    public void Diff_OrdersChangesByRoleThenEmployee()
    {
        // Arrange
        var before = new Dictionary<int, Guid[]>();
        var after = new Dictionary<int, Guid[]> { [Manager] = [Cy, Ada], [Owner] = [Ben] };

        // Act
        var (added, _) = RoleManager.Diff(before, after);

        // Assert — the same change must always produce the same payload
        added.Should().Equal(
            new RoleAssignmentChange(Owner, Ben),
            new RoleAssignmentChange(Manager, Ada),
            new RoleAssignmentChange(Manager, Cy));
    }
}
