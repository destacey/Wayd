using NodaTime;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class TeamHierarchyPeriodsTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid OtherGroup = Guid.NewGuid();
    private static readonly Guid Team = Guid.NewGuid();

    private static readonly LocalDate From = new(2027, 2, 1);
    private static readonly LocalDate To = new(2027, 2, 28);

    [Fact]
    public void Descendants_ClipsAMembershipToTheRange()
    {
        // Arrange — one membership running past both ends of the range, one inside it
        var other = Guid.NewGuid();
        HierarchyMembership[] memberships =
        [
            new(Team, Root, new LocalDate(2026, 6, 1), new LocalDate(2027, 3, 15)),
            new(other, Root, new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 12)),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Single(n => n.TeamId == Team).Periods.Should().Equal(new HierarchyPeriod(From, To));
        nodes.Single(n => n.TeamId == other).Periods.Should().Equal(new HierarchyPeriod(new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 12)));
    }

    [Fact]
    public void Descendants_OrdersSiblingsByNameIgnoringCase_WhateverOrderTheyArriveIn()
    {
        // Arrange — siblings out of name order, as rows read without an ORDER BY can arrive
        var lending = Guid.NewGuid();
        var cards = Guid.NewGuid();
        var accounts = Guid.NewGuid();
        HierarchyMembership[] memberships =
        [
            new(lending, Root, From, null, "Lending"),
            new(cards, Root, From, null, "cards"),
            new(accounts, Root, From, null, "Accounts"),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);
        var reversed = TeamHierarchyPeriods.Descendants(Root, From, To, memberships.Reverse());

        // Assert
        nodes.Select(n => n.TeamId).Should().Equal(accounts, cards, lending);
        reversed.Select(n => n.TeamId).Should().Equal(accounts, cards, lending);
    }

    [Fact]
    public void Descendants_WithoutNames_OrdersSiblingsById()
    {
        // Arrange
        var first = new Guid("00000000-0000-0000-0000-000000000001");
        var second = new Guid("00000000-0000-0000-0000-000000000002");
        HierarchyMembership[] memberships = [new(second, Root, From, null), new(first, Root, From, null)];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Select(n => n.TeamId).Should().Equal(first, second);
    }

    [Fact]
    public void Descendants_ForAChildThatLeftAndCameBack_GivesOneNodeWithBothPeriodsInDateOrder()
    {
        // Arrange — the later membership first
        HierarchyMembership[] memberships =
        [
            new(Team, Root, new LocalDate(2027, 2, 20), null),
            new(Team, Root, new LocalDate(2027, 1, 1), new LocalDate(2027, 2, 5)),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Should().ContainSingle().Which.Periods.Should().Equal(
            new HierarchyPeriod(From, new LocalDate(2027, 2, 5)),
            new HierarchyPeriod(new LocalDate(2027, 2, 20), To));
    }

    [Fact]
    public void Descendants_JoinsMembershipsThatRunOnFromOneAnother()
    {
        // Arrange — the second starts the day after the first ends
        HierarchyMembership[] memberships =
        [
            new(Team, Root, new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 9)),
            new(Team, Root, new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 15)),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Should().ContainSingle().Which.Periods.Should().Equal(new HierarchyPeriod(new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 15)));
    }

    [Fact]
    public void Descendants_CountsAGrandchildOnlyWhileItsParentWasBeneathTheRoot()
    {
        // Arrange — the team is in the group all along, but the group joins the root on the 10th
        HierarchyMembership[] memberships =
        [
            new(Group, Root, new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 20)),
            new(Team, Group, new LocalDate(2026, 1, 1), new LocalDate(2027, 2, 15)),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        var group = nodes.Should().ContainSingle().Subject;
        group.TeamId.Should().Be(Group);
        group.Periods.Should().Equal(new HierarchyPeriod(new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 20)));
        var team = group.Children.Should().ContainSingle().Subject;
        team.TeamId.Should().Be(Team);
        team.Periods.Should().Equal(new HierarchyPeriod(new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 15)));
    }

    [Fact]
    public void Descendants_LeavesOutAGrandchildWithNoDayWhileItsParentWasBeneathTheRoot()
    {
        // Arrange — the team left the group before the group joined the root
        HierarchyMembership[] memberships =
        [
            new(Group, Root, new LocalDate(2027, 2, 10), null),
            new(Team, Group, new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 9)),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Should().ContainSingle().Which.Children.Should().BeEmpty();
    }

    [Fact]
    public void Descendants_ForATeamThatMovedBetweenTwoParents_PutsItUnderEachForItsDaysThere()
    {
        // Arrange
        HierarchyMembership[] memberships =
        [
            new(Group, Root, new LocalDate(2026, 1, 1), null),
            new(OtherGroup, Root, new LocalDate(2026, 1, 1), null),
            new(Team, Group, new LocalDate(2026, 1, 1), new LocalDate(2027, 2, 14)),
            new(Team, OtherGroup, new LocalDate(2027, 2, 15), null),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Single(n => n.TeamId == Group).Children.Should().ContainSingle()
            .Which.Periods.Should().Equal(new HierarchyPeriod(From, new LocalDate(2027, 2, 14)));
        nodes.Single(n => n.TeamId == OtherGroup).Children.Should().ContainSingle()
            .Which.Periods.Should().Equal(new HierarchyPeriod(new LocalDate(2027, 2, 15), To));
    }

    [Fact]
    public void Descendants_LeavesOutMembershipsOfATeamOfTeamsNotBeneathTheRoot()
    {
        // Arrange
        HierarchyMembership[] memberships = [new(Team, OtherGroup, new LocalDate(2026, 1, 1), null)];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        nodes.Should().BeEmpty();
    }

    [Fact]
    public void Descendants_WhenDatedMembershipsFormALoop_DoesNotWalkATeamTwiceOnOnePath()
    {
        // Arrange — the group is beneath the root early in the range, and the root beneath the group later
        HierarchyMembership[] memberships =
        [
            new(Group, Root, new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 20)),
            new(Root, Group, new LocalDate(2027, 2, 10), null),
        ];

        // Act
        var nodes = TeamHierarchyPeriods.Descendants(Root, From, To, memberships);

        // Assert
        var group = nodes.Should().ContainSingle().Subject;
        group.TeamId.Should().Be(Group);
        group.Children.Should().BeEmpty();
    }
}
