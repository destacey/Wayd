using Wayd.Common.Domain.Replication;

namespace Wayd.Common.Domain.Tests.Sut.Replication;

public sealed class ReplicaWatermarkTests
{
    private static readonly Instant Applied = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    [Fact]
    public void IsStale_WhenNothingHasBeenApplied_IsFalse()
    {
        // Arrange
        Instant? watermark = null;

        // Act
        var stale = ReplicaWatermark.IsStale(watermark, Applied.Minus(Duration.FromDays(365)));

        // Assert
        stale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_WhenOlderThanTheLastAppliedChange_IsTrue()
    {
        // Arrange
        var older = Applied.Minus(Duration.FromMilliseconds(1));

        // Act
        var stale = ReplicaWatermark.IsStale(Applied, older);

        // Assert
        stale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WhenItIsTheSameInstant_IsFalse()
    {
        // Arrange — a redelivery carries the timestamp already applied.

        // Act
        var stale = ReplicaWatermark.IsStale(Applied, Applied);

        // Assert
        stale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_WhenNewerThanTheLastAppliedChange_IsFalse()
    {
        // Arrange
        var newer = Applied.Plus(Duration.FromMilliseconds(1));

        // Act
        var stale = ReplicaWatermark.IsStale(Applied, newer);

        // Assert
        stale.Should().BeFalse();
    }
}
