using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkItemTagsTests
{
    private readonly WorkspaceFaker _workspaceFaker = new();
    private readonly WorkTypeFaker _workTypeFaker = new();
    private readonly WorkItemFaker _workItemFaker = new();

    private static readonly Instant _now = Instant.FromUtc(2024, 1, 15, 12, 0, 0);

    #region WorkItemTag

    [Fact]
    public void WorkItemTag_TrimsWhitespace()
    {
        var tag = new WorkItemTag("  Backend  ");

        tag.Value.Should().Be("Backend");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WorkItemTag_ThrowsOnNullOrWhiteSpace(string? value)
    {
        var act = () => new WorkItemTag(value!);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateExternal

    [Fact]
    public void CreateExternal_WithTags_PopulatesTagsCollection()
    {
        // Arrange
        var workspace = _workspaceFaker.AsExternal().Generate();
        var workType = _workTypeFaker.AsStory().Generate();
        var tags = new List<WorkItemTag> { new("Backend"), new("Q2"), new("Performance") };

        // Act
        var workItem = WorkItem.CreateExternal(
            workspace: workspace,
            externalId: 1,
            title: "Title",
            workType: workType,
            statusId: 1,
            statusCategory: WorkStatusCategory.Proposed,
            parentInfo: null,
            teamId: null,
            created: _now,
            createdById: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            externalTeamIdentifier: null,
            tags: tags);

        // Assert
        workItem.Tags.Should().HaveCount(3);
        workItem.Tags.Select(t => t.Value).Should().BeEquivalentTo("Backend", "Q2", "Performance");
    }

    [Fact]
    public void CreateExternal_WithNullTags_HasEmptyTagsCollection()
    {
        // Arrange
        var workspace = _workspaceFaker.AsExternal().Generate();
        var workType = _workTypeFaker.AsStory().Generate();

        // Act
        var workItem = WorkItem.CreateExternal(
            workspace: workspace,
            externalId: 1,
            title: "Title",
            workType: workType,
            statusId: 1,
            statusCategory: WorkStatusCategory.Proposed,
            parentInfo: null,
            teamId: null,
            created: _now,
            createdById: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            externalTeamIdentifier: null,
            tags: null);

        // Assert
        workItem.Tags.Should().BeEmpty();
    }

    [Fact]
    public void CreateExternal_WithEmptyTags_HasEmptyTagsCollection()
    {
        // Arrange
        var workspace = _workspaceFaker.AsExternal().Generate();
        var workType = _workTypeFaker.AsStory().Generate();

        // Act
        var workItem = WorkItem.CreateExternal(
            workspace: workspace,
            externalId: 1,
            title: "Title",
            workType: workType,
            statusId: 1,
            statusCategory: WorkStatusCategory.Proposed,
            parentInfo: null,
            teamId: null,
            created: _now,
            createdById: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            externalTeamIdentifier: null,
            tags: []);

        // Assert
        workItem.Tags.Should().BeEmpty();
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithTags_ReplacesTags()
    {
        // Arrange
        var workItem = _workItemFaker.WithTags("OldTag").Generate();
        var newTags = new List<WorkItemTag> { new("NewTag1"), new("NewTag2") };

        // Act
        workItem.Update(
            title: workItem.Title,
            workType: workItem.Type,
            statusId: workItem.Status.Id,
            statusCategory: workItem.StatusCategory,
            parentInfo: null,
            teamId: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            extendedProps: null,
            tags: newTags);

        // Assert
        workItem.Tags.Should().HaveCount(2);
        workItem.Tags.Select(t => t.Value).Should().BeEquivalentTo("NewTag1", "NewTag2");
    }

    [Fact]
    public void Update_WithEmptyTags_ClearsTags()
    {
        // Arrange
        var workItem = _workItemFaker.WithTags("Tag1", "Tag2").Generate();

        // Act
        workItem.Update(
            title: workItem.Title,
            workType: workItem.Type,
            statusId: workItem.Status.Id,
            statusCategory: workItem.StatusCategory,
            parentInfo: null,
            teamId: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            extendedProps: null,
            tags: []);

        // Assert
        workItem.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithNullTags_ClearsTags()
    {
        // Arrange
        var workItem = _workItemFaker.WithTags("Tag1", "Tag2").Generate();

        // Act
        workItem.Update(
            title: workItem.Title,
            workType: workItem.Type,
            statusId: workItem.Status.Id,
            statusCategory: workItem.StatusCategory,
            parentInfo: null,
            teamId: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            extendedProps: null,
            tags: null);

        // Assert
        workItem.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Update_DeduplicatesTags()
    {
        // Arrange
        var workItem = _workItemFaker.Generate();
        var duplicateTags = new List<WorkItemTag> { new("Backend"), new("Backend"), new("Q2") };

        // Act
        workItem.Update(
            title: workItem.Title,
            workType: workItem.Type,
            statusId: workItem.Status.Id,
            statusCategory: workItem.StatusCategory,
            parentInfo: null,
            teamId: null,
            lastModified: _now,
            lastModifiedById: null,
            assignedToId: null,
            priority: null,
            stackRank: 0,
            storyPoints: null,
            iterationId: null,
            activatedTimestamp: null,
            doneTimestamp: null,
            extendedProps: null,
            tags: duplicateTags);

        // Assert
        workItem.Tags.Should().HaveCount(2);
        workItem.Tags.Select(t => t.Value).Should().BeEquivalentTo("Backend", "Q2");
    }

    [Fact]
    public void Tags_AreExposedAsReadOnly()
    {
        var workItem = _workItemFaker.WithTags("Tag1").Generate();

        workItem.Tags.Should().BeAssignableTo<IReadOnlyCollection<WorkItemTag>>();
    }

    #endregion

    #region Faker

    [Fact]
    public void WorkItemFaker_WithTags_GeneratesWorkItemWithExpectedTags()
    {
        var workItem = _workItemFaker.WithTags("Alpha", "Beta").Generate();

        workItem.Tags.Should().HaveCount(2);
        workItem.Tags.Select(t => t.Value).Should().BeEquivalentTo("Alpha", "Beta");
    }

    [Fact]
    public void WorkItemFaker_DefaultGenerate_HasEmptyTags()
    {
        var workItem = _workItemFaker.Generate();

        workItem.Tags.Should().BeEmpty();
    }

    #endregion
}
