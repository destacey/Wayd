using Wayd.Infrastructure.RateLimiting;

namespace Wayd.Infrastructure.Tests.Sut.RateLimiting;

/// <summary>
/// Holds the wait a refusal announces to whole seconds rounded up, so a caller that waits exactly as long as it is told
/// is never refused again for retrying a fraction of a second early.
/// </summary>
public sealed class RateLimitRejectionTests
{
    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(1.0, 1)]
    [InlineData(1.01, 2)]
    [InlineData(59.5, 60)]
    public void Seconds_RoundsTheWaitUp(double seconds, int expected)
    {
        // Act
        var result = RateLimitRejection.Seconds(TimeSpan.FromSeconds(seconds));

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Describe_NamesTheWait_WhenItIsKnown()
    {
        // Act
        var description = RateLimitRejection.Describe(TimeSpan.FromSeconds(11.4));

        // Assert
        description.Should().Be("Too many requests. Try again in 12 seconds.");
    }

    [Fact]
    public void Describe_SaysShortly_WhenTheWaitIsUnknown()
    {
        // Act
        var description = RateLimitRejection.Describe(null);

        // Assert
        description.Should().Be("Too many requests. Try again shortly.");
    }
}
