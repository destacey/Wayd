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
            .Where(p => p.PropertyType == typeof(FixedWindowLimit))
            .Select(p => (FixedWindowLimit)p.GetValue(settings)!)
            .ToList();

        // Act
        var listed = settings.All().ToList();

        // Assert
        declared.Should().HaveCount(6);
        listed.Should().HaveCount(declared.Count);
        declared.Should().OnlyContain(limit => listed.Any(l => ReferenceEquals(l, limit)));
    }

    [Fact]
    public void Defaults_GiveEveryLimitAPositivePermitCountAndWindow()
    {
        // Arrange
        var settings = new RateLimitSettings();

        // Act
        var invalid = settings.All().Where(l => l.PermitLimit <= 0 || l.WindowSeconds <= 0);

        // Assert
        invalid.Should().BeEmpty();
    }
}
