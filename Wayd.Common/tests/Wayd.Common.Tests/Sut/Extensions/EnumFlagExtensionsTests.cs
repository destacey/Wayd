using Wayd.Common.Extensions;

namespace Wayd.Common.Tests.Sut.Extensions;

public sealed class EnumFlagExtensionsTests
{
    /// <summary>A flags enum with no zero member, so "none" has to be said with null.</summary>
    [Flags]
    public enum Access
    {
        Read = 1,
        Write = 2,
        Delete = 4,
        All = Read | Write | Delete
    }

    /// <summary>A flags enum that names zero itself, which changes what counts as a valid value.</summary>
    [Flags]
    public enum Channel
    {
        None = 0,
        Email = 1,
        Sms = 2
    }

    [Theory]
    [InlineData(Access.Read)]
    [InlineData(Access.Write)]
    [InlineData(Access.Read | Access.Write)]
    [InlineData(Access.All)]
    public void IsValidFlagCombination_ForDeclaredBits_ShouldBeTrue(Access value)
    {
        // Act
        var result = value.IsValidFlagCombination();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidFlagCombination_ShouldAcceptACombinationEnumIsDefinedRejects()
    {
        // Arrange — the point of the method: IsDefined knows only declared members
        var value = Access.Read | Access.Write;

        // Act & Assert
        value.IsValidFlagCombination().Should().BeTrue();
        Enum.IsDefined(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidFlagCombination_ForABitTheEnumDoesNotDeclare_ShouldBeFalse()
    {
        // Arrange
        var value = (Access)16;

        // Act
        var result = value.IsValidFlagCombination();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFlagCombination_ForZero_ShouldBeFalse_WhereTheEnumDeclaresNoZero()
    {
        // Arrange — an enum with no zero member is saying "none" is not one of its values
        var value = default(Access);

        // Act
        var result = value.IsValidFlagCombination();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFlagCombination_ForZero_ShouldBeTrue_WhereTheEnumDeclaresZero()
    {
        // Act
        var result = Channel.None.IsValidFlagCombination();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ToFlags_ShouldListTheMembersNamed_Ascending()
    {
        // Act
        var result = (Access.Delete | Access.Read).ToFlags();

        // Assert
        result.Should().Equal(Access.Read, Access.Delete);
    }

    [Fact]
    public void ToFlags_ShouldNotListADeclaredCombinationAlongsideItsParts()
    {
        // Act — All is declared, but returning it beside Read/Write/Delete would double-count
        var result = Access.All.ToFlags();

        // Assert
        result.Should().Equal(Access.Read, Access.Write, Access.Delete);
    }

    [Fact]
    public void ToFlags_ShouldNotListADeclaredZero()
    {
        // Arrange — every value "has" a zero flag, so listing None would put it in every collection
        var value = Channel.Email;

        // Act
        var result = value.ToFlags();

        // Assert
        result.Should().Equal(Channel.Email);
    }

    [Fact]
    public void ToFlags_ForNull_ShouldBeNull()
    {
        // Arrange — nothing recorded, which an empty collection would not distinguish
        Access? value = null;

        // Act
        var result = value.ToFlags();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToFlagCombination_ShouldCombineEveryMember()
    {
        // Act
        var result = new[] { Access.Read, Access.Delete }.ToFlagCombination();

        // Assert
        result.Should().Be(Access.Read | Access.Delete);
    }

    [Fact]
    public void ToFlagCombination_ShouldIgnoreARepeatedMember()
    {
        // Act
        var result = new[] { Access.Write, Access.Write }.ToFlagCombination();

        // Assert
        result.Should().Be(Access.Write);
    }

    [Fact]
    public void ToFlagCombination_ForAnEmptyCollection_ShouldBeNull()
    {
        // Arrange — a boundary with no null of its own says "nothing recorded" by sending nothing
        var flags = Array.Empty<Access>();

        // Act
        var result = flags.ToFlagCombination();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToFlagCombination_ForNull_ShouldBeNull()
    {
        // Act
        var result = ((IEnumerable<Access>?)null).ToFlagCombination();

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(Access.Read)]
    [InlineData(Access.Write | Access.Delete)]
    [InlineData(Access.All)]
    public void ToFlags_ThenToFlagCombination_ShouldRoundTrip(Access value)
    {
        // Act
        var result = value.ToFlags().ToFlagCombination();

        // Assert
        result.Should().Be(value);
    }
}
