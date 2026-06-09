using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Interfaces;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkItemTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly WorkItemFaker _workItemFaker;
    private readonly WorkTypeFaker _workTypeFaker;

    public WorkItemTests()
    {
        _dateTimeProvider = new(new DateTime(2024, 04, 01, 11, 0, 0));
        _workItemFaker = new WorkItemFaker();
        _workTypeFaker = new WorkTypeFaker();
    }

    #region UpdateParent

    [Fact]
    public void UpdateParent_WithNoParent_SetNullParent_ShouldSucceed()
    {
        // Arrange
        var workItem = _workItemFaker.Generate();
        IWorkItemParentInfo? parentInfo = null;

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
    }

    [Fact]
    public void UpdateParent_WithParent_SetNullParent_ShouldSucceed()
    {
        // Arrange
        var workItem = _workItemFaker.WithParentId(Guid.NewGuid()).WithParentProjectId(Guid.NewGuid()).Generate();
        IWorkItemParentInfo? parentInfo = null;

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
    }

    [Fact]
    public void UpdateParent_StoryWithNoParent_SetEpicParent_ShouldSucceed()
    {
        // Arrange
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var epicWorkType = _workTypeFaker.AsEpic().Generate();
        var workItem = _workItemFaker.WithType(storyWorkType).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, epicWorkType.Level!.Tier, epicWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().Be(parentInfo.Id);
        workItem.ParentProjectId.Should().BeNull();
    }

    [Fact]
    public void UpdateParent_FeatureWithNoParent_SetEpicParent_ShouldSucceed()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var epicWorkType = _workTypeFaker.AsEpic().Generate();
        var projectId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(featureWorkType).WithProjectId(projectId).Generate();
        var parentProjectId = Guid.NewGuid();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, epicWorkType.Level!.Tier, epicWorkType.Level.Order, parentProjectId);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().Be(parentInfo.Id);
        workItem.ParentProjectId.Should().Be(parentProjectId);
        workItem.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public void UpdateParent_StoryWithNoParent_SetStoryParent_ShouldFail()
    {
        // Arrange
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var workItem = _workItemFaker.WithType(storyWorkType).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, storyWorkType.Level!.Tier, storyWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_StoryWithNoParent_SetOtherParent_ShouldFail()
    {
        // Arrange
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var otherWorkType = _workTypeFaker.AsOther().Generate();
        var workItem = _workItemFaker.WithType(storyWorkType).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, otherWorkType.Level!.Tier, otherWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_FeatureWithNoParent_SetOtherParent_ShouldFail()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var otherWorkType = _workTypeFaker.AsOther().Generate();
        var workItem = _workItemFaker.WithType(featureWorkType).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, otherWorkType.Level!.Tier, otherWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_FeatureWithNoParent_SetStoryParent_ShouldFail()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var workItem = _workItemFaker.WithType(featureWorkType).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, storyWorkType.Level!.Tier, storyWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_FeatureWithNoParent_SetFeatureParent_ShouldFail()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var workItem = _workItemFaker.WithType(featureWorkType).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, featureWorkType.Level!.Tier, featureWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().BeNull();
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("The parent must be a higher level than the work item.");
    }

    [Fact]
    public void UpdateParent_StoryWithParent_SetEpicParent_ShouldSucceed()
    {
        // Arrange
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var epicWorkType = _workTypeFaker.AsEpic().Generate();
        var workItem = _workItemFaker.WithType(storyWorkType).WithParentId(Guid.NewGuid()).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, epicWorkType.Level!.Tier, epicWorkType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().Be(parentInfo.Id);
        workItem.ParentProjectId.Should().Be(parentInfo.ProjectId);
    }

    [Fact]
    public void UpdateParent_FeatureWithParent_SetEpicParent_ShouldSucceed()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var epicWorkType = _workTypeFaker.AsEpic().Generate();
        var workItem = _workItemFaker.WithType(featureWorkType).WithParentId(Guid.NewGuid()).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, epicWorkType.Level!.Tier, epicWorkType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().Be(parentInfo.Id);
        workItem.ParentProjectId.Should().Be(parentInfo.ProjectId);
    }

    [Fact]
    public void UpdateParent_FeatureWithProjectId_SetEpicParentWithDifferentProjectId_ShouldSucceed()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var epicWorkType = _workTypeFaker.AsEpic().Generate();
        var featureProjectId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(featureWorkType).WithParentId(Guid.NewGuid()).WithProjectId(featureProjectId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, epicWorkType.Level!.Tier, epicWorkType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().Be(parentInfo.Id);
        workItem.ParentProjectId.Should().Be(parentInfo.ProjectId);
        workItem.ProjectId.Should().Be(featureProjectId);
    }

    [Fact]
    public void UpdateParent_FeatureWithProjectId_SetEpicParentWithSameProjectId_ShouldSucceed()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var epicWorkType = _workTypeFaker.AsEpic().Generate();
        var projectId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(featureWorkType).WithParentId(Guid.NewGuid()).WithProjectId(projectId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, epicWorkType.Level!.Tier, epicWorkType.Level.Order, projectId);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workItem.ParentId.Should().Be(parentInfo.Id);
        workItem.ParentProjectId.Should().Be(projectId);
        workItem.ProjectId.Should().BeNull();
    }

    [Fact]
    public void UpdateParent_StoryWithParent_SetStoryParent_ShouldFail()
    {
        // Arrange
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var expectedParentId = Guid.NewGuid();
        var expectedParentProjectId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(storyWorkType).WithParentId(expectedParentId).WithParentProjectId(expectedParentProjectId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, storyWorkType.Level!.Tier, storyWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().Be(expectedParentId);
        workItem.ParentProjectId.Should().Be(expectedParentProjectId);
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_StoryWithParent_SetOtherParent_ShouldFail()
    {
        // Arrange
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var otherWorkType = _workTypeFaker.AsOther().Generate();
        var expectedParentId = Guid.NewGuid();
        var expectedParentProjectId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(storyWorkType).WithParentId(expectedParentId).WithParentProjectId(expectedParentProjectId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, otherWorkType.Level!.Tier, otherWorkType.Level.Order);

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().Be(expectedParentId);
        workItem.ParentProjectId.Should().Be(expectedParentProjectId);
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_FeatureWithParent_SetOtherParent_ShouldFail()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var otherWorkType = _workTypeFaker.AsOther().Generate();
        var expectedParentId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(featureWorkType).WithParentId(expectedParentId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, otherWorkType.Level!.Tier, otherWorkType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().Be(expectedParentId);
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_FeatureWithParent_SetStoryParent_ShouldFail()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var storyWorkType = _workTypeFaker.AsStory().Generate();
        var expectedParentId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(featureWorkType).WithParentId(expectedParentId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, storyWorkType.Level!.Tier, storyWorkType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().Be(expectedParentId);
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Only portfolio tier work items can be parents.");
    }

    [Fact]
    public void UpdateParent_FeatureWithParent_SetFeatureParent_ShouldFail()
    {
        // Arrange
        var featureWorkType = _workTypeFaker.AsFeature().Generate();
        var expectedParentId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(featureWorkType).WithParentId(expectedParentId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, featureWorkType.Level!.Tier, featureWorkType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().Be(expectedParentId);
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("The parent must be a higher level than the work item.");
    }

    [Fact]
    public void UpdateParent_FeatureWithParentAndWithoutLevel_SetFeatureParent_ShouldFail()
    {
        // Arrange
        var workItemType = _workTypeFaker.AsFeature().Generate();
        var parentType = _workTypeFaker.AsFeature().Generate();

        workItemType.Level = null;

        var expectedParentId = Guid.NewGuid();
        var workItem = _workItemFaker.WithType(workItemType).WithParentId(expectedParentId).Generate();
        var parentInfo = new TestParentInfo(Guid.NewGuid(), 123456, parentType.Level!.Tier, parentType.Level.Order, Guid.NewGuid());

        // Act
        var result = workItem.UpdateParent(parentInfo, workItem.Type);

        // Assert
        result.IsFailure.Should().BeTrue();
        workItem.ParentId.Should().Be(expectedParentId);
        workItem.ParentProjectId.Should().BeNull();
        result.Error.Should().Be("Unable to set the work item parent without the type and level.");
    }

    #endregion UpdateParent

    public sealed record TestParentInfo(Guid Id, int? ExternalId, WorkTypeTier Tier, int LevelOrder, Guid? ProjectId = null) : IWorkItemParentInfo;
}