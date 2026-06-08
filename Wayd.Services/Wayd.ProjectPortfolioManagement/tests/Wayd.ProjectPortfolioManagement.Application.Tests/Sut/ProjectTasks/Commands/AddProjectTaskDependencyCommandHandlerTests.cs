using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Commands;

public class AddProjectTaskDependencyCommandHandlerTests : IDisposable
{
    private readonly ProjectTaskFaker _taskFaker;
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly AddProjectTaskDependencyCommandHandler _handler;
    private readonly Mock<ILogger<AddProjectTaskDependencyCommandHandler>> _mockLogger;

    public AddProjectTaskDependencyCommandHandlerTests()
    {
        _taskFaker = new ProjectTaskFaker();

        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<AddProjectTaskDependencyCommandHandler>>();

        _handler = new AddProjectTaskDependencyCommandHandler(
            _dbContext,
            _mockLogger.Object);

    }

    [Fact]
    public async Task Handle_ShouldAddDependency_WhenBothTasksExist()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var predecessor = _taskFaker.WithProjectId(projectId).WithName("Predecessor Task").Generate();
        var successor = _taskFaker.WithProjectId(projectId).WithName("Successor Task").Generate();

        _dbContext.AddProjectTasks([predecessor, successor]);

        var command = new AddProjectTaskDependencyCommand(projectId, predecessor.Id, successor.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        predecessor.Successors.Should().HaveCount(1);
        predecessor.Successors.First().SuccessorId.Should().Be(successor.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPredecessorDoesNotExist()
    {
        // Arrange
        var successor = _taskFaker.Generate();
        _dbContext.AddProjectTask(successor);

        var nonExistentPredecessorId = Guid.NewGuid();
        var command = new AddProjectTaskDependencyCommand(successor.ProjectId, nonExistentPredecessorId, successor.Id);

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
        var command = new AddProjectTaskDependencyCommand(predecessor.ProjectId, predecessor.Id, nonExistentSuccessorId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Successor task");
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTasksAreInDifferentProjects()
    {
        // Arrange
        var project1Id = Guid.NewGuid();
        var project2Id = Guid.NewGuid();

        var predecessor = _taskFaker.WithProjectId(project1Id).Generate();
        var successor = _taskFaker.WithProjectId(project2Id).Generate();

        _dbContext.AddProjectTasks([predecessor, successor]);

        var command = new AddProjectTaskDependencyCommand(project1Id, predecessor.Id, successor.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Successor task not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact(Skip = "Circular dependency detection not yet implemented - see ProjectTask.AddDependency TODO comment")]
    public async Task Handle_ShouldFail_WhenDependencyWouldCreateCircularReference()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var task1 = _taskFaker.WithProjectId(projectId).WithName("Task 1").Generate();
        var task2 = _taskFaker.WithProjectId(projectId).WithName("Task 2").Generate();

        _dbContext.AddProjectTasks([task1, task2]);

        // First, create dependency: task1 -> task2
        task1.AddDependency(task2);

        var command = new AddProjectTaskDependencyCommand(projectId, task2.Id, task1.Id);

        // Act - Try to create reverse dependency: task2 -> task1 (creates circular reference)
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("circular");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDependsOnItself()
    {
        // Arrange
        var task = _taskFaker.Generate();
        _dbContext.AddProjectTask(task);

        var command = new AddProjectTaskDependencyCommand(task.ProjectId, task.Id, task.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("itself");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDependencyAlreadyExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var predecessor = _taskFaker.WithProjectId(projectId).Generate();
        var successor = _taskFaker.WithProjectId(projectId).Generate();

        _dbContext.AddProjectTasks([predecessor, successor]);

        // Add dependency first time
        predecessor.AddDependency(successor);

        var command = new AddProjectTaskDependencyCommand(projectId, predecessor.Id, successor.Id);

        // Act - Try to add same dependency again
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}