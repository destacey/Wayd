using FluentAssertions;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Models;

public sealed class CursorEncodingTests
{
    [Fact]
    public void Decode_ShouldReturnThePartsThatWereEncoded()
    {
        // Arrange
        var cursor = CursorEncoding.Encode("638000000000000000", "42");

        // Act
        var parts = CursorEncoding.Decode(cursor, 2);

        // Assert
        parts.Should().Equal("638000000000000000", "42");
    }

    [Fact]
    public void Encode_ShouldProduceAUrlSafeStringWithoutPadding()
    {
        // Act — standard base64 of this text is "Pz8/Pj4+Pw==": a '/', a '+' and padding.
        var cursor = CursorEncoding.Encode("???>>>?");

        // Assert
        cursor.Should().Be("Pz8_Pj4-Pw");
        CursorEncoding.Decode(cursor, 1).Should().Equal("???>>>?");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 !")]
    public void Decode_ShouldRefuseAnythingThatIsNotACursor(string? value)
    {
        // Act & Assert
        CursorEncoding.Decode(value, 2).Should().BeNull();
    }

    [Fact]
    public void Decode_ShouldRefuseACursorWithTheWrongNumberOfParts()
    {
        // Arrange
        var cursor = CursorEncoding.Encode("1", "2", "3");

        // Act & Assert
        CursorEncoding.Decode(cursor, 2).Should().BeNull();
    }

    [Fact]
    public void Encode_ShouldRefuseAPartContainingTheSeparator()
    {
        // Act
        var encode = () => CursorEncoding.Encode("ppm:project", "1");

        // Assert
        encode.Should().Throw<ArgumentException>().WithParameterName("parts");
    }

    [Fact]
    public void Decode_ShouldRefuseAStringLongerThanAnyCursor()
    {
        // Arrange
        var cursor = CursorEncoding.Encode(new string('1', 100), new string('2', 100));

        // Act & Assert
        CursorEncoding.Decode(cursor, 2).Should().BeNull();
    }
}
