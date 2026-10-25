using Wayd.Infrastructure.RateLimiting;

namespace Wayd.Infrastructure.Tests.Sut.RateLimiting;

/// <summary>
/// Holds <see cref="RateLimitSettings.All"/> to every limit the settings declare, because startup validation checks only
/// the limits it lists: one left out could be configured to zero and refuse every request instead of failing at startup.
/// </summary>
public sealed class RateLimitSettingsTests
{
    [Fact]
    public void All_ListsEveryLimitTheSettingsDeclare()
    {
        // Arrange
        var settings = new RateLimitSettings();
        var declared = typeof(RateLimitSettings).GetProperties()
            .Where(p => typeof(IRateLimit).IsAssignableFrom(p.PropertyType))
            .Select(p => (IRateLimit)p.GetValue(settings)!)
            .ToList();

        // Act
        var listed = settings.All().ToList();

        // Assert
        declared.Should().HaveCount(8);
        listed.Should().HaveCount(declared.Count);
        declared.Should().OnlyContain(limit => listed.Any(l => ReferenceEquals(l, limit)));
    }

    [Fact]
    public void Defaults_AreEveryOneValid()
    {
        // Arrange
        var settings = new RateLimitSettings();

        // Act
        var invalid = settings.All().Where(l => !l.IsValid());

        // Assert
        invalid.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(30, 0)]
    [InlineData(-1, 60)]
    public void FixedWindowLimit_IsInvalid_WithANumberAtOrBelowZero(int permitLimit, int windowSeconds)
    {
        // Arrange
        var limit = new FixedWindowLimit { PermitLimit = permitLimit, WindowSeconds = windowSeconds };

        // Act
        var valid = limit.IsValid();

        // Assert
        valid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(200, 0, 1)]
    [InlineData(200, 5, 0)]
    public void TokenBucketLimit_IsInvalid_WithANumberAtOrBelowZero(int tokenLimit, int tokensPerPeriod, int periodSeconds)
    {
        // Arrange
        var limit = new TokenBucketLimit { TokenLimit = tokenLimit, TokensPerPeriod = tokensPerPeriod, PeriodSeconds = periodSeconds };

        // Act
        var valid = limit.IsValid();

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void ConcurrencyLimit_IsInvalid_AtZero()
    {
        // Arrange
        var limit = new ConcurrencyLimit { PermitLimit = 0 };

        // Act
        var valid = limit.IsValid();

        // Assert
        valid.Should().BeFalse();
    }
}
