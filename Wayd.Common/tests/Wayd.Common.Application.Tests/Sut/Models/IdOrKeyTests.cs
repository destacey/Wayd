using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Models;

public sealed class IdOrKeyTests
{
    [Fact]
    public void Constructor_ReadsAGuidAsAnId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var idOrKey = new IdOrKey(id.ToString());

        // Assert
        idOrKey.AsId.Should().Be(id);
    }

    [Fact]
    public void Constructor_ReadsAnIntegerAsAKey()
    {
        // Act
        var idOrKey = new IdOrKey("42");

        // Assert
        idOrKey.AsKey.Should().Be(42);
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("99999999999")]
    public void Constructor_ThrowsNotFound_ForAValueThatCanNameNoRecord(string value)
    {
        // Act
        var act = () => new IdOrKey(value);

        // Assert
        act.Should().Throw<NotFoundException>().WithMessage($"*'{value}'*");
    }
}
