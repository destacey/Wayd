using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Commands;

public sealed class RemoveProjectWorkItemsCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly RemoveProjectWorkItemsCommandHandler _handler;
    private readonly Guid _projectId = Guid.NewGuid();

    public RemoveProjectWorkItemsCommandHandlerTests()
    {
        _handler = new RemoveProjectWorkItemsCommandHandler(_dbContext, NullLogger<RemoveProjectWorkItemsCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_UnlinksTheWorkItem_AndItsInheritingChildren()
    {
        // Arrange
        var epic = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsEpic().Generate())
            .WithProjectId(_projectId)
            .Generate();
        var story = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsStory().Generate())
            .WithParentId(epic.Id)
            .WithParentProjectId(_projectId)
            .Generate();
        _dbContext.AddWorkItems([epic, story]);

        // Act
        var result = await _handler.Handle(
            new RemoveProjectWorkItemsCommand(_projectId, [epic.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        epic.ProjectId.Should().BeNull();
        story.ParentProjectId.Should().BeNull();
        _dbContext.UnitOfWorkCommitCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeavesTheWorkItemOnItsParentsProject()
    {
        // Arrange
        var parentProjectId = Guid.NewGuid();
        var feature = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsFeature().Generate())
            .WithParentId(Guid.NewGuid())
            .WithParentProjectId(parentProjectId)
            .WithProjectId(_projectId)
            .Generate();
        _dbContext.AddWorkItem(feature);

        // Act
        var result = await _handler.Handle(
            new RemoveProjectWorkItemsCommand(_projectId, [feature.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        feature.ProjectId.Should().BeNull();
        feature.CurrentProjectId.Should().Be(parentProjectId);
    }

    [Fact]
    public async Task Handle_ChangesNothing_WhenAWorkItemOnlyInheritsTheProject()
    {
        // Arrange
        var epic = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsEpic().Generate())
            .WithProjectId(_projectId)
            .Generate();
        var feature = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsFeature().Generate())
            .WithParentId(Guid.NewGuid())
            .WithParentProjectId(_projectId)
            .WithKey(new WorkItemKey(new WorkspaceKey("TEST"), 7))
            .Generate();
        _dbContext.AddWorkItems([epic, feature]);

        // Act
        var result = await _handler.Handle(
            new RemoveProjectWorkItemsCommand(_projectId, [epic.Id, feature.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(feature.Key.ToString());
        epic.ProjectId.Should().Be(_projectId);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenAWorkItemIsMissing()
    {
        // Act
        var result = await _handler.Handle(
            new RemoveProjectWorkItemsCommand(_projectId, [Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more work items were not found.");
    }
}
