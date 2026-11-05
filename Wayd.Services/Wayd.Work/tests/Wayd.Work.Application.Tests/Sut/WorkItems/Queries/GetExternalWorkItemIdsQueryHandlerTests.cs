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
            NativeWorkItem(first),
        ]);

        // Act
        var result = await _handler.Handle(new GetExternalWorkItemIdsQuery([first, second]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([11, 12]);
    }

    /// <summary>A work item created in Wayd, which has no external id.</summary>
    private static Domain.Models.WorkItem NativeWorkItem(Guid workspaceId)
    {
        var faker = new WorkItemFaker().WithWorkspaceId(workspaceId);
        faker.RuleFor(x => x.ExternalId, _ => (int?)null);
        return faker.Generate();
    }
}
