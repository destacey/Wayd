using FluentAssertions;
using Wayd.AppIntegration.Application.Connections;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections;

public class ConnectionSecretTests
{
    private const string StoredSecret = "stored-token-value-1234";

    [Fact]
    public void Masked_ReturnsFixedWidthPlaceholder_ForAnyStoredSecret()
    {
        // Arrange
        var shortSecret = "ab";
        var longSecret = new string('x', 200);

        // Act
        var maskedShort = ConnectionSecret.Masked(shortSecret);
        var maskedLong = ConnectionSecret.Masked(longSecret);

        // Assert
        maskedShort.Should().Be(maskedLong,
            "a length-preserving mask would disclose the secret's length to any Connections.View holder");
        maskedShort.Should().Be(ConnectionSecret.Mask);
    }

    [Fact]
    public void Masked_DoesNotRevealAnyCharacterOfTheSecret()
    {
        // Arrange
        var secret = "abcdEFGH1234";

        // Act
        var masked = ConnectionSecret.Masked(secret);

        // Assert
        masked.Should().NotContain("abcd", "the mask must not carry a prefix of the real secret");
        masked.Should().MatchRegex("^\\*+$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Masked_ReturnsEmpty_WhenNoSecretIsStored(string? secret)
    {
        // Act
        var masked = ConnectionSecret.Masked(secret);

        // Assert
        masked.Should().BeEmpty("a fixed-width mask must still distinguish 'set' from 'not set'");
    }

    [Theory]
    [InlineData("********")]          // the placeholder this class emits
    [InlineData("abcd****")]          // legacy length-preserving mask
    [InlineData("abcd************")]
    public void IsMasked_RecognisesMaskedValues(string value)
    {
        // Act
        var isMasked = ConnectionSecret.IsMasked(value);

        // Assert
        isMasked.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcdEFGH1234")]
    [InlineData("abcd")]
    [InlineData("pa**word")]          // asterisks inside the first 4 chars are not a mask shape
    [InlineData("*abc****")]
    public void IsMasked_RejectsValuesThatAreNotMasks(string? value)
    {
        // Act
        var isMasked = ConnectionSecret.IsMasked(value);

        // Assert
        isMasked.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_KeepsStoredSecret_WhenSubmittedValueIsBlank(string? submitted)
    {
        // Act
        var resolved = ConnectionSecret.Resolve(submitted, StoredSecret);

        // Assert
        resolved.Should().Be(StoredSecret, "an omitted secret means 'keep', never 'clear'");
    }

    [Theory]
    [InlineData("********")]
    [InlineData("stor******************")]
    public void Resolve_KeepsStoredSecret_WhenSubmittedValueIsMasked(string submitted)
    {
        // Act
        var resolved = ConnectionSecret.Resolve(submitted, StoredSecret);

        // Assert
        resolved.Should().Be(StoredSecret,
            "a masked value is never a real credential, so storing it could only break the connection");
    }

    [Fact]
    public void Resolve_ReplacesStoredSecret_WhenAGenuinelyNewValueIsSubmitted()
    {
        // Arrange
        var newSecret = "rotated-token-value-5678";

        // Act
        var resolved = ConnectionSecret.Resolve(newSecret, StoredSecret);

        // Assert
        resolved.Should().Be(newSecret);
    }

    [Fact]
    public void Resolve_ReplacesStoredSecret_WhenNewValueSharesThePrefixAndLengthOfTheStoredOne()
    {
        // Arrange - the superseded heuristic compared first-4-chars plus length, so a rotated
        // secret matching on both was silently discarded.
        var newSecret = "storedXtoken-value-1234";
        newSecret.Length.Should().Be(StoredSecret.Length);

        // Act
        var resolved = ConnectionSecret.Resolve(newSecret, StoredSecret);

        // Assert
        resolved.Should().Be(newSecret);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ab")]
    [InlineData("a")]
    public void Resolve_HandlesStoredSecretsShorterThanTheLegacyPrefix(string stored)
    {
        // Arrange - the superseded heuristic indexed [..4] unguarded and threw for these.
        var submitted = "a-new-secret";

        // Act
        var resolved = ConnectionSecret.Resolve(submitted, stored);

        // Assert
        resolved.Should().Be(submitted);
    }
}
