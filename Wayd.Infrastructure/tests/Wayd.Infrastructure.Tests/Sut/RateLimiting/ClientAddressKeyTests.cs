using System.Net;
using Wayd.Infrastructure.RateLimiting;

namespace Wayd.Infrastructure.Tests.Sut.RateLimiting;

/// <summary>
/// Holds every address in one IPv6 subscriber block to one key, and each IPv4 address to its own, so rotating the low
/// bits of an IPv6 address does not buy a caller a fresh rate-limit window.
/// </summary>
public sealed class ClientAddressKeyTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    [InlineData("::1", "::/64")]
    public void For_AnswersTheKeyTheAddressIsCountedUnder(string address, string expected)
    {
        // Arrange
        var parsed = IPAddress.Parse(address);

        // Act
        var key = ClientAddressKey.For(parsed);

        // Assert
        key.Should().Be(expected);
    }

    [Fact]
    public void For_CountsTwoAddressesInOneIPv6Block_UnderOneKey()
    {
        // Arrange
        var first = IPAddress.Parse("2001:db8:1:2::1");
        var second = IPAddress.Parse("2001:db8:1:2:ffff:ffff:ffff:ffff");

        // Act
        var keys = new[] { ClientAddressKey.For(first), ClientAddressKey.For(second) };

        // Assert
        keys.Distinct().Should().ContainSingle();
    }

    [Fact]
    public void For_CountsAddressesInNeighbouringIPv6Blocks_Separately()
    {
        // Arrange
        var first = IPAddress.Parse("2001:db8:1:2::1");
        var second = IPAddress.Parse("2001:db8:1:3::1");

        // Act
        var keys = new[] { ClientAddressKey.For(first), ClientAddressKey.For(second) };

        // Assert
        keys.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void For_AnswersUnknown_WhenTheRequestHasNoAddress()
    {
        // Act
        var key = ClientAddressKey.For(null);

        // Assert
        key.Should().Be(ClientAddressKey.Unknown);
    }
}
