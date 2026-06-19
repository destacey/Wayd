using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Tests.Data.Models;

namespace Wayd.Common.Domain.Tests.Sut.Models.HealthChecks;

public sealed class HealthCheckBaseTests
{
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);

    [Fact]
    public void Create_WithValidArgs_SetsAllProperties()
    {
        var reportedById = Guid.NewGuid();
        var expiration = _now.Plus(Duration.FromDays(7));

        var sut = TestHealthCheck.Create(HealthStatus.Healthy, reportedById, _now, expiration, "All good");

        sut.Status.Should().Be(HealthStatus.Healthy);
        sut.ReportedById.Should().Be(reportedById);
        sut.ReportedOn.Should().Be(_now);
        sut.Expiration.Should().Be(expiration);
        sut.Note.Should().Be("All good");
    }

    [Fact]
    public void Create_WhenExpirationEqualsReportedOn_Throws()
    {
        Action act = () => TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now, null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Expiration must be greater than ReportedOn*");
    }

    [Fact]
    public void Create_WhenExpirationBeforeReportedOn_Throws()
    {
        Action act = () => TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Minus(Duration.FromMinutes(1)), null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Note_TrimsAndCollapsesWhitespaceToNull()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(1)), "   ");

        sut.Note.Should().BeNull();
    }

    [Fact]
    public void IsExpired_WhenNowIsAfterExpiration_ReturnsTrue()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromHours(1)), null);

        sut.IsExpired(_now.Plus(Duration.FromHours(2))).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenNowEqualsExpiration_ReturnsTrue()
    {
        var expiration = _now.Plus(Duration.FromHours(1));
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, expiration, null);

        sut.IsExpired(expiration).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenNowBeforeExpiration_ReturnsFalse()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromHours(2)), null);

        sut.IsExpired(_now.Plus(Duration.FromHours(1))).Should().BeFalse();
    }

    [Fact]
    public void ChangeExpiration_WhenAfterReportedOn_UpdatesExpiration()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(2)), null);
        var newExpiration = _now.Plus(Duration.FromHours(3));

        sut.ChangeExpiration(newExpiration);

        sut.Expiration.Should().Be(newExpiration);
    }

    [Fact]
    public void ChangeExpiration_WhenEqualToReportedOn_AllowsTruncationToReportedOn()
    {
        // Truncation to ReportedOn is the lower bound used by HealthReport when adding a check at the same instant.
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(2)), null);

        Action act = () => sut.ChangeExpiration(_now);

        act.Should().NotThrow();
        sut.Expiration.Should().Be(_now);
    }

    [Fact]
    public void ChangeExpiration_WhenBeforeReportedOn_Throws()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(2)), null);

        Action act = () => sut.ChangeExpiration(_now.Minus(Duration.FromSeconds(1)));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_WhenActiveAndExpirationInFuture_AppliesChanges()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), "old");
        var newExpiration = _now.Plus(Duration.FromDays(14));

        var result = sut.Update(HealthStatus.AtRisk, newExpiration, "new", _now);

        result.IsSuccess.Should().BeTrue();
        sut.Status.Should().Be(HealthStatus.AtRisk);
        sut.Expiration.Should().Be(newExpiration);
        sut.Note.Should().Be("new");
    }

    [Fact]
    public void Update_WhenAlreadyExpired_FailsAndDoesNotMutate()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromHours(1)), "old");
        var afterExpiry = _now.Plus(Duration.FromHours(2));

        var result = sut.Update(HealthStatus.Unhealthy, afterExpiry.Plus(Duration.FromDays(7)), "new", afterExpiry);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expired health checks cannot be modified.");
        sut.Status.Should().Be(HealthStatus.Healthy);
        sut.Note.Should().Be("old");
    }

    [Fact]
    public void Update_WhenNewExpirationNotInFuture_FailsAndDoesNotMutate()
    {
        var sut = TestHealthCheck.Create(HealthStatus.Healthy, Guid.NewGuid(), _now, _now.Plus(Duration.FromDays(7)), "old");

        var result = sut.Update(HealthStatus.AtRisk, _now, "new", _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expiration must be in the future.");
        sut.Status.Should().Be(HealthStatus.Healthy);
    }
}
