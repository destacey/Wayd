using Wayd.Infrastructure.Common.Services;

namespace Wayd.Infrastructure.Tests.Sut.Common.Services;

public sealed class DateTimeProviderTests
{
    [Fact]
    public void Now_ShouldReturnInstantFromInjectedTimeProvider()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 7, 1, 23, 45, 30, TimeSpan.Zero);
        var sut = new DateTimeProvider(new FixedTimeProvider(now));

        // Act
        var result = sut.Now;

        // Assert
        result.Should().Be(Instant.FromUtc(2026, 7, 1, 23, 45, 30));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
