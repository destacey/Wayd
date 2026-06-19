using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public sealed class PlanningIntervalObjectiveHealthCheckTests
{
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);
    private readonly PlanningIntervalObjectiveFaker _objectiveFaker;

    public PlanningIntervalObjectiveHealthCheckTests()
    {
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _objectiveFaker = new PlanningIntervalObjectiveFaker(Guid.NewGuid(), team, ObjectiveStatus.NotStarted, isStretch: false);
    }

    #region AddHealthCheck

    [Fact]
    public void AddHealthCheck_WhenExpirationInFuture_ReturnsSuccessAndAppendsToCollection()
    {
        var objective = _objectiveFaker.Generate();
        var reportedById = Guid.NewGuid();
        var expiration = _now.Plus(Duration.FromDays(7));

        var result = objective.AddHealthCheck(HealthStatus.Healthy, reportedById, expiration, "Looking good", _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HealthStatus.Healthy);
        result.Value.ReportedById.Should().Be(reportedById);
        result.Value.ReportedOn.Should().Be(_now);
        result.Value.Expiration.Should().Be(expiration);
        result.Value.Note.Should().Be("Looking good");
        result.Value.PlanningIntervalObjectiveId.Should().Be(objective.Id);

        objective.HealthChecks.Should().ContainSingle().Which.Should().Be(result.Value);
    }

    [Fact]
    public void AddHealthCheck_WhenExpirationEqualsNow_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();

        var result = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now, null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expiration must be in the future.");
        objective.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenExpirationInPast_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();

        var result = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Minus(Duration.FromHours(1)), null, _now);

        result.IsFailure.Should().BeTrue();
        objective.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenLatestStillActive_TruncatesPreviousAndAppendsNew()
    {
        var objective = _objectiveFaker.Generate();

        var firstResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, _now);
        firstResult.IsSuccess.Should().BeTrue();
        var first = firstResult.Value;

        var laterNow = _now.Plus(Duration.FromDays(2));
        var secondResult = objective.AddHealthCheck(HealthStatus.AtRisk, Guid.NewGuid(), laterNow.Plus(Duration.FromDays(7)), null, laterNow);

        secondResult.IsSuccess.Should().BeTrue();
        first.Expiration.Should().Be(laterNow);
        first.IsExpired(laterNow).Should().BeTrue();

        objective.HealthChecks.Should().HaveCount(2);
        objective.HealthChecks.Count(h => !h.IsExpired(laterNow)).Should().Be(1);
    }

    [Fact]
    public void AddHealthCheck_WhenPreviousAlreadyExpired_DoesNotMutatePrevious()
    {
        var objective = _objectiveFaker.Generate();

        var firstResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromHours(1)), null, _now);
        firstResult.IsSuccess.Should().BeTrue();
        var first = firstResult.Value;
        var firstExpiration = first.Expiration;

        var afterFirstExpired = _now.Plus(Duration.FromHours(2));
        var secondResult = objective.AddHealthCheck(HealthStatus.Unhealthy, Guid.NewGuid(), afterFirstExpired.Plus(Duration.FromDays(7)), null, afterFirstExpired);

        secondResult.IsSuccess.Should().BeTrue();
        first.Expiration.Should().Be(firstExpiration);
        objective.HealthChecks.Should().HaveCount(2);
    }

    #endregion

    #region UpdateHealthCheck

    [Fact]
    public void UpdateHealthCheck_WhenHealthCheckExistsAndActive_AppliesChanges()
    {
        var objective = _objectiveFaker.Generate();
        var addResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), "old", _now);
        var hcId = addResult.Value.Id;

        var newExpiration = _now.Plus(Duration.FromDays(14));
        var updateResult = objective.UpdateHealthCheck(hcId, HealthStatus.AtRisk, newExpiration, "new", _now);

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.Status.Should().Be(HealthStatus.AtRisk);
        updateResult.Value.Expiration.Should().Be(newExpiration);
        updateResult.Value.Note.Should().Be("new");
    }

    [Fact]
    public void UpdateHealthCheck_WhenHealthCheckNotFound_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();
        var unknownId = Guid.NewGuid();

        var result = objective.UpdateHealthCheck(unknownId, HealthStatus.AtRisk, _now.Plus(Duration.FromDays(7)), null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public void UpdateHealthCheck_WhenHealthCheckExpired_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();
        var addResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromHours(1)), null, _now);

        var afterExpired = _now.Plus(Duration.FromHours(2));
        var result = objective.UpdateHealthCheck(addResult.Value.Id, HealthStatus.Unhealthy, afterExpired.Plus(Duration.FromDays(7)), "trying", afterExpired);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expired health checks cannot be modified.");
    }

    #endregion

    #region RemoveHealthCheck

    [Fact]
    public void RemoveHealthCheck_WhenHealthCheckExists_RemovesFromCollection()
    {
        var objective = _objectiveFaker.Generate();
        var addResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, _now);

        var result = objective.RemoveHealthCheck(addResult.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(addResult.Value);
        objective.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void RemoveHealthCheck_WhenHealthCheckNotFound_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();
        var unknownId = Guid.NewGuid();

        var result = objective.RemoveHealthCheck(unknownId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public void RemoveHealthCheck_WhenMultipleExist_OnlyRemovesTheTarget()
    {
        var objective = _objectiveFaker.Generate();
        var first = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, _now).Value;
        var laterNow = _now.Plus(Duration.FromDays(2));
        var second = objective.AddHealthCheck(HealthStatus.AtRisk, Guid.NewGuid(), laterNow.Plus(Duration.FromDays(7)), null, laterNow).Value;

        var result = objective.RemoveHealthCheck(second.Id);

        result.IsSuccess.Should().BeTrue();
        objective.HealthChecks.Should().ContainSingle().Which.Should().Be(first);
    }

    #endregion
}
