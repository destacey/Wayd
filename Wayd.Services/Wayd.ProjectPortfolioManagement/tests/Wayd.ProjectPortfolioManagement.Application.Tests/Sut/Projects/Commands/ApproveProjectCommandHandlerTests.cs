using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Commands;

public class ApproveProjectCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ApproveProjectCommandHandler _handler;
    private readonly Mock<ILogger<ApproveProjectCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ProjectFaker _projectFaker;

    public ApproveProjectCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ApproveProjectCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ApproveProjectCommandHandler(_dbContext, _mockLogger.Object);

        _projectFaker = new ProjectFaker();
    }

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
        project.AssignLifecycle(lifecycle);
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
    public async Task Handle_ShouldFail_WhenProjectIsCancelled()
    {
        // Arrange
        var project = _projectFaker.AsCancelled(_dateTimeProvider, Guid.NewGuid());
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
