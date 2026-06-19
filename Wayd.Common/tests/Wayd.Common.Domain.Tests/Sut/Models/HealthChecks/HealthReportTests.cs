using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.HealthChecks;
using Wayd.Common.Domain.Tests.Data.Models;

namespace Wayd.Common.Domain.Tests.Sut.Models.HealthChecks;

public sealed class HealthReportTests
{
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);

    [Fact]
    public void Constructor_OrdersExistingChecksByReportedOnDescending()
    {
        var older = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now.Minus(Duration.FromDays(7)), _now.Minus(Duration.FromDays(1)), null);
        var newer = TestHealthCheck.Create(HealthStatus.AtRisk, Guid.NewGuid(), _now.Minus(Duration.FromDays(2)), _now.Plus(Duration.FromDays(2)), null);

        var sut = new HealthReport<TestHealthCheck>([older, newer]);

        sut.HealthChecks.Should().ContainInOrder(newer, older);
    }

    [Fact]
    public void Add_WhenReportIsEmpty_AppendsTheCheck()
    {
        var sut = new HealthReport<TestHealthCheck>([]);
        var check = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), null);

        sut.Add(check, _now);

        sut.HealthChecks.Should().ContainSingle().Which.Should().Be(check);
    }

    [Fact]
    public void Add_WhenLatestStillActive_TruncatesPreviousExpirationToNow()
    {
        var existingReportedOn = _now.Minus(Duration.FromDays(2));
        var existingExpiration = _now.Plus(Duration.FromDays(5));
        var existing = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), existingReportedOn, existingExpiration, null);

        var sut = new HealthReport<TestHealthCheck>([existing]);

        var newCheck = TestHealthCheck.Create(HealthStatus.AtRisk, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), null);
        sut.Add(newCheck, _now);

        existing.Expiration.Should().Be(_now);
        existing.IsExpired(_now).Should().BeTrue();
    }

    [Fact]
    public void Add_WhenLatestAlreadyExpired_DoesNotChangePreviousExpiration()
    {
        var existingReportedOn = _now.Minus(Duration.FromDays(7));
        var existingExpiration = _now.Minus(Duration.FromDays(1));
        var existing = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), existingReportedOn, existingExpiration, null);

        var sut = new HealthReport<TestHealthCheck>([existing]);

        var newCheck = TestHealthCheck.Create(HealthStatus.AtRisk, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), null);
        sut.Add(newCheck, _now);

        existing.Expiration.Should().Be(existingExpiration);
    }

    [Fact]
    public void Add_PutsTheNewCheckAtTheFrontOfTheCollection()
    {
        var existing = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now.Minus(Duration.FromDays(2)), _now.Plus(Duration.FromDays(5)), null);
        var sut = new HealthReport<TestHealthCheck>([existing]);

        var newCheck = TestHealthCheck.Create(HealthStatus.AtRisk, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), null);
        sut.Add(newCheck, _now);

        sut.HealthChecks.Should().ContainInOrder(newCheck, existing);
    }

    [Fact]
    public void Add_AfterMultipleAdds_PreservesNoOverlapInvariant()
    {
        var sut = new HealthReport<TestHealthCheck>([]);

        var first = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), null);
        sut.Add(first, _now);

        var laterNow = _now.Plus(Duration.FromDays(2));
        var second = TestHealthCheck.Create(HealthStatus.AtRisk, Guid.NewGuid(), laterNow, laterNow.Plus(Duration.FromDays(7)), null);
        sut.Add(second, laterNow);

        first.Expiration.Should().Be(laterNow);

        // After both adds, only `second` should be active.
        sut.HealthChecks.Count(h => !h.IsExpired(laterNow)).Should().Be(1);
        sut.HealthChecks.Single(h => !h.IsExpired(laterNow)).Should().Be(second);
    }
}
