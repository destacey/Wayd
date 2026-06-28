using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class SearchWorkItemsQueryTests
{
    [Fact]
    public async Task Handle_WhenSearchTermIsEmpty_ReturnsFailure()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var handler = new SearchWorkItemsQueryHandler(context, NullLogger<SearchWorkItemsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new SearchWorkItemsQuery("   ", 50), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_MatchesOnTitle()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var match = CreateWorkItem(1, "Improve search performance");
        var nonMatch = CreateWorkItem(2, "Unrelated work");
        context.AddWorkItems([match, nonMatch]);
        var handler = new SearchWorkItemsQueryHandler(context, NullLogger<SearchWorkItemsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new SearchWorkItemsQuery("search", 50), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(match.Id, result.Value.First().Id);
    }

    [Fact]
    public async Task Handle_MatchesOnKey()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var match = CreateWorkItem(42, "Some title");
        var nonMatch = CreateWorkItem(7, "Another title");
        context.AddWorkItems([match, nonMatch]);
        var handler = new SearchWorkItemsQueryHandler(context, NullLogger<SearchWorkItemsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new SearchWorkItemsQuery(((string)match.Key), 50), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(match.Id, result.Value.First().Id);
    }

    [Fact]
    public async Task Handle_RespectsTopLimit()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        context.AddWorkItems([
            CreateWorkItem(1, "match one"),
            CreateWorkItem(2, "match two"),
            CreateWorkItem(3, "match three"),
        ]);
        var handler = new SearchWorkItemsQueryHandler(context, NullLogger<SearchWorkItemsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new SearchWorkItemsQuery("match", 2), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
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
