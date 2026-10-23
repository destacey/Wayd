using FluentAssertions;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class WorkItemExistsInWorkspaceQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly WorkItemExistsInWorkspaceQueryHandler _handler;

    public WorkItemExistsInWorkspaceQueryHandlerTests()
    {
        _handler = new WorkItemExistsInWorkspaceQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsTrue_WhenTheWorkItemIsInTheWorkspace()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workItem = new WorkItemFaker().WithWorkspaceId(workspaceId).Generate();
        _dbContext.AddWorkItem(workItem);

        // Act
        var exists = await _handler.Handle(new WorkItemExistsInWorkspaceQuery(workspaceId, workItem.Id), TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheWorkItemIsInAnotherWorkspace()
    {
        // Arrange
        var workItem = new WorkItemFaker().WithWorkspaceId(Guid.NewGuid()).Generate();
        _dbContext.AddWorkItem(workItem);

        // Act
        var exists = await _handler.Handle(new WorkItemExistsInWorkspaceQuery(Guid.NewGuid(), workItem.Id), TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoWorkItemHasTheId()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _dbContext.AddWorkItem(new WorkItemFaker().WithWorkspaceId(workspaceId).Generate());

        // Act
        var exists = await _handler.Handle(new WorkItemExistsInWorkspaceQuery(workspaceId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeFalse();
    }
}
