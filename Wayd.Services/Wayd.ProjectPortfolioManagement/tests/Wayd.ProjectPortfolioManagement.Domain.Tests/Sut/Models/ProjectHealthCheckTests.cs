using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class ProjectHealthCheckTests
{
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly ProjectFaker _projectFaker = new();

    private (Project Project, Guid ActorId) ProjectWithOwner()
    {
        var actorId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Owner] = [actorId] })
            .Generate();
        return (project, actorId);
    }

    #region AddHealthCheck

    [Fact]
    public void AddHealthCheck_WhenAuthorizedAndExpirationInFuture_ReturnsSuccessAndAppendsToCollection()
    {
        var (project, actorId) = ProjectWithOwner();
        var expiration = _now.Plus(Duration.FromDays(7));

        var result = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, expiration, "On track", _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HealthStatus.Healthy);
        result.Value.ReportedById.Should().Be(actorId);
        result.Value.ReportedOn.Should().Be(_now);
        result.Value.Expiration.Should().Be(expiration);
        result.Value.Note.Should().Be("On track");
        result.Value.ProjectId.Should().Be(project.Id);

        project.HealthChecks.Should().ContainSingle().Which.Should().Be(result.Value);
    }

    [Fact]
    public void AddHealthCheck_WhenActorNotAuthorized_ReturnsFailureAndDoesNotAppend()
    {
        var project = _projectFaker.Generate();
        var unauthorizedActor = Guid.NewGuid();

        var result = project.AddHealthCheck(HealthStatus.Healthy, unauthorizedActor, [], null, _now.Plus(Duration.FromDays(7)), "trying", _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenExpirationEqualsNow_ReturnsFailure()
    {
        var (project, actorId) = ProjectWithOwner();

        var result = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now, null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expiration must be in the future.");
        project.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenExpirationInPast_ReturnsFailure()
    {
        var (project, actorId) = ProjectWithOwner();

        var result = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Minus(Duration.FromHours(1)), null, _now);

        result.IsFailure.Should().BeTrue();
        project.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void AddHealthCheck_WhenLatestStillActive_TruncatesPreviousAndAppendsNew()
    {
        var (project, actorId) = ProjectWithOwner();

        var firstResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromDays(7)), null, _now);
        firstResult.IsSuccess.Should().BeTrue();
        var first = firstResult.Value;

        var laterNow = _now.Plus(Duration.FromDays(2));
        var secondResult = project.AddHealthCheck(HealthStatus.AtRisk, actorId, [], null, laterNow.Plus(Duration.FromDays(7)), null, laterNow);

        secondResult.IsSuccess.Should().BeTrue();
        first.Expiration.Should().Be(laterNow);
        first.IsExpired(laterNow).Should().BeTrue();

        project.HealthChecks.Should().HaveCount(2);
        project.HealthChecks.Count(h => !h.IsExpired(laterNow)).Should().Be(1);
    }

    [Fact]
    public void AddHealthCheck_WhenPreviousAlreadyExpired_DoesNotMutatePrevious()
    {
        var (project, actorId) = ProjectWithOwner();

        var firstResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromHours(1)), null, _now);
        firstResult.IsSuccess.Should().BeTrue();
        var first = firstResult.Value;
        var firstExpiration = first.Expiration;

        var afterFirstExpired = _now.Plus(Duration.FromHours(2));
        var secondResult = project.AddHealthCheck(HealthStatus.Unhealthy, actorId, [], null, afterFirstExpired.Plus(Duration.FromDays(7)), null, afterFirstExpired);

        secondResult.IsSuccess.Should().BeTrue();
        first.Expiration.Should().Be(firstExpiration);
        project.HealthChecks.Should().HaveCount(2);
    }

    #endregion

    #region UpdateHealthCheck

    [Fact]
    public void UpdateHealthCheck_WhenAuthorizedAndHealthCheckExistsAndActive_AppliesChanges()
    {
        var (project, actorId) = ProjectWithOwner();
        var addResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromDays(7)), "old", _now);
        var hcId = addResult.Value.Id;

        var newExpiration = _now.Plus(Duration.FromDays(14));
        var updateResult = project.UpdateHealthCheck(hcId, actorId, [], null, HealthStatus.AtRisk, newExpiration, "new", _now);

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.Status.Should().Be(HealthStatus.AtRisk);
        updateResult.Value.Expiration.Should().Be(newExpiration);
        updateResult.Value.Note.Should().Be("new");
    }

    [Fact]
    public void UpdateHealthCheck_WhenActorNotAuthorized_ReturnsFailureAndDoesNotMutate()
    {
        var (project, actorId) = ProjectWithOwner();
        var addResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromDays(7)), "old", _now);
        var hcId = addResult.Value.Id;
        var originalExpiration = addResult.Value.Expiration;
        var unauthorizedActor = Guid.NewGuid();

        var result = project.UpdateHealthCheck(hcId, unauthorizedActor, [], null, HealthStatus.Unhealthy, _now.Plus(Duration.FromDays(14)), "tampered", _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        addResult.Value.Status.Should().Be(HealthStatus.Healthy);
        addResult.Value.Expiration.Should().Be(originalExpiration);
        addResult.Value.Note.Should().Be("old");
    }

    [Fact]
    public void UpdateHealthCheck_WhenHealthCheckNotFound_ReturnsFailure()
    {
        var (project, actorId) = ProjectWithOwner();
        var unknownId = Guid.NewGuid();

        var result = project.UpdateHealthCheck(unknownId, actorId, [], null, HealthStatus.AtRisk, _now.Plus(Duration.FromDays(7)), null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public void UpdateHealthCheck_WhenHealthCheckExpired_ReturnsFailure()
    {
        var (project, actorId) = ProjectWithOwner();
        var addResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromHours(1)), null, _now);

        var afterExpired = _now.Plus(Duration.FromHours(2));
        var result = project.UpdateHealthCheck(addResult.Value.Id, actorId, [], null, HealthStatus.Unhealthy, afterExpired.Plus(Duration.FromDays(7)), "trying", afterExpired);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expired health checks cannot be modified.");
    }

    #endregion

    #region RemoveHealthCheck

    [Fact]
    public void RemoveHealthCheck_WhenAuthorizedAndHealthCheckExists_RemovesFromCollection()
    {
        var (project, actorId) = ProjectWithOwner();
        var addResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromDays(7)), null, _now);

        var result = project.RemoveHealthCheck(addResult.Value.Id, actorId, [], null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(addResult.Value);
        project.HealthChecks.Should().BeEmpty();
    }

    [Fact]
    public void RemoveHealthCheck_WhenActorNotAuthorized_ReturnsFailureAndDoesNotRemove()
    {
        var (project, actorId) = ProjectWithOwner();
        var addResult = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromDays(7)), null, _now);
        var unauthorizedActor = Guid.NewGuid();

        var result = project.RemoveHealthCheck(addResult.Value.Id, unauthorizedActor, [], null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.HealthChecks.Should().ContainSingle();
    }

    [Fact]
    public void RemoveHealthCheck_WhenHealthCheckNotFound_ReturnsFailure()
    {
        var (project, actorId) = ProjectWithOwner();
        var unknownId = Guid.NewGuid();

        var result = project.RemoveHealthCheck(unknownId, actorId, [], null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public void RemoveHealthCheck_WhenMultipleExist_OnlyRemovesTheTarget()
    {
        var (project, actorId) = ProjectWithOwner();
        var first = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], null, _now.Plus(Duration.FromDays(7)), null, _now).Value;
        var laterNow = _now.Plus(Duration.FromDays(2));
        var second = project.AddHealthCheck(HealthStatus.AtRisk, actorId, [], null, laterNow.Plus(Duration.FromDays(7)), null, laterNow).Value;

        var result = project.RemoveHealthCheck(second.Id, actorId, [], null);

        result.IsSuccess.Should().BeTrue();
        project.HealthChecks.Should().ContainSingle().Which.Should().Be(first);
    }

    [Fact]
    public void AddHealthCheck_WhenAuthorizedViaPortfolioOwner_Succeeds()
    {
        var actorId = Guid.NewGuid();
        var project = _projectFaker.Generate();
        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Owner, actorId),
        };

        var result = project.AddHealthCheck(HealthStatus.Healthy, actorId, portfolioRoles, null, _now.Plus(Duration.FromDays(7)), null, _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportedById.Should().Be(actorId);
    }

    [Fact]
    public void AddHealthCheck_WhenAuthorizedViaProgramManager_Succeeds()
    {
        var actorId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithData(programId: programId).Generate();
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Manager, actorId),
        };

        var result = project.AddHealthCheck(HealthStatus.Healthy, actorId, [], programRoles, _now.Plus(Duration.FromDays(7)), null, _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportedById.Should().Be(actorId);
    }

    #endregion

    #region CanManageProject

    [Fact]
    public void CanManageProject_WhenProjectOwner_ReturnsTrue()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Owner] = [employeeId] })
            .Generate();

        var result = project.CanManageProject(employeeId, [], null);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_WhenProjectManager_ReturnsTrue()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Manager] = [employeeId] })
            .Generate();

        var result = project.CanManageProject(employeeId, [], null);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_WhenProjectSponsor_ReturnsFalse()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Sponsor] = [employeeId] })
            .Generate();

        var result = project.CanManageProject(employeeId, [], null);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanManageProject_WhenProjectMember_ReturnsFalse()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(roles: new() { [ProjectRole.Member] = [employeeId] })
            .Generate();

        var result = project.CanManageProject(employeeId, [], null);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanManageProject_WhenPortfolioOwner_ReturnsTrue()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();
        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Owner, employeeId),
        };

        var result = project.CanManageProject(employeeId, portfolioRoles, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_WhenPortfolioManager_ReturnsTrue()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();
        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Manager, employeeId),
        };

        var result = project.CanManageProject(employeeId, portfolioRoles, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_WhenPortfolioSponsor_ReturnsFalse()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();
        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Sponsor, employeeId),
        };

        var result = project.CanManageProject(employeeId, portfolioRoles, null);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanManageProject_WhenProgramOwner_ReturnsTrue()
    {
        var employeeId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithData(programId: programId).Generate();
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Owner, employeeId),
        };

        var result = project.CanManageProject(employeeId, [], programRoles);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_WhenProgramManager_ReturnsTrue()
    {
        var employeeId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithData(programId: programId).Generate();
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Manager, employeeId),
        };

        var result = project.CanManageProject(employeeId, [], programRoles);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanManageProject_WhenProgramSponsor_ReturnsFalse()
    {
        var employeeId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker.WithData(programId: programId).Generate();
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Sponsor, employeeId),
        };

        var result = project.CanManageProject(employeeId, [], programRoles);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanManageProject_WhenNoRoleAnywhere_ReturnsFalse()
    {
        var employeeId = Guid.NewGuid();
        var project = _projectFaker.Generate();

        var result = project.CanManageProject(employeeId, [], null);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanManageProject_WhenDifferentEmployeeOnAllLevels_ReturnsFalse()
    {
        var requestingEmployeeId = Guid.NewGuid();
        var ownerEmployeeId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var project = _projectFaker
            .WithData(
                programId: programId,
                roles: new() { [ProjectRole.Owner] = [ownerEmployeeId] })
            .Generate();

        var portfolioRoles = new[]
        {
            new RoleAssignment<ProjectPortfolioRole>(project.PortfolioId, ProjectPortfolioRole.Owner, ownerEmployeeId),
        };
        var programRoles = new[]
        {
            new RoleAssignment<ProgramRole>(programId, ProgramRole.Manager, ownerEmployeeId),
        };

        var result = project.CanManageProject(requestingEmployeeId, portfolioRoles, programRoles);

        result.Should().BeFalse();
    }

    #endregion
}
