using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;

using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class ApproveProjectCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ApproveProjectCommandHandler _handler;
    private readonly Mock<ILogger<ApproveProjectCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal;
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Guid _actorEmployeeId = Guid.NewGuid();
    private readonly string _actorUserId = Guid.NewGuid().ToString();

    private readonly ProjectFaker _projectFaker;

    public ApproveProjectCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ApproveProjectCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        // Authorized by default via the PPM administrator grant, so tests about approval mechanics do not
        // each have to arrange membership. Authorization-specific tests override these setups.
        _mockCurrentPrincipal = new Mock<ICurrentPrincipal>();
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_actorUserId);

        _handler = new ApproveProjectCommandHandler(_dbContext, _mockCurrentPrincipal.Object,
            _mockCurrentUser.Object, _dateTimeProvider, _mockLogger.Object);

        _projectFaker = new ProjectFaker();
    }

    #region Authorization

    // The handler resolves a PpmActor from the current principal and passes the project's ancestor roles
    // into the domain. These tests pin that an unauthorized actor is rejected and that the handler forwards
    // ancestor roles it has been given.
    //
    // What they CANNOT pin: that the handler's query actually loads those ancestor roles. The fake
    // DbContext is backed by in-memory lists, so .Include is a no-op and these tests populate the Portfolio
    // navigation directly. Dropping .Include(p => p.Portfolio).ThenInclude(p => p!.Roles) from the handler
    // leaves every test here green, but against a real database it would make Portfolio null, empty the
    // ancestry, and silently deny a legitimate portfolio owner. Only an integration test against a real
    // database can catch that.

    [Fact]
    public async Task Handle_ShouldFail_WhenActorHoldsNoRoleAndIsNotPpmAdministrator()
    {
        // Arrange
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var project = _projectFaker.AsProposed(_dateTimeProvider, Guid.NewGuid());
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, new ProjectLifecycleFaker().AsActiveWithPhases(("Plan", "Planning")));
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldApproveProject_WhenActorIsProjectOwnerWithoutAdministratorGrant()
    {
        // Arrange
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var portfolio = new ProjectPortfolioFaker().Generate();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Proposed)
            .WithPortfolioId(portfolio.Id)
            .WithRoles(new() { [ProjectRole.Owner] = [_actorEmployeeId] })
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, new ProjectLifecycleFaker().AsActiveWithPhases(("Plan", "Planning")));
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Approved);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldApproveProject_WhenActorsOnlyRoleIsOnTheParentPortfolio()
    {
        // Arrange — the actor holds NO role on the project itself; their only path to authorization is the
        // parent portfolio. This proves the handler forwards ancestor roles to the domain rather than
        // checking project roles alone. It does NOT prove the query loads them — see the region comment.
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var portfolio = new ProjectPortfolioFaker()
            .WithRoles(new() { [ProjectPortfolioRole.Owner] = [_actorEmployeeId] })
            .Generate();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Proposed)
            .WithPortfolioId(portfolio.Id)
            .WithRoles(null)
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, new ProjectLifecycleFaker().AsActiveWithPhases(("Plan", "Planning")));
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Approved);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var project = _projectFaker.AsProposed(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — the handler's catch-all converts the thrown guard into a failure result.
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    #endregion Authorization

    [Fact]
    public async Task Handle_ShouldFail_WhenProjectDoesNotExist()
    {
        // Arrange
        var command = new ApproveProjectCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldApproveProject_WhenProjectIsProposed()
    {
        // Arrange
        var project = _projectFaker.AsProposed(_dateTimeProvider, Guid.NewGuid());
        var lifecycle = new ProjectLifecycleFaker().AsActiveWithPhases(("Plan", "Planning"), ("Execute", "Execution"));
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Approved);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenProjectIsActive()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act & Assert - the handler calls Entry().ReloadAsync() on failure which throws in fake context
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // The handler catches the NotImplementedException from Entry() and returns a generic error
        var result = await act();
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenProjectIsCompleted()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - handler catches NotImplementedException from Entry() and returns generic error
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenProjectIsCanceled()
    {
        // Arrange
        var project = _projectFaker.AsCanceled(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - handler catches NotImplementedException from Entry() and returns generic error
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenProjectIsAlreadyApproved()
    {
        // Arrange
        var project = _projectFaker.AsApproved(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new ApproveProjectCommand(project.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - handler catches NotImplementedException from Entry() and returns generic error
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
