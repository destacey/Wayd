using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>
/// Holds a client's self-chosen name to what a consent page can safely show: no characters that reorder or hide
/// text, no line breaks, and short enough that the client's real host stays in view.
/// </summary>
public sealed class ClientDisplayNameTests
{
    private const string Host = "client.acme.example";

    [Theory]
    [InlineData("Acme Agent", "Acme Agent")]
    [InlineData("  Acme \t\n Agent  ", "Acme Agent")]
    [InlineData("Acme\u202Etnega", "Acmetnega")]
    [InlineData("Acme\u2066Agent\u2069", "AcmeAgent")]
    [InlineData("Ac\u200Bme\u200D", "Acme")]
    [InlineData("Acme\u0000\u001B[31mAgent", "Acme[31mAgent")]
    [InlineData("Acme\u2028\u2029Agent", "Acme Agent")]
    [InlineData("\u00C4gent \u00DCnicode", "\u00C4gent \u00DCnicode")]
    public void Clean_RemovesCharactersThatCouldDisguiseTheName(string name, string expected)
    {
        // Arrange — a right-to-left override, isolates, zero-width characters, control codes, line and paragraph
        // separators (read as one space), and ordinary accented text that must survive

        // Act
        var result = ClientDisplayName.Clean(name, Host);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("\u202E\u200B\u0007")]
    public void Clean_FallsBackToTheHost_WhenNothingShowableIsLeft(string? name)
    {
        // Arrange

        // Act
        var result = ClientDisplayName.Clean(name, Host);

        // Assert
        result.Should().Be(Host);
    }

    [Fact]
    public void Clean_CapsALongNameAndMarksTheCut()
    {
        // Arrange
        var name = new string('a', ClientDisplayName.MaxLength + 40);

        // Act
        var result = ClientDisplayName.Clean(name, Host);

        // Assert
        result.Should().Be(new string('a', ClientDisplayName.MaxLength - 1) + "\u2026");
    }

    [Fact]
    public void Clean_KeepsANameAtTheCapWhole()
    {
        // Arrange
        var name = new string('a', ClientDisplayName.MaxLength);

        // Act
        var result = ClientDisplayName.Clean(name, Host);

        // Assert
        result.Should().Be(name);
    }

    [Fact]
    public void Clean_KeepsAtMostTwoCombiningMarksPerCharacter()
    {
        // Arrange — stacked marks ("Zalgo" text) spill over the text around the name; "q" has no precomposed form
        // with a grave accent, so normalization leaves every mark in place
        var grave = ((char)0x0300).ToString();
        var name = "q" + string.Concat(Enumerable.Repeat(grave, 10)) + "b";

        // Act
        var result = ClientDisplayName.Clean(name, Host);

        // Assert
        result.Should().Be("q" + grave + grave + "b");
    }

    [Fact]
    public void Clean_DropsACombiningMarkWithNothingToCombineWith()
    {
        // Arrange
        var grave = ((char)0x0300).ToString();

        // Act
        var result = ClientDisplayName.Clean(grave + "Acme " + grave + "Agent", Host);

        // Assert
        result.Should().Be("Acme Agent");
    }

    [Fact]
    public void Clean_CapsTheCharacterCount_WhenFewTextElementsHoldManyCharacters()
    {
        // Arrange — each family emoji is one text element of eleven UTF-16 characters
        var family = char.ConvertFromUtf32(0x1F468) + "\u200D" + char.ConvertFromUtf32(0x1F469) + "\u200D" + char.ConvertFromUtf32(0x1F467);
        var name = string.Concat(Enumerable.Repeat("x" + family, 30));

        // Act
        var result = ClientDisplayName.Clean(name, Host);

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(ClientDisplayName.MaxChars);
        result.Should().EndWith("\u2026");
    }

    [Fact]
    public void Clean_CapsTheFallbackHost()
    {
        // Arrange — a host may be up to 253 characters, more than the name's column holds
        var host = string.Join('.', Enumerable.Repeat(new string('h', 60), 4));

        // Act
        var result = ClientDisplayName.Clean(null, host);

        // Assert
        result.Should().Be(new string('h', ClientDisplayName.MaxLength - 1) + "…");
    }
}
