using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using NodaTime;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class TeamToTeamMembershipTests
{
    private static readonly LocalDate SeedActiveDate = new(2019, 1, 1);

    /// <summary>A real child/parent pair — Create now takes the teams so it can wire both navigations.</summary>
    private static (Team Child, TeamOfTeams Parent) Pair()
    {
        var child = new TeamFaker().WithActiveDate(SeedActiveDate).AsActive().Generate();
        var parent = new TeamOfTeamsFaker().WithActiveDate(SeedActiveDate).AsActive().Generate();
        return (child, parent);
    }
    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange
        var (child, parent) = Pair();
        var start = new LocalDate(2020, 1, 1);
        var end = new LocalDate(2020, 2, 1);
        var dateRange = new MembershipDateRange(start, end);

        // Act
        var sut = TeamMembership.Create(child, parent, dateRange);

        // Assert — both ids and both navigations, so a caller can walk the edge before it is saved
        sut.SourceId.Should().Be(child.Id);
        sut.TargetId.Should().Be(parent.Id);
        sut.Source.Should().BeSameAs(child);
        sut.Target.Should().BeSameAs(parent);
        sut.DateRange.Should().Be(dateRange);
    }

    [Fact]
    public void Create_WhenChildIdEqualsParentId_ThrowsArgumentException()
    {
        // Arrange
        var parent = new TeamOfTeamsFaker().WithActiveDate(SeedActiveDate).AsActive().Generate();
        var start = new LocalDate(2020, 1, 1);
        var end = new LocalDate(2020, 2, 1);
        var dateRange = new MembershipDateRange(start, end);

        // Act — the same team on both ends
        Action action = () => TeamMembership.Create(parent, parent, dateRange);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("A team or team of teams cannot have a membership with its self.");
    }

    #endregion Create

    #region IsPastOn

    [Theory]
    [MemberData(nameof(IsPastOnData))]
    public void IsPastOn(MembershipDateRange range, LocalDate now, bool expected)
    {
        // Arrange
        var sut = TeamMembership.Create(Pair().Child, Pair().Parent, range);

        // ACT
        var result = sut.IsPastOn(now);

        // ASSERT
        result.Should().Be(expected);
    }
    public static IEnumerable<object[]> IsPastOnData()
    {
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2019, 12, 31),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 1),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 10),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 11),
            true
        };
        // null end scenarios
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2019, 12, 31),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2020, 1, 1),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2020, 1, 10),
            false
        };
    }

    #endregion IsActiveOn


    #region IsActiveOn

    [Theory]
    [MemberData(nameof(IsActiveOnData))]
    public void IsActiveOn(MembershipDateRange range, LocalDate now, bool expected)
    {
        // Arrange
        var sut = TeamMembership.Create(Pair().Child, Pair().Parent, range);

        // ACT
        var result = sut.IsActiveOn(now);

        // ASSERT
        result.Should().Be(expected);
    }
    public static IEnumerable<object[]> IsActiveOnData()
    {
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2019, 12, 31),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 1),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 10),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 11),
            false
        };
        // null end scenarios
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2019, 12, 31),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2020, 1, 1),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2020, 1, 10),
            true
        };
    }

    #endregion IsActiveOn

    #region IsFutureOn

    [Theory]
    [MemberData(nameof(IsFutureOnData))]
    public void IsFutureOn(MembershipDateRange range, LocalDate now, bool expected)
    {
        // Arrange
        var sut = TeamMembership.Create(Pair().Child, Pair().Parent, range);

        // ACT
        var result = sut.IsFutureOn(now);

        // ASSERT
        result.Should().Be(expected);
    }
    public static IEnumerable<object[]> IsFutureOnData()
    {
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2019, 12, 31),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 1),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 10),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new LocalDate(2020, 1, 11),
            false
        };
        // null end scenarios
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2019, 12, 31),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2020, 1, 1),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new LocalDate(2020, 1, 10),
            false
        };
    }

    #endregion IsActiveOn
}
