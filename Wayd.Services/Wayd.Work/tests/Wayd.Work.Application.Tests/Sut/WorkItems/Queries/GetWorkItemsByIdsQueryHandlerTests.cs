using FluentAssertions;
using Wayd.Common.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetWorkItemsByIdsQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetWorkItemsByIdsQueryHandler _handler;

    public GetWorkItemsByIdsQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _handler = new GetWorkItemsByIdsQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsOnlyTheRequestedWorkItems_WithKeyTitleAndStatus()
    {
        // Arrange
        var first = CreateWorkItem(1, "Export screen");
        var second = CreateWorkItem(2, "Import screen");
        var other = CreateWorkItem(3, "Unrelated");
        _dbContext.AddWorkItems([first, second, other]);

        // Act
        var result = await _handler.Handle(new GetWorkItemsByIdsQuery([first.Id, second.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Select(w => w.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        var found = result.Single(w => w.Id == first.Id);
        found.Key.Should().Be("TEST-1");
        found.Title.Should().Be("Export screen");
        found.WorkspaceKey.Should().Be("TEST");
        found.Status.Should().Be(first.Status.Name);
        found.StatusCategory.Id.Should().Be((int)first.StatusCategory);
    }

    [Fact]
    public async Task Handle_LeavesOutAnIdWithNoWorkItem()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Export screen");
        _dbContext.AddWorkItem(workItem);

        // Act
        var result = await _handler.Handle(new GetWorkItemsByIdsQuery([workItem.Id, Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(workItem.Id);
    }

    [Fact]
    public async Task Handle_WithNoIds_ReturnsNothing()
    {
        // Arrange
        _dbContext.AddWorkItem(CreateWorkItem(1, "Export screen"));

        // Act
        var result = await _handler.Handle(new GetWorkItemsByIdsQuery([]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    private static WorkItem CreateWorkItem(int externalId, string title)
    {
        var workspace = new WorkspaceFaker()
            .AsExternal()
            .WithKey(new WorkspaceKey("TEST"))
            .Generate();
        var faker = new WorkItemFaker(workspace.Id)
            .WithExternalId(externalId)
            .WithTitle(title);

        faker.RuleFor(x => x.Key, new WorkItemKey(workspace.Key, externalId));
        faker.RuleFor(x => x.Workspace, workspace);

        return faker.Generate();
    }
}
