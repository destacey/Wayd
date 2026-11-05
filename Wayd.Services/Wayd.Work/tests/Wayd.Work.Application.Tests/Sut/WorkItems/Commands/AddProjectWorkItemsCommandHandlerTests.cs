using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Commands;

public sealed class AddProjectWorkItemsCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly AddProjectWorkItemsCommandHandler _handler;
    private readonly WorkProject _project = new WorkProjectFaker().Generate();

    public AddProjectWorkItemsCommandHandlerTests()
    {
        _dbContext.AddWorkProject(_project);
        _handler = new AddProjectWorkItemsCommandHandler(_dbContext, NullLogger<AddProjectWorkItemsCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_LinksEachWorkItem_AndCommitsOnce()
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        var feature = new WorkItemFaker().WithType(new WorkTypeFaker().AsFeature().Generate()).Generate();
        _dbContext.AddWorkItems([epic, feature]);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [epic.Id, feature.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        epic.ProjectId.Should().Be(_project.Id);
        feature.ProjectId.Should().Be(_project.Id);
        _dbContext.UnitOfWorkCommitCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MovesAWorkItemFromAnotherProject()
    {
        // Arrange
        var otherProjectId = Guid.NewGuid();
        var epic = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsEpic().Generate())
            .WithProjectId(otherProjectId)
            .Generate();
        _dbContext.AddWorkItem(epic);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [epic.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        epic.ProjectId.Should().Be(_project.Id);
    }

    [Fact]
    public async Task Handle_PassesTheProjectToChildrenWithoutTheirOwn()
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        var story = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsStory().Generate())
            .WithParentId(epic.Id)
            .Generate();
        _dbContext.AddWorkItems([epic, story]);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [epic.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        story.ProjectId.Should().BeNull();
        story.ParentProjectId.Should().Be(_project.Id);
    }

    [Fact]
    public async Task Handle_ChangesNothing_WhenAnyWorkItemIsNotPortfolioTier()
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        var story = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsStory().Generate())
            .WithKey(new WorkItemKey(new WorkspaceKey("TEST"), 7))
            .Generate();
        _dbContext.AddWorkItems([epic, story]);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [epic.Id, story.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(story.Key.ToString());
        epic.ProjectId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenAWorkItemIsMissing()
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        _dbContext.AddWorkItem(epic);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [epic.Id, Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more work items were not found.");
        epic.ProjectId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Fails_WhenTheProjectIsMissing()
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        _dbContext.AddWorkItem(epic);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(Guid.NewGuid(), [epic.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project not found, or not yet available to Work Management.");
        epic.ProjectId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PassesTheProjectThroughEveryLevel()
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        var feature = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsFeature().Generate())
            .WithParentId(epic.Id)
            .Generate();
        var story = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsStory().Generate())
            .WithParentId(feature.Id)
            .Generate();
        _dbContext.AddWorkItems([epic, feature, story]);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [epic.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        feature.CurrentProjectId.Should().Be(_project.Id);
        story.CurrentProjectId.Should().Be(_project.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_LinksTheParent_WhenAParentAndItsChildAreBothListed(bool childFirst)
    {
        // Arrange
        var epic = new WorkItemFaker().WithType(new WorkTypeFaker().AsEpic().Generate()).Generate();
        var feature = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsFeature().Generate())
            .WithParentId(epic.Id)
            .WithProjectId(Guid.NewGuid())
            .Generate();
        _dbContext.AddWorkItems([epic, feature]);
        Guid[] ids = childFirst ? [feature.Id, epic.Id] : [epic.Id, feature.Id];

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, ids),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        epic.ProjectId.Should().Be(_project.Id);
        feature.ProjectId.Should().BeNull();
        feature.ParentProjectId.Should().Be(_project.Id);
    }

    [Fact]
    public async Task Handle_ChangesNothingBeneath_AWorkItemThatAlreadyInheritsTheProject()
    {
        // Arrange
        var feature = new WorkItemFaker()
            .WithType(new WorkTypeFaker().AsFeature().Generate())
            .WithParentId(Guid.NewGuid())
            .WithParentProjectId(_project.Id)
            .Generate();
        _dbContext.AddWorkItem(feature);

        // Act
        var result = await _handler.Handle(
            new AddProjectWorkItemsCommand(_project.Id, [feature.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        feature.ProjectId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
