using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

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

        var result = objective.AddHealthCheck(HealthStatus.Healthy, reportedById, expiration, "Looking good", EventActor.System, _now);

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

        var result = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now, null, EventActor.System, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expiration must be in the future.");
        objective.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenExpirationInPast_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();

        var result = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Minus(Duration.FromHours(1)), null, EventActor.System, _now);

        result.IsFailure.Should().BeTrue();
        objective.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenLatestStillActive_TruncatesPreviousAndAppendsNew()
    {
        var objective = _objectiveFaker.Generate();

        var firstResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, EventActor.System, _now);
        firstResult.IsSuccess.Should().BeTrue();
        var first = firstResult.Value;

        var laterNow = _now.Plus(Duration.FromDays(2));
        var secondResult = objective.AddHealthCheck(HealthStatus.AtRisk, Guid.NewGuid(), laterNow.Plus(Duration.FromDays(7)), null, EventActor.System, laterNow);

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

        var firstResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromHours(1)), null, EventActor.System, _now);
        firstResult.IsSuccess.Should().BeTrue();
        var first = firstResult.Value;
        var firstExpiration = first.Expiration;

        var afterFirstExpired = _now.Plus(Duration.FromHours(2));
        var secondResult = objective.AddHealthCheck(HealthStatus.Unhealthy, Guid.NewGuid(), afterFirstExpired.Plus(Duration.FromDays(7)), null, EventActor.System, afterFirstExpired);

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
        var addResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), "old", EventActor.System, _now);
        var hcId = addResult.Value.Id;

        var newExpiration = _now.Plus(Duration.FromDays(14));
        var updateResult = objective.UpdateHealthCheck(hcId, HealthStatus.AtRisk, newExpiration, "new", EventActor.System, _now);

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

        var result = objective.UpdateHealthCheck(unknownId, HealthStatus.AtRisk, _now.Plus(Duration.FromDays(7)), null, EventActor.System, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public void UpdateHealthCheck_WhenHealthCheckExpired_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();
        var addResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromHours(1)), null, EventActor.System, _now);

        var afterExpired = _now.Plus(Duration.FromHours(2));
        var result = objective.UpdateHealthCheck(addResult.Value.Id, HealthStatus.Unhealthy, afterExpired.Plus(Duration.FromDays(7)), "trying", EventActor.System, afterExpired);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expired health checks cannot be modified.");
    }

    #endregion

    #region RemoveHealthCheck

    [Fact]
    public void RemoveHealthCheck_WhenHealthCheckExists_RemovesFromCollection()
    {
        var objective = _objectiveFaker.Generate();
        var addResult = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, EventActor.System, _now);

        var result = objective.RemoveHealthCheck(addResult.Value.Id, EventActor.System, _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(addResult.Value);
        objective.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void RemoveHealthCheck_WhenHealthCheckNotFound_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();
        var unknownId = Guid.NewGuid();

        var result = objective.RemoveHealthCheck(unknownId, EventActor.System, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public void RemoveHealthCheck_WhenMultipleExist_OnlyRemovesTheTarget()
    {
        var objective = _objectiveFaker.Generate();
        var first = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, EventActor.System, _now).Value;
        var laterNow = _now.Plus(Duration.FromDays(2));
        var second = objective.AddHealthCheck(HealthStatus.AtRisk, Guid.NewGuid(), laterNow.Plus(Duration.FromDays(7)), null, EventActor.System, laterNow).Value;

        var result = objective.RemoveHealthCheck(second.Id, EventActor.System, _now);

        result.IsSuccess.Should().BeTrue();
        objective.HealthChecks.Should().ContainSingle().Which.Should().Be(first);
    }

    #endregion

    #region Events

    [Fact]
    public void AddHealthCheck_RaisesHealthCheckAddedFromTheStoredCheck()
    {
        // Arrange
        var objective = _objectiveFaker.Generate();
        var reportedById = Guid.NewGuid();
        var expiration = _now.Plus(Duration.FromDays(7));

        // Act
        var added = objective.AddHealthCheck(HealthStatus.AtRisk, reportedById, expiration, "  Vendor slipped ", EventActor.System, _now).Value;

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveHealthCheckAddedEvent>().Subject;
        raised.Id.Should().Be(objective.Id);
        raised.Key.Should().Be(objective.Key);
        raised.HealthCheckId.Should().Be(added.Id);
        raised.Status.Should().Be(HealthStatus.AtRisk);
        raised.Note.Should().Be("Vendor slipped");
        raised.Expiration.Should().Be(expiration);
        raised.ReportedById.Should().Be(reportedById);
    }

    [Fact]
    public void UpdateHealthCheck_Corrected_RaisesHealthCheckUpdatedWithBothEnds()
    {
        // Arrange
        var objective = _objectiveFaker.Generate();
        var originalExpiration = _now.Plus(Duration.FromDays(7));
        var added = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), originalExpiration, "old", EventActor.System, _now).Value;
        objective.ClearDomainEvents();
        var newExpiration = _now.Plus(Duration.FromDays(14));

        // Act
        objective.UpdateHealthCheck(added.Id, HealthStatus.AtRisk, newExpiration, " new ", EventActor.System, _now);

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveHealthCheckUpdatedEvent>().Subject;
        raised.HealthCheckId.Should().Be(added.Id);
        raised.PreviousStatus.Should().Be(HealthStatus.Healthy);
        raised.PreviousNote.Should().Be("old");
        raised.PreviousExpiration.Should().Be(originalExpiration);
        raised.Status.Should().Be(HealthStatus.AtRisk);
        raised.Note.Should().Be("new");
        raised.Expiration.Should().Be(newExpiration);
    }

    [Fact]
    public void UpdateHealthCheck_NothingChanged_RaisesNothing()
    {
        // Arrange
        var objective = _objectiveFaker.Generate();
        var expiration = _now.Plus(Duration.FromDays(7));
        var added = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), expiration, "steady", EventActor.System, _now).Value;
        objective.ClearDomainEvents();

        // Act
        objective.UpdateHealthCheck(added.Id, HealthStatus.Healthy, expiration, "steady ", EventActor.System, _now);

        // Assert
        objective.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveHealthCheck_RaisesHealthCheckRemovedWithTheStatusItCarried()
    {
        // Arrange
        var objective = _objectiveFaker.Generate();
        var added = objective.AddHealthCheck(HealthStatus.Unhealthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, EventActor.System, _now).Value;
        objective.ClearDomainEvents();

        // Act
        objective.RemoveHealthCheck(added.Id, EventActor.System, _now);

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveHealthCheckRemovedEvent>().Subject;
        raised.HealthCheckId.Should().Be(added.Id);
        raised.Status.Should().Be(HealthStatus.Unhealthy);
    }

    #endregion
}
