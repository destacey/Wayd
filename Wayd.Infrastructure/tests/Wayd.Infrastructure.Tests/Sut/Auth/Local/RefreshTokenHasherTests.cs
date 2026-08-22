using Wayd.Infrastructure.Auth.Local;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Local;

public class RefreshTokenHasherTests
{
    private const string Token = "kFq2xJ8mTn4pRb7vYc1wZs5hLd0aGe3u";

    [Fact]
    public void Hash_ShouldNotReturnTheToken()
    {
        // Arrange & Act
        var hash = RefreshTokenHasher.Hash(Token);

        // Assert
        hash.Should().NotContain(Token);
    }

    [Fact]
    public void Hash_ShouldProduceDifferentHashesForTheSameToken()
    {
        // Arrange & Act
        var first = RefreshTokenHasher.Hash(Token);
        var second = RefreshTokenHasher.Hash(Token);

        // Assert
        first.Should().NotBe(second, "each hash carries its own random salt");
    }

    [Fact]
    public void Hash_ShouldFitTheColumn()
    {
        // Arrange & Act
        var hash = RefreshTokenHasher.Hash(Token);

        // Assert
        hash.Length.Should().BeLessThanOrEqualTo(256);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenTokenMatches()
    {
        // Arrange
        var hash = RefreshTokenHasher.Hash(Token);

        // Act
        var result = RefreshTokenHasher.Verify(Token, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenTokenDiffers()
    {
        // Arrange
        var hash = RefreshTokenHasher.Hash(Token);

        // Act
        var result = RefreshTokenHasher.Verify("nQw9zA6bXr2tKm5yUh8jVp1cFd4sGe7i", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-salted-hash")]
    [InlineData("only-one-part:")]
    [InlineData("!!!not-base64!!!:!!!not-base64!!!")]
    public void Verify_ShouldReturnFalse_WhenStoredValueIsUnusable(string? storedHash)
    {
        // A row still holding a pre-hashing plaintext token lands here and must fail
        // closed rather than throw.

        // Act
        var result = RefreshTokenHasher.Verify(Token, storedHash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_ShouldReturnFalse_WhenPresentedTokenIsMissing(string? token)
    {
        // Arrange
        var hash = RefreshTokenHasher.Hash(Token);

        // Act
        var result = RefreshTokenHasher.Verify(token, hash);

        // Assert
        result.Should().BeFalse();
    }
}
