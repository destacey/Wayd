using Wayd.Organization.Domain.Models;
using NodaTime;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class MembershipDateRangeTests
{
    #region New

    [Fact]
    public void New_WithGap_Success()
    {
        // ARRANGE
        LocalDate start = new(2020, 1, 1);
        LocalDate? end = new(2020, 2, 1);

        // ACT
        var result = new MembershipDateRange(start, end);

        // ASSERT
        result.Start.Should().Be(start);
        result.End.Should().Be(end);
    }

    [Fact]
    public void New_NoGap_Success()
    {
        // ARRANGE
        LocalDate start = new(2020, 1, 1);
        LocalDate? end = new(2020, 1, 2);

        // ACT
        var result = new MembershipDateRange(start, end);

        // ASSERT
        result.Start.Should().Be(start);
        result.End.Should().Be(end);
    }

    [Fact]
    public void New_SameValue_Success()
    {
        // ARRANGE
        LocalDate start = new(2020, 1, 1);
        LocalDate? end = new(2020, 1, 1);

        // ACT
        var result = new MembershipDateRange(start, end);

        // ASSERT
        result.Start.Should().Be(start);
        result.End.Should().Be(end);
    }

    [Fact]
    public void New_EarlierEnd_Throws()
    {
        // ARRANGE
        LocalDate start = new(2020, 1, 2);
        LocalDate? end = new(2020, 1, 1);

        // ACT & ASSERT
        var excpetion = Assert.Throws<ArgumentException>(() => new MembershipDateRange(start, end));

        // ASSERT
        excpetion.Message.Should().Be("The start date must be on or before the end date. (Parameter 'LocalDateRange')");
    }

    [Fact]
    public void New_NullStart_Throws()
    {
        // ARRANGE
        LocalDate? start = null;
        LocalDate? end = new(2020, 1, 1);

        // ACT & ASSERT
        // The Guard.Against clause should return an ArgumentNullException, but it isn't
        var excpetion = Assert.Throws<InvalidOperationException>(() => new MembershipDateRange((LocalDate)start!, end));

        // ASSERT
        excpetion.Message.Should().Be("Nullable object must have a value.");
    }

    #endregion New

    #region Includes

    [Theory]
    [MemberData(nameof(Includes_SingleData))]
    public void Includes_Single(LocalDate start, LocalDate? end, LocalDate localDate, bool expected)
    {
        // ACT
        var range = new MembershipDateRange(start, end);
        var result = range.Includes(localDate);

        // ASSERT
        result.Should().Be(expected);
    }
    public static IEnumerable<object[]> Includes_SingleData()
    {
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 10),
            new LocalDate(2020, 1, 1),
            true
        };
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 10),
            new LocalDate(2020, 1, 10),
            true
        };
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 10),
            new LocalDate(2020, 1, 2),
            true
        };
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 1),
            true
        };
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 10),
            new LocalDate(2020, 1, 11),
            false
        };
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            new LocalDate(2020, 1, 10),
            new LocalDate(2019, 1, 1),
            false
        };
        // null end scenarios
        yield return new object[]
        {
            new LocalDate(2020, 1, 1),
            null!,
            new LocalDate(2020, 3, 1),
            true
        };
        yield return new object[]
        {
            new LocalDate(2020, 3, 1),
            null!,
            new LocalDate(2020, 1, 1),
            false
        };
    }

    [Theory]
    [MemberData(nameof(Includes_RangeData))]
    public void Includes_Range(MembershipDateRange range1, MembershipDateRange range2, bool expected)
    {
        // ACT
        var result = range1.Includes(range2);

        // ASSERT
        result.Should().Be(expected);
    }
    public static IEnumerable<object[]> Includes_RangeData()
    {
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 5),
                new LocalDate(2020, 1, 7)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 9),
                new LocalDate(2020, 1, 10)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 1)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 1)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 2),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 11)),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            false
        };
        // null end scenarios
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                null),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                null),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 20)),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 2),
                null),
            false
        };
    }

    #endregion Includes

    #region Overlaps

    [Theory]
    [MemberData(nameof(Overlaps_RangeData))]
    public void Overlaps_Range(MembershipDateRange range1, MembershipDateRange range2, bool expected)
    {
        // ACT
        var result = range1.Overlaps(range2);

        // ASSERT
        result.Should().Be(expected);
    }
    public static IEnumerable<object[]> Overlaps_RangeData()
    {
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 5),
                new LocalDate(2020, 1, 7)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 5),
                new LocalDate(2020, 1, 7)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 10)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 5),
                new LocalDate(2020, 1, 10)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 5),
                new LocalDate(2020, 1, 11)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 18),
                new LocalDate(2020, 1, 25)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 20),
                new LocalDate(2020, 1, 25)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            false
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            false
        };
        // null end scenarios
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                new LocalDate(2020, 1, 20)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 10),
                null),
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 20)),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                new LocalDate(2020, 1, 9)),
            new MembershipDateRange(
                new LocalDate(2020, 1, 2),
                null),
            true
        };
        yield return new object[]
        {
            new MembershipDateRange(
                new LocalDate(2020, 1, 1),
                null),
            new MembershipDateRange(
                new LocalDate(2020, 1, 2),
                null),
            true
        };
    }

    #endregion Overlaps

    #region Equals

    [Fact]
    public void GetEqualityComponents_SameValue_EqualityTrue()
    {
        // ARRANGE
        LocalDate start = new(2020, 1, 1);
        LocalDate? end = new(2020, 1, 1);

        // ACT
        var result1 = new MembershipDateRange(start, end);
        var result2 = new MembershipDateRange(start, end);

        // ASSERT
        result1.Should().Be(result2);
        result1.GetHashCode().Should().Be(result2.GetHashCode());
        result1.Start.Should().Be(result2.Start);
        result1.End.Should().Be(result2.End);
    }


    [Fact]
    public void GetEqualityComponents_DifferentValue_EqualityFalse()
    {
        // ARRANGE
        LocalDate start = new(2020, 1, 1);
        LocalDate? end = new(2020, 1, 1);
        LocalDate start2 = new(2020, 1, 1);
        LocalDate? end2 = new(2020, 1, 2);

        // ACT
        var result1 = new MembershipDateRange(start, end);
        var result2 = new MembershipDateRange(start2, end2);

        // ASSERT
        result1.Should().NotBe(result2);
        result1.GetHashCode().Should().NotBe(result2.GetHashCode());
    }

    #endregion Equals

    #region Intersect

    [Fact]
    public void Intersect_BothBounded_ReturnsTheSharedDays()
    {
        // Arrange
        var range = new MembershipDateRange(new LocalDate(2020, 1, 1), new LocalDate(2020, 1, 31));
        var other = new MembershipDateRange(new LocalDate(2020, 1, 15), new LocalDate(2020, 2, 15));

        // Act
        var result = range.Intersect(other);

        // Assert
        result.Should().Be(new MembershipDateRange(new LocalDate(2020, 1, 15), new LocalDate(2020, 1, 31)));
    }

    [Fact]
    public void Intersect_OneOpenEnded_TakesTheBoundedEnd()
    {
        // Arrange
        var range = new MembershipDateRange(new LocalDate(2020, 1, 1), null);
        var other = new MembershipDateRange(new LocalDate(2019, 6, 1), new LocalDate(2020, 3, 1));

        // Act
        var result = range.Intersect(other);

        // Assert
        result.Should().Be(new MembershipDateRange(new LocalDate(2020, 1, 1), new LocalDate(2020, 3, 1)));
    }

    [Fact]
    public void Intersect_BothOpenEnded_StaysOpen()
    {
        // Arrange
        var range = new MembershipDateRange(new LocalDate(2020, 1, 1), null);
        var other = new MembershipDateRange(new LocalDate(2020, 5, 1), null);

        // Act
        var result = range.Intersect(other);

        // Assert
        result.Should().Be(new MembershipDateRange(new LocalDate(2020, 5, 1), null));
    }

    [Fact]
    public void Intersect_SharingOneDay_ReturnsThatDay()
    {
        // Arrange
        var range = new MembershipDateRange(new LocalDate(2020, 1, 1), new LocalDate(2020, 1, 31));
        var other = new MembershipDateRange(new LocalDate(2020, 1, 31), null);

        // Act
        var result = range.Intersect(other);

        // Assert
        result.Should().Be(new MembershipDateRange(new LocalDate(2020, 1, 31), new LocalDate(2020, 1, 31)));
    }

    [Fact]
    public void Intersect_Disjoint_ReturnsNull()
    {
        // Arrange
        var range = new MembershipDateRange(new LocalDate(2020, 1, 1), new LocalDate(2020, 1, 31));
        var other = new MembershipDateRange(new LocalDate(2020, 2, 1), null);

        // Act
        var result = range.Intersect(other);

        // Assert
        result.Should().BeNull();
    }

    #endregion Intersect
}
