using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
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

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class RevertProjectStatusCommandHandlerTests : IDisposable
{
    private const string ARevertReason = "Closed in error; the remaining scope is being finished.";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly RevertProjectStatusCommandHandler _handler;
    private readonly Mock<ILogger<RevertProjectStatusCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal;
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Guid _actorEmployeeId = Guid.NewGuid();
    private readonly string _actorUserId = Guid.NewGuid().ToString();

    private readonly ProjectFaker _projectFaker;

    public RevertProjectStatusCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<RevertProjectStatusCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        // Authorized by default via the PPM administrator grant, so tests about revert mechanics do not
        // each have to arrange membership. Authorization-specific tests override these setups.
        _mockCurrentPrincipal = new Mock<ICurrentPrincipal>();
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_actorUserId);

        _handler = new RevertProjectStatusCommandHandler(_dbContext, _mockCurrentPrincipal.Object,
            _mockCurrentUser.Object, _dateTimeProvider, _mockLogger.Object);

        _projectFaker = new ProjectFaker();
    }

    #region Authorization

    // As with the forward transition handlers, these tests pin that the handler resolves a PpmActor and
    // forwards the project's ancestor roles into the domain — but they CANNOT pin that the query loads
    // them. The fake DbContext is backed by in-memory lists, so .Include is a no-op and these tests set
    // the Portfolio navigation directly. Dropping the .Include chain from the handler leaves every test
    // here green while denying a legitimate portfolio owner against a real database.

    [Fact]
    public async Task Handle_ShouldFail_WhenActorHoldsNoRoleAndIsNotPpmAdministrator()
    {
        // Arrange
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Active, ARevertReason);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRevert_WhenActorsOnlyRoleIsOnTheParentPortfolio()
    {
        // Arrange — the actor holds NO role on the project itself; their only path to authorization is the
        // parent portfolio.
        _mockCurrentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var portfolio = new ProjectPortfolioFaker()
            .WithStatus(ProjectPortfolioStatus.Active)
            .WithRoles(new() { [ProjectPortfolioRole.Owner] = [_actorEmployeeId] })
            .Generate();
        var project = _projectFaker
            .WithStatus(ProjectStatus.Completed)
            .WithDateRange(ADeliveredDateRange())
            .WithPortfolioId(portfolio.Id)
            .WithRoles(null)
            .Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Active, ARevertReason);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Active, ARevertReason);

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
        var command = new RevertProjectStatusCommand(Guid.NewGuid(), ProjectStatus.Active, ARevertReason);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRevertACompletedProject_ToActive()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Active, ARevertReason);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheReason_OnTheStatusHistory()
    {
        // Arrange
        var project = _projectFaker.AsCanceled(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Active, "Funding was restored");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = project.StatusHistory.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(ProjectStatus.Canceled);
        entry.ToStatus.Should().Be(ProjectStatus.Active);
        entry.Reason.Should().Be("Funding was restored");
        entry.ChangedByUserId.Should().Be(_actorUserId);
        entry.ChangedByEmployeeId.Should().Be(_actorEmployeeId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTransitionIsNotBackward()
    {
        // Arrange
        var project = _projectFaker.AsActive(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Completed, ARevertReason);

        // Act — the handler calls Entry().ReloadAsync() on failure, which the fake context does not
        // implement; the catch-all converts it to a failure result either way.
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNoReasonIsGiven()
    {
        // Arrange — the aggregate enforces this too, so a caller that bypasses the validator is still
        // rejected.
        var project = _projectFaker.AsCompleted(_dateTimeProvider, Guid.NewGuid());
        _dbContext.AddProject(project);

        var command = new RevertProjectStatusCommand(project.Id, ProjectStatus.Active, "   ");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private LocalDateRange ADeliveredDateRange()
    {
        var start = _dateTimeProvider.Today.PlusDays(-20);

        return new LocalDateRange(start, start.PlusMonths(2));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
