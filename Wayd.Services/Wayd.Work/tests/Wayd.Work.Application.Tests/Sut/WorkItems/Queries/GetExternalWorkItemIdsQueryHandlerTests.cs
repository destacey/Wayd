using FluentAssertions;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetExternalWorkItemIdsQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetExternalWorkItemIdsQueryHandler _handler;

    public GetExternalWorkItemIdsQueryHandlerTests()
    {
        _handler = new GetExternalWorkItemIdsQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsTheExternalIdsHeldInTheGivenWorkspaces()
    {
        // Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _dbContext.AddWorkItems(
        [
            new WorkItemFaker().WithWorkspaceId(first).WithExternalId(11).Generate(),
            new WorkItemFaker().WithWorkspaceId(second).WithExternalId(12).Generate(),
            new WorkItemFaker().WithWorkspaceId(Guid.NewGuid()).WithExternalId(13).Generate(),
        ]);

        // Act
        var result = await _handler.Handle(new GetExternalWorkItemIdsQuery([first, second]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([11, 12]);
    }

    [Fact]
    public async Task Handle_WithNoWorkspaces_ReturnsNothing()
    {
        // Arrange
        _dbContext.AddWorkItem(new WorkItemFaker().WithExternalId(11).Generate());

        // Act
        var result = await _handler.Handle(new GetExternalWorkItemIdsQuery([]), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeEmpty();
    }
}
