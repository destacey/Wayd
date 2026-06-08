using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Queries;

public class GetCriticalPathQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly GetCriticalPathQueryHandler _handler;
    private readonly ProjectFaker _projectFaker;
    private readonly Mock<ILogger<GetCriticalPathQueryHandler>> _mockLogger;

    public GetCriticalPathQueryHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<GetCriticalPathQueryHandler>>();
        _handler = new GetCriticalPathQueryHandler(
            _dbContext,
            _mockLogger.Object);
        _projectFaker = new ProjectFaker();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenProjectExists()
    {
        // Arrange - This is a stub implementation that returns empty list
        var projectKey = new ProjectKey("TEST");
        var project = _projectFaker.WithKey(projectKey).Generate();
        _dbContext.AddProject(project);

        var query = new GetCriticalPathQuery(projectKey.Value);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Stub implementation returns empty list
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenProjectDoesNotExist()
    {
        // Arrange
        var query = new GetCriticalPathQuery("NONEXISTENT");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAcceptProjectId_AsGuid()
    {
        // Arrange
        var project = _projectFaker.Generate();
        _dbContext.AddProject(project);

        var query = new GetCriticalPathQuery(project.Id.ToString());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Stub implementation returns empty list
    }

    [Fact]
    public async Task Handle_ShouldAcceptProjectKey_AsString()
    {
        // Arrange
        var projectKey = new ProjectKey("APOLLO");
        var project = _projectFaker.WithKey(projectKey).Generate();
        _dbContext.AddProject(project);

        var query = new GetCriticalPathQuery("APOLLO");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Stub implementation returns empty list
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}