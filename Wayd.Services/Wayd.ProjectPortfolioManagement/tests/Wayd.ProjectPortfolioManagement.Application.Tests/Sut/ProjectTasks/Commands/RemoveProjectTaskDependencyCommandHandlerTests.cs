using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Commands;

public class RemoveProjectTaskDependencyCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly RemoveProjectTaskDependencyCommandHandler _handler;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Mock<ILogger<RemoveProjectTaskDependencyCommandHandler>> _mockLogger;
    private readonly ProjectTaskFaker _taskFaker;

    public RemoveProjectTaskDependencyCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _mockLogger = new Mock<ILogger<RemoveProjectTaskDependencyCommandHandler>>();
        _handler = new RemoveProjectTaskDependencyCommandHandler(
            _dbContext,
            _dateTimeProvider,
            _mockLogger.Object);
        _taskFaker = new ProjectTaskFaker();
    }

    [Fact]
    public async Task Handle_ShouldRemoveDependency_WhenDependencyExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var predecessor = _taskFaker.WithProjectId(projectId).WithName("Predecessor Task").Generate();
        var successor = _taskFaker.WithProjectId(projectId).WithName("Successor Task").Generate();

        _dbContext.AddProjectTasks([predecessor, successor]);

        // Add dependency first
        predecessor.AddDependency(successor);

        var command = new RemoveProjectTaskDependencyCommand(projectId, predecessor.Id, successor.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        predecessor.Successors.Where(d => d.IsActive).Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPredecessorDoesNotExist()
    {
        // Arrange
        var successor = _taskFaker.Generate();
        _dbContext.AddProjectTask(successor);

        var nonExistentPredecessorId = Guid.NewGuid();
        var command = new RemoveProjectTaskDependencyCommand(successor.ProjectId, nonExistentPredecessorId, successor.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Predecessor task");
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSuccessorDoesNotExist()
    {
        // Arrange
        var predecessor = _taskFaker.Generate();
        _dbContext.AddProjectTask(predecessor);

        var nonExistentSuccessorId = Guid.NewGuid();
        var command = new RemoveProjectTaskDependencyCommand(predecessor.ProjectId, predecessor.Id, nonExistentSuccessorId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDependencyDoesNotExist()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var predecessor = _taskFaker.WithProjectId(projectId).Generate();
        var successor = _taskFaker.WithProjectId(projectId).Generate();

        _dbContext.AddProjectTasks([predecessor, successor]);

        // No dependency added
        var command = new RemoveProjectTaskDependencyCommand(projectId, predecessor.Id, successor.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldAllowRemovingMultipleDependencies()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var predecessor = _taskFaker.WithProjectId(projectId).WithName("Predecessor").Generate();
        var successor1 = _taskFaker.WithProjectId(projectId).WithName("Successor 1").Generate();
        var successor2 = _taskFaker.WithProjectId(projectId).WithName("Successor 2").Generate();

        _dbContext.AddProjectTasks([predecessor, successor1, successor2]);

        // Add multiple dependencies
        predecessor.AddDependency(successor1);
        predecessor.AddDependency(successor2);

        // Act - Remove first dependency
        var result1 = await _handler.Handle(new RemoveProjectTaskDependencyCommand(projectId, predecessor.Id, successor1.Id), TestContext.Current.CancellationToken);
        // Remove second dependency
        var result2 = await _handler.Handle(new RemoveProjectTaskDependencyCommand(projectId, predecessor.Id, successor2.Id), TestContext.Current.CancellationToken);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        predecessor.Successors.Where(d => d.IsActive).Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}