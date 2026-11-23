using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

/// <summary>
/// The handler's refusals and save handling. What EF writes when the stages are replaced is covered by the
/// integration test of the same name.
/// </summary>
public sealed class ChangeProjectLifecycleCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly ProjectLifecycleFaker _lifecycleFaker = new();

    public ChangeProjectLifecycleCommandHandlerTests()
    {
        // Authorized through the administrator grant so these tests need not arrange roles.
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private ChangeProjectLifecycleCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider, Mock.Of<ILogger<ChangeProjectLifecycleCommandHandler>>());

    /// <summary>A proposed project on a two-stage lifecycle, and the lifecycle it can change to.</summary>
    private (Project Project, ProjectLifecycle NewLifecycle) Seed(bool newLifecycleIsActive = true)
    {
        var project = new ProjectFaker().AsProposed(_dateTimeProvider);
        var current = _lifecycleFaker.AsActiveWithStages(("Plan", "Planning"), ("Execute", "Execution"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, current, _dateTimeProvider.Now);
        project.ClearDomainEvents();
        _dbContext.AddProject(project);

        var stages = new[] { ("Design", "Design"), ("Build", "Build"), ("Release", "Release") };
        var newLifecycle = newLifecycleIsActive
            ? _lifecycleFaker.AsActiveWithStages(stages)
            : _lifecycleFaker.AsProposedWithStages(stages);
        _dbContext.AddProjectLifecycle(newLifecycle);

        return (project, newLifecycle);
    }

    [Fact]
    public async Task Handle_ShouldMoveTheProjectOntoTheNewLifecycle()
    {
        // Arrange
        var (project, newLifecycle) = Seed();

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectLifecycleCommand(project.Id, newLifecycle.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.ProjectLifecycleId.Should().Be(newLifecycle.Id);
        project.Stages.Select(s => s.Name).Should().Equal("Design", "Build", "Release");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheNewLifecycleIsNotActive()
    {
        // Arrange
        var (project, newLifecycle) = Seed(newLifecycleIsActive: false);
        var currentLifecycleId = project.ProjectLifecycleId;

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectLifecycleCommand(project.Id, newLifecycle.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active lifecycles can be assigned to projects.");
        project.ProjectLifecycleId.Should().Be(currentLifecycleId);
        project.Stages.Select(s => s.Name).Should().Equal("Plan", "Execute");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProjectDoesNotExist()
    {
        // Arrange
        var (_, newLifecycle) = Seed();
        var projectId = Guid.NewGuid();

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectLifecycleCommand(projectId, newLifecycle.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Project {projectId} not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheNewLifecycleDoesNotExist()
    {
        // Arrange
        var (project, _) = Seed();
        var lifecycleId = Guid.NewGuid();

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectLifecycleCommand(project.Id, lifecycleId, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Project Lifecycle {lifecycleId} not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAStaleProject_WhenAnotherSaveChangedItFirst()
    {
        // Arrange
        var (project, newLifecycle) = Seed();
        _dbContext.SaveException = new DbUpdateConcurrencyException();

        // Act
        var result = await CreateHandler().Handle(new ChangeProjectLifecycleCommand(project.Id, newLifecycle.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProjectSaves.StaleError);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var (project, newLifecycle) = Seed();
        var currentLifecycleId = project.ProjectLifecycleId;

        // Act
        var act = () => CreateHandler().Handle(new ChangeProjectLifecycleCommand(project.Id, newLifecycle.Id, []), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        project.ProjectLifecycleId.Should().Be(currentLifecycleId);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
