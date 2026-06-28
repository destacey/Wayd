using NodaTime.Extensions;
using NodaTime.Testing;
using OneOf;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Planning.Domain.Tests.Models;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public class RoadmapTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly RoadmapFaker _faker;
    private readonly RoadmapActivityFaker _activityFaker;
    private readonly RoadmapMilestoneFaker _milestoneFaker;
    private readonly RoadmapTimeboxFaker _timeboxFaker;

    public RoadmapTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new RoadmapFaker(_dateTimeProvider.Today);
        _activityFaker = new RoadmapActivityFaker(localDate: _dateTimeProvider.Today);
        _milestoneFaker = new RoadmapMilestoneFaker(localDate: _dateTimeProvider.Today);
        _timeboxFaker = new RoadmapTimeboxFaker(localDate: _dateTimeProvider.Today);
    }

    [Fact]
    public void Create_ValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();

        // Act
        var result = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(fakeRoadmap.Name);
        result.Value.Description.Should().Be(fakeRoadmap.Description);
        result.Value.DateRange.Should().Be(fakeRoadmap.DateRange);
        result.Value.Visibility.Should().Be(fakeRoadmap.Visibility);
        result.Value.State.Should().Be(RoadmapState.Active);
        result.Value.RoadmapManagers.Should().HaveCount(1);
        result.Value.RoadmapManagers.First().ManagerId.Should().Be(managerId);
    }

    [Fact]
    public void Create_NoManagers_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managers = Array.Empty<Guid>();

        // Act
        var result = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, managers);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Required input roadmapManagerIds was empty. (Parameter 'roadmapManagerIds')");
    }

    [Fact]
    public void Update_ValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var newName = "Updated Name";
        var newDescription = "Updated Description";
        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(11));
        var newVisibility = Visibility.Private;

        // Act
        var result = roadmap.Update(newName, newDescription, newDateRange, [managerId], newVisibility, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Name.Should().Be(newName);
        roadmap.Description.Should().Be(newDescription);
        roadmap.DateRange.Should().Be(newDateRange);
        roadmap.Visibility.Should().Be(newVisibility);
    }

    [Fact]
    public void Update_InvalidManagerId_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.Update("Updated Name", "Updated Description", new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(11)), [managerId], Visibility.Private, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    #region Add/Remove Manager Tests

    [Fact]
    public void AddManager_ValidManagerId_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var initialManagers = new Guid[] { managerId };
        var managerId2 = Guid.NewGuid();
        var expectedManagers = initialManagers.Append(managerId2);

        // Act
        var result = roadmap.AddManager(managerId2, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.RoadmapManagers.Should().HaveCount(2);
        roadmap.RoadmapManagers.Select(x => x.ManagerId).Should().BeEquivalentTo(expectedManagers);
    }

    [Fact]
    public void AddManager_DuplicateManagerId_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.AddManager(managerId, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap manager already exists on this roadmap.");
        roadmap.RoadmapManagers.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveManager_ValidManagerId_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var managerId2 = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId, managerId2]).Value;

        // Act
        var result = roadmap.RemoveManager(managerId2, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.RoadmapManagers.Should().HaveCount(1);
        roadmap.RoadmapManagers.First().ManagerId.Should().Be(managerId);
    }

    [Fact]
    public void RemoveManager_InvalidManagerId_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.RemoveManager(Guid.NewGuid(), managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap manager does not exist on this roadmap.");
    }

    [Fact]
    public void RemoveManager_LastManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.RemoveManager(managerId, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap must have at least one roadmap manager.");
    }

    #endregion Add/Remove Manager Tests

    #region Archive/Activate Tests

    [Fact]
    public void Archive_WhenActive_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.Archive(managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.State.Should().Be(RoadmapState.Archived);
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        // Act
        var result = roadmap.Archive(managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active roadmaps can be archived.");
    }

    [Fact]
    public void Archive_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.Archive(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void Activate_WhenArchived_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        // Act
        var result = roadmap.Activate(managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.State.Should().Be(RoadmapState.Active);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Act
        var result = roadmap.Activate(managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only archived roadmaps can be activated.");
    }

    [Fact]
    public void Activate_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        // Act
        var result = roadmap.Activate(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void Update_WhenArchived_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        // Act
        var result = roadmap.Update("New Name", "New Desc", fakeRoadmap.DateRange, [managerId], Visibility.Public, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Archived roadmaps cannot be modified.");
    }

    [Fact]
    public void CreateActivity_WhenArchived_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        var upsertActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.CreateActivity(upsertActivity, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Archived roadmaps cannot be modified.");
    }

    [Fact]
    public void CanDelete_WhenArchived_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        // Act
        var result = roadmap.CanDelete(managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Archived roadmaps cannot be modified.");
    }

    #endregion Archive/Activate Tests

    #region Copy Tests

    [Fact]
    public void Copy_ValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var newName = "Copied Roadmap";
        var newManagerId = Guid.NewGuid();
        var newVisibility = Visibility.Private;

        // Act
        var result = roadmap.Copy(newName, [newManagerId], newVisibility);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(newName);
        result.Value.Description.Should().Be(fakeRoadmap.Description);
        result.Value.DateRange.Should().Be(fakeRoadmap.DateRange);
        result.Value.Visibility.Should().Be(newVisibility);
        result.Value.State.Should().Be(RoadmapState.Active);
        result.Value.RoadmapManagers.Should().HaveCount(1);
        result.Value.RoadmapManagers.First().ManagerId.Should().Be(newManagerId);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public void Copy_NoManagers_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var newName = "Copied Roadmap";
        var managers = Array.Empty<Guid>();

        // Act
        var result = roadmap.Copy(newName, managers, Visibility.Public);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Required input roadmapManagerIds was empty. (Parameter 'roadmapManagerIds')");
    }

    [Fact]
    public void Copy_WithItems_ShouldCopyAllItems()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Create some items
        var activityResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        activityResult.IsSuccess.Should().BeTrue();

        var milestoneResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()), managerId);
        milestoneResult.IsSuccess.Should().BeTrue();

        var timeboxResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId);
        timeboxResult.IsSuccess.Should().BeTrue();

        var newName = "Copied Roadmap";
        var newManagerId = Guid.NewGuid();

        // Act
        var result = roadmap.Copy(newName, [newManagerId], Visibility.Public);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);

        var copiedActivity = result.Value.Items.OfType<RoadmapActivity>().FirstOrDefault();
        copiedActivity.Should().NotBeNull();
        copiedActivity!.Name.Should().Be(activityResult.Value.Name);
        copiedActivity.Description.Should().Be(activityResult.Value.Description);
        copiedActivity.DateRange.Should().Be(activityResult.Value.DateRange);

        var copiedMilestone = result.Value.Items.OfType<RoadmapMilestone>().FirstOrDefault();
        copiedMilestone.Should().NotBeNull();
        copiedMilestone!.Name.Should().Be(milestoneResult.Value.Name);
        copiedMilestone.Description.Should().Be(milestoneResult.Value.Description);
        copiedMilestone.Date.Should().Be(milestoneResult.Value.Date);

        var copiedTimebox = result.Value.Items.OfType<RoadmapTimebox>().FirstOrDefault();
        copiedTimebox.Should().NotBeNull();
        copiedTimebox!.Name.Should().Be(timeboxResult.Value.Name);
        copiedTimebox.Description.Should().Be(timeboxResult.Value.Description);
        copiedTimebox.DateRange.Should().Be(timeboxResult.Value.DateRange);
    }

    [Fact]
    public void Copy_WithNestedActivities_ShouldPreserveHierarchy()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var parentActivityResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        parentActivityResult.IsSuccess.Should().BeTrue();
        parentActivityResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create child activity
        var childActivity = _activityFaker.WithParentId(parentActivityResult.Value.Id).Generate();
        var childActivityResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(childActivity), managerId);
        childActivityResult.IsSuccess.Should().BeTrue();
        childActivityResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        var newName = "Copied Roadmap";
        var newManagerId = Guid.NewGuid();

        // Act
        var result = roadmap.Copy(newName, [newManagerId], Visibility.Public);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);

        var copiedActivities = result.Value.Items.OfType<RoadmapActivity>().ToList();
        copiedActivities.Should().HaveCount(2);

        // Verify parent activity was copied
        var copiedParent = copiedActivities.FirstOrDefault(a => a.ParentId == null);
        copiedParent.Should().NotBeNull();
        copiedParent!.Name.Should().Be(parentActivityResult.Value.Name);

        // Verify child activity was copied and has correct parent reference
        var copiedChild = copiedActivities.FirstOrDefault(a => a.Name == childActivityResult.Value.Name && a != copiedParent);
        copiedChild.Should().NotBeNull();
        copiedChild!.Name.Should().Be(childActivityResult.Value.Name);
        copiedChild.Parent.Should().Be(copiedParent);
        copiedParent.Children.Should().Contain(copiedChild);
    }

    #endregion Copy Tests

    #region Create Item Tests

    [Fact]
    public void CreateActivity_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var nonManagerId = Guid.NewGuid();
        var upsertActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.CreateActivity(upsertActivity, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void CreateActivity_AsRootItem_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var upsertActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.CreateActivity(upsertActivity, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(upsertActivity.Name);
        result.Value.Type.Should().Be(RoadmapItemType.Activity);
        result.Value.ParentId.Should().BeNull();
        result.Value.Children.Should().BeEmpty();
        roadmap.Items.Should().Contain(result.Value);
    }

    [Fact]
    public void CreateActivity_WithInvalidParent_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var activity = _activityFaker.WithParentId(Guid.NewGuid()).Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);

        // Act
        var result = roadmap.CreateActivity(upsertActivity, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap Activity does not exist on this roadmap.");
    }

    [Fact]
    public void CreateActivity_AsChildItem_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        // Create parent activity
        var parentActivity = _activityFaker.Generate();
        var upsertParentActivity = new TestUpsertRoadmapActivity(parentActivity);
        var parentResult = roadmap.CreateActivity(upsertParentActivity, managerId);
        parentResult.IsSuccess.Should().BeTrue();

        // Create child activity
        var childActivity = _activityFaker
            .WithParentId(parentResult.Value.Id)
            .Generate();
        var upsertChildActivity = new TestUpsertRoadmapActivity(childActivity);

        // Act
        var result = roadmap.CreateActivity(upsertChildActivity, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(parentResult.Value.Id);
        roadmap.Items.Should().Contain(result.Value);
    }

    [Fact]
    public void CreateMilestone_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var nonManagerId = Guid.NewGuid();
        var upsertMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.CreateMilestone(upsertMilestone, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void CreateMilestone_AsRootItem_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var upsertMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.CreateMilestone(upsertMilestone, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(upsertMilestone.Name);
        result.Value.Type.Should().Be(RoadmapItemType.Milestone);
        result.Value.ParentId.Should().BeNull();
        roadmap.Items.Should().Contain(result.Value);
    }


    [Fact]
    public void CreateTimebox_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var nonManagerId = Guid.NewGuid();
        var upsertTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.CreateTimebox(upsertTimebox, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void CreateTimebox_AsRootItem_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var upsertMilestone = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.CreateTimebox(upsertMilestone, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(upsertMilestone.Name);
        result.Value.Type.Should().Be(RoadmapItemType.Timebox);
        result.Value.ParentId.Should().BeNull();
        roadmap.Items.Should().Contain(result.Value);
    }

    #endregion Create Item Tests


    #region Update Item Tests

    // ACTIVITY

    [Fact]
    public void UpdateActivity_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var createResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        createResult.IsSuccess.Should().BeTrue();

        var nonManagerId = Guid.NewGuid();
        var updateActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.UpdateActivity(createResult.Value.Id, updateActivity, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void UpdateActivity_WhenActivityDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var updateActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.UpdateActivity(Guid.NewGuid(), updateActivity, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap Activity does not exist on this roadmap.");
    }

    [Fact]
    public void UpdateActivity_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var createResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        createResult.IsSuccess.Should().BeTrue();

        var updateActivity = new TestUpsertRoadmapActivity(_activityFaker
            .WithName("Updated Activity").WithDescription("Updated Description").WithDateRange(new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(5)))
            .Generate());

        // Act
        var result = roadmap.UpdateActivity(createResult.Value.Id, updateActivity, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedActivity = roadmap.Items.OfType<RoadmapActivity>().First();
        updatedActivity.Name.Should().Be("Updated Activity");
        updatedActivity.Description.Should().Be("Updated Description");
        updatedActivity.DateRange.Start.Should().Be(_dateTimeProvider.Today.PlusDays(1));
        updatedActivity.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(5));
    }

    [Fact]
    public void UpdateActivity_ChangeParentFromRootToChild_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create child activity
        var createChildResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        createChildResult.IsSuccess.Should().BeTrue();

        // Create update request with new parent
        var updateActivity = new TestUpsertRoadmapActivity(createChildResult.Value)
        {
            ParentId = createParentResult.Value.Id
        };

        // Act
        var result = roadmap.UpdateActivity(createChildResult.Value.Id, updateActivity, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedActivity = roadmap.Items.OfType<RoadmapActivity>().First(x => x.Id == createChildResult.Value.Id);
        updatedActivity.ParentId.Should().Be(createParentResult.Value.Id);
        createParentResult.Value.Children.Count.Should().Be(1);
        createParentResult.Value.Children.Should().Contain(updatedActivity);

        var rootActivities = roadmap.Items.OfType<RoadmapActivity>().Where(x => x.ParentId == null).ToList();
        rootActivities.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateActivity_ChangeParentFromChildToRoot_ShouldReturnSuccessAndUpdateOrder()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var parentActivity = _activityFaker.Generate();
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(parentActivity), managerId);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create child activity
        var childActivity = _activityFaker
            .WithParentId(createParentResult.Value.Id)
            .Generate();
        var createChildResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(childActivity), managerId);
        createChildResult.IsSuccess.Should().BeTrue();
        createChildResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());
        createChildResult.Value.SetPrivate(x => x.Parent, createParentResult.Value);

        // Create update request to make it a root activity
        var updateActivity = new TestUpsertRoadmapActivity(createChildResult.Value)
        {
            ParentId = null
        };

        // Act
        var result = roadmap.UpdateActivity(createChildResult.Value.Id, updateActivity, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedActivity = roadmap.Items.OfType<RoadmapActivity>().First(x => x.Id == createChildResult.Value.Id);
        updatedActivity.ParentId.Should().BeNull();

        var rootActivities = roadmap.Items.OfType<RoadmapActivity>().Where(x => x.ParentId == null).ToList();
        rootActivities.Should().HaveCount(2);
        rootActivities.Should().BeInAscendingOrder(x => x.Order);

        var originalParentActivity = rootActivities.First(x => x.Id == createParentResult.Value.Id);
        originalParentActivity.Children.Should().BeEmpty();
    }

    // MILESTONE

    [Fact]
    public void UpdateMilestone_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var createResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()), managerId);
        createResult.IsSuccess.Should().BeTrue();

        var nonManagerId = Guid.NewGuid();
        var updateMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.UpdateMilestone(createResult.Value.Id, updateMilestone, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void UpdateMilestone_WhenMilestoneDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var updateMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.UpdateMilestone(Guid.NewGuid(), updateMilestone, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap Milestone does not exist on this roadmap.");
    }

    [Fact]
    public void UpdateMilestone_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        var milestone = _milestoneFaker.Generate();
        var createResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(milestone), managerId);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        var updateMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker
            .WithName("Updated Milestone").WithDescription("Updated Description").WithDate(_dateTimeProvider.Today.PlusDays(1))
            .Generate());

        // Act
        var result = roadmap.UpdateMilestone(createResult.Value.Id, updateMilestone, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedMilestone = roadmap.Items.OfType<RoadmapMilestone>().First(i => i.Id == createResult.Value.Id);
        updatedMilestone.Name.Should().Be("Updated Milestone");
        updatedMilestone.Description.Should().Be("Updated Description");
        updatedMilestone.Date.Should().Be(_dateTimeProvider.Today.PlusDays(1));
    }

    [Fact]
    public void UpdateMilestone_ChangeParentFromRootToChild_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create milestone
        var createMilestoneResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()), managerId);
        createMilestoneResult.IsSuccess.Should().BeTrue();

        // Create update request with new parent
        var updateMilestone = new TestUpsertRoadmapMilestone(createMilestoneResult.Value)
        {
            ParentId = createParentResult.Value.Id
        };

        // Act
        var result = roadmap.UpdateMilestone(createMilestoneResult.Value.Id, updateMilestone, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedMilestone = roadmap.Items.OfType<RoadmapMilestone>().First(x => x.Id == createMilestoneResult.Value.Id);
        updatedMilestone.ParentId.Should().Be(createParentResult.Value.Id);
        createParentResult.Value.Children.Count.Should().Be(1);
        createParentResult.Value.Children.Should().Contain(updatedMilestone);
    }

    // TIMEBOX

    [Fact]
    public void UpdateTimebox_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var createResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId);
        createResult.IsSuccess.Should().BeTrue();

        var nonManagerId = Guid.NewGuid();
        var updateTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.UpdateTimebox(createResult.Value.Id, updateTimebox, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void UpdateTimebox_WhenMilestoneDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var updateTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.UpdateTimebox(Guid.NewGuid(), updateTimebox, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap Timebox does not exist on this roadmap.");
    }

    [Fact]
    public void UpdateTimebox_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        var createResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        var updateTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker
            .WithName("Updated Timebox").WithDescription("Updated Description").WithDateRange(new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(5)))
            .Generate());

        // Act
        var result = roadmap.UpdateTimebox(createResult.Value.Id, updateTimebox, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedTimebox = roadmap.Items.OfType<RoadmapTimebox>().First(i => i.Id == createResult.Value.Id);
        updatedTimebox.Name.Should().Be("Updated Timebox");
        updatedTimebox.Description.Should().Be("Updated Description");
        updatedTimebox.ParentId.Should().BeNull();
        updatedTimebox.DateRange.Start.Should().Be(_dateTimeProvider.Today.PlusDays(1));
        updatedTimebox.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(5));
    }


    [Fact]
    public void UpdateTimebox_ChangeParentFromRootToChild_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create milestone
        var createTimeboxResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId);
        createTimeboxResult.IsSuccess.Should().BeTrue();

        // Create update request with new parent
        var updateTimebox = new TestUpsertRoadmapTimebox(createTimeboxResult.Value)
        {
            ParentId = createParentResult.Value.Id
        };

        // Act
        var result = roadmap.UpdateTimebox(createTimeboxResult.Value.Id, updateTimebox, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedTimebox = roadmap.Items.OfType<RoadmapTimebox>().First(x => x.Id == createTimeboxResult.Value.Id);
        updatedTimebox.ParentId.Should().Be(createParentResult.Value.Id);
        createParentResult.Value.Children.Count.Should().Be(1);
        createParentResult.Value.Children.Should().Contain(updatedTimebox);
    }



    #endregion Update Item Tests

    #region Update Item Dates Tests

    [Fact]
    public void UpdateRoadmapItemDates_ActivityDateRange_ValidManager_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var activity = _activityFaker.Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);
        var createResult = roadmap.CreateActivity(upsertActivity, managerId);
        createResult.IsSuccess.Should().BeTrue();

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(10));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newDateRange)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedActivity = roadmap.Items.OfType<RoadmapActivity>().First(x => x.Id == createResult.Value.Id);
        updatedActivity.DateRange.Should().Be(newDateRange);
    }

    [Fact]
    public void UpdateRoadmapItemDates_MilestoneDate_ValidManager_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var milestone = _milestoneFaker.Generate();
        var upsertMilestone = new TestUpsertRoadmapMilestone(milestone);
        var createResult = roadmap.CreateMilestone(upsertMilestone, managerId);
        createResult.IsSuccess.Should().BeTrue();

        var newDate = _dateTimeProvider.Today.PlusDays(5);
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT1(
            new TestUpsertRoadmapMilestoneDate(newDate)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedMilestone = roadmap.Items.OfType<RoadmapMilestone>().First(x => x.Id == createResult.Value.Id);
        updatedMilestone.Date.Should().Be(newDate);
    }

    [Fact]
    public void UpdateRoadmapItemDates_TimeboxDateRange_ValidManager_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var timebox = _timeboxFaker.Generate();
        var upsertTimebox = new TestUpsertRoadmapTimebox(timebox);
        var createResult = roadmap.CreateTimebox(upsertTimebox, managerId);
        createResult.IsSuccess.Should().BeTrue();

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(3), _dateTimeProvider.Today.PlusDays(8));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT2(
            new TestUpsertRoadmapTimeboxDateRange(newDateRange)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedTimebox = roadmap.Items.OfType<RoadmapTimebox>().First(x => x.Id == createResult.Value.Id);
        updatedTimebox.DateRange.Should().Be(newDateRange);
    }

    [Fact]
    public void UpdateRoadmapItemDates_WhenUserIsNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var activity = _activityFaker.Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);
        var createResult = roadmap.CreateActivity(upsertActivity, managerId);
        createResult.IsSuccess.Should().BeTrue();

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(10));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newDateRange)
        );
        var nonManagerId = Guid.NewGuid();

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, nonManagerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void UpdateRoadmapItemDates_WhenItemDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(10));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newDateRange)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(Guid.NewGuid(), dateUpdate, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Roadmap Item does not exist on this roadmap.");
    }

    [Fact]
    public void UpdateRoadmapItemDates_TypeMismatch_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        var activity = _activityFaker.Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);
        var createResult = roadmap.CreateActivity(upsertActivity, managerId);
        createResult.IsSuccess.Should().BeTrue();

        var milestoneDate = _dateTimeProvider.Today.PlusDays(5);
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT1(
            new TestUpsertRoadmapMilestoneDate(milestoneDate)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Item is not a Roadmap Milestone.");
    }

    #endregion Update Item Dates Tests

    #region Date Rollup Tests

    private Roadmap CreateRoadmapWithManager(out Guid managerId)
    {
        var fakeRoadmap = _faker.Generate();
        managerId = Guid.NewGuid();
        return Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
    }

    private RoadmapActivity CreateActivity(Roadmap roadmap, Guid managerId, LocalDateRange dateRange, Guid? parentId = null)
    {
        var activity = _activityFaker.WithDateRange(dateRange).WithParentId(parentId).Generate();
        var result = roadmap.CreateActivity(new TestUpsertRoadmapActivity(activity), managerId);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void CreateActivity_ChildEndsAfterParent_ShouldGrowParentEnd()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));

        // Act
        CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(5), _dateTimeProvider.Today.PlusDays(20)),
            parent.Id);

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void CreateActivity_ChildStartsBeforeParent_ShouldGrowParentStart()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(10), _dateTimeProvider.Today.PlusDays(20)));

        // Act
        CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(15)),
            parent.Id);

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void CreateActivity_ChildFullyInsideParent_ShouldLeaveParentUnchanged()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parentRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(30));
        var parent = CreateActivity(roadmap, managerId, parentRange);

        // Act
        CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(5), _dateTimeProvider.Today.PlusDays(10)),
            parent.Id);

        // Assert
        parent.DateRange.Should().Be(parentRange);
    }

    [Fact]
    public void CreateMilestone_ChildDateOutsideParent_ShouldGrowParent()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));
        var milestoneDate = _dateTimeProvider.Today.PlusDays(25);
        var milestone = _milestoneFaker.WithDate(milestoneDate).WithParentId(parent.Id).Generate();

        // Act
        var result = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(milestone), managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(milestoneDate);
    }

    [Fact]
    public void CreateTimebox_ChildRangeOutsideParent_ShouldGrowParent()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));
        var timeboxRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(8), _dateTimeProvider.Today.PlusDays(18));
        var timebox = _timeboxFaker.WithDateRange(timeboxRange).WithParentId(parent.Id).Generate();

        // Act
        var result = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(timebox), managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(18));
    }

    [Fact]
    public void UpdateRoadmapItemDates_ChildGrowsBeyondParent_ShouldBubbleUpToRoot()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var grandparent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)),
            grandparent.Id);
        var child = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)),
            parent.Id);

        var newChildRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(40));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newChildRange));

        // Act
        var result = roadmap.UpdateRoadmapItemDates(child.Id, dateUpdate, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(40));
        grandparent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(40));
    }

    [Fact]
    public void UpdateRoadmapItemDates_ParentWithNoChildren_ShouldApplyRangeUnchanged()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var activity = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));

        var newRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(6));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newRange));

        // Act
        var result = roadmap.UpdateRoadmapItemDates(activity.Id, dateUpdate, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        activity.DateRange.Should().Be(newRange);
    }

    [Fact]
    public void UpdateRoadmapItemDates_ParentWiderThanChildren_ShouldKeepParentRange()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));
        CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(6)),
            parent.Id);

        // Set the parent wider than its child
        var widerRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(-5), _dateTimeProvider.Today.PlusDays(30));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(widerRange));

        // Act
        var result = roadmap.UpdateRoadmapItemDates(parent.Id, dateUpdate, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(widerRange);
    }

    [Fact]
    public void UpdateActivity_FormUpdateGrowsChildBeyondParent_ShouldGrowParent()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        var parent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));
        var child = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)),
            parent.Id);

        // Form update that widens the child past the parent's end
        var updateChild = new TestUpsertRoadmapActivity(child)
        {
            ParentId = parent.Id,
            DateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(35))
        };

        // Act
        var result = roadmap.UpdateActivity(child.Id, updateChild, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(35));
    }

    [Fact]
    public void MoveActivity_ChildExtendsBeyondNewParent_ShouldGrowNewParent()
    {
        // Arrange
        var roadmap = CreateRoadmapWithManager(out var managerId);
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        var originalParent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(40)));
        originalParent.SetPrivate(x => x.Id, Guid.NewGuid());

        var newParent = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(10)));
        newParent.SetPrivate(x => x.Id, Guid.NewGuid());

        var child = CreateActivity(roadmap, managerId,
            new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(30)),
            originalParent.Id);
        child.SetPrivate(x => x.Id, Guid.NewGuid());
        child.SetPrivate(x => x.Parent, originalParent);

        // Act
        var result = roadmap.MoveActivity(child.Id, newParent.Id, 1, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        newParent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(30));
    }

    #endregion Date Rollup Tests

    #region Update Colors Tests

    [Fact]
    public void UpdateColors_WhenValidColors_ShouldReplaceColors()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = true },
            new TestUpsertRoadmapColor { Color = "#00FF00", Name = "On Track", Order = 2, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().HaveCount(2);
        roadmap.Colors.Should().ContainSingle(c => c.Color == "#FF0000" && c.Name == "At Risk" && c.Order == 1 && c.IsDefault);
        roadmap.Colors.Should().ContainSingle(c => c.Color == "#00FF00" && c.Name == "On Track" && c.Order == 2 && !c.IsDefault);
    }

    [Fact]
    public void UpdateColors_WhenRoadmapHasExistingColors_ShouldReplaceThemEntirely()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        roadmap.UpdateColors(
            [new TestUpsertRoadmapColor { Color = "#111111", Name = "Old", Order = 1, IsDefault = false }],
            managerId);

        var newColors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#222222", Name = "New", Order = 1, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(newColors, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().ContainSingle();
        roadmap.Colors.Should().ContainSingle(c => c.Color == "#222222");
        roadmap.Colors.Should().NotContain(c => c.Color == "#111111");
    }

    [Fact]
    public void UpdateColors_WhenEmptyCollection_ShouldClearColors()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        roadmap.UpdateColors(
            [new TestUpsertRoadmapColor { Color = "#111111", Name = "Old", Order = 1, IsDefault = false }],
            managerId);

        // Act
        var result = roadmap.UpdateColors([], managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().BeEmpty();
    }

    [Fact]
    public void UpdateColors_WhenMoreThanOneDefault_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = true },
            new TestUpsertRoadmapColor { Color = "#00FF00", Name = "On Track", Order = 2, IsDefault = true },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only one color can be marked as the default.");
    }

    [Fact]
    public void UpdateColors_WhenDuplicateColors_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = false },
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "Blocked", Order = 2, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A Roadmap cannot have two colors with the same value.");
    }

    [Fact]
    public void UpdateColors_WhenDuplicateColorsDifferByCase_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#abc123", Name = "Lower", Order = 1, IsDefault = false },
            new TestUpsertRoadmapColor { Color = "#ABC123", Name = "Upper", Order = 2, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A Roadmap cannot have two colors with the same value.");
    }

    [Fact]
    public void UpdateColors_WhenExceedingMaxColors_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = Enumerable.Range(0, Roadmap.MaxColors + 1)
            .Select(i => new TestUpsertRoadmapColor
            {
                Color = $"#{i:X6}",
                Name = $"Color {i}",
                Order = i + 1,
                IsDefault = false,
            })
            .ToArray();

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"A Roadmap cannot have more than {Roadmap.MaxColors} colors.");
    }

    [Fact]
    public void UpdateColors_WhenAtMaxColors_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = Enumerable.Range(0, Roadmap.MaxColors)
            .Select(i => new TestUpsertRoadmapColor
            {
                Color = $"#{i:X6}",
                Name = $"Color {i}",
                Order = i + 1,
                IsDefault = false,
            })
            .ToArray();

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().HaveCount(Roadmap.MaxColors);
    }

    [Fact]
    public void UpdateColors_WhenNotManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    [Fact]
    public void UpdateColors_WhenArchived_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId]).Value;
        roadmap.Archive(managerId);

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Archived roadmaps cannot be modified.");
    }

    #endregion Update Colors Tests

    //[Fact]
    //public void SetChildrenOrder_ForAll_WhenValidChildrenProvided_ShouldReturnSuccess()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(3);
    //    var managerId = roadmap.RoadmapManagers.First().ManagerId;

    //    var children = roadmap.Children.OrderBy(c => c.Order).ToList();

    //    var child1 = children[0];
    //    var child2 = children[1];
    //    var child3 = children[2];

    //    var childLinks = new Dictionary<Guid, int>
    //    {
    //        { child1.Id, 2 },
    //        { child2.Id, 17 }, // setting the higher order than the count of child links should still set it to the last
    //        { child3.Id, 1 }
    //    };

    //    // Act
    //    var result = roadmap.SetChildrenOrder(childLinks, managerId);

    //    // Assert
    //    result.IsSuccess.Should().BeTrue();
    //    roadmap.Children.First(x => x.Id == child1.Id).Order.Should().Be(2);
    //    roadmap.Children.First(x => x.Id == child2.Id).Order.Should().Be(3);
    //    roadmap.Children.First(x => x.Id == child3.Id).Order.Should().Be(1);
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForAll_WhenUserIsNotManager_ShouldReturnFailure()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(3);
    //    var childLinks = new Dictionary<Guid, int>();
    //    var nonManagerId = Guid.NewGuid();

    //    // Act
    //    var result = roadmap.SetChildrenOrder(childLinks, nonManagerId);

    //    // Assert
    //    result.IsFailure.Should().BeTrue();
    //    result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForAll_WhenChildLinksCountMismatch_ShouldReturnFailure()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(2);
    //    var managerId = roadmap.RoadmapManagers.First().ManagerId;
    //    var childLinks = new Dictionary<Guid, int> { { Guid.NewGuid(), 1 } };

    //    // Act
    //    var result = roadmap.SetChildrenOrder(childLinks, managerId);

    //    // Assert
    //    result.IsFailure.Should().BeTrue();
    //    result.Error.Should().Be("Not all child roadmaps provided were found.");
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForAll_WhenChildLinkNotFound_ShouldReturnFailure()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(1);
    //    var managerId = roadmap.RoadmapManagers.First().ManagerId;
    //    var childLinks = new Dictionary<Guid, int> { { Guid.NewGuid(), 1 } };

    //    // Act
    //    var result = roadmap.SetChildrenOrder(childLinks, managerId);

    //    // Assert
    //    result.IsFailure.Should().BeTrue();
    //    result.Error.Should().Be("Not all child roadmaps provided were found.");
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForOne_WhenMovingDown_ShouldReturnSuccess()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(5);
    //    var managerId = roadmap.RoadmapManagers.First().ManagerId;

    //    var children = roadmap.Children.OrderBy(c => c.Order).ToList();

    //    var child1 = children[0];
    //    var child2 = children[1];
    //    var child3 = children[2];
    //    var child4 = children[3];
    //    var child5 = children[4];

    //    // Act
    //    var result = roadmap.SetChildrenOrder(child2.Id, 4, managerId);

    //    // Assert
    //    result.IsSuccess.Should().BeTrue();
    //    roadmap.Children.First(x => x.Id == child1.Id).Order.Should().Be(1);
    //    roadmap.Children.First(x => x.Id == child2.Id).Order.Should().Be(4);
    //    roadmap.Children.First(x => x.Id == child3.Id).Order.Should().Be(2);
    //    roadmap.Children.First(x => x.Id == child4.Id).Order.Should().Be(3);
    //    roadmap.Children.First(x => x.Id == child5.Id).Order.Should().Be(5);
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForOne_WhenMovingUp_ShouldReturnSuccess()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(5);
    //    var managerId = roadmap.RoadmapManagers.First().ManagerId;

    //    var children = roadmap.Children.OrderBy(c => c.Order).ToList();

    //    var child1 = children[0];
    //    var child2 = children[1];
    //    var child3 = children[2];
    //    var child4 = children[3];
    //    var child5 = children[4];

    //    // Act
    //    var result = roadmap.SetChildrenOrder(child4.Id, 2, managerId);

    //    // Assert
    //    result.IsSuccess.Should().BeTrue();
    //    roadmap.Children.First(x => x.Id == child1.Id).Order.Should().Be(1);
    //    roadmap.Children.First(x => x.Id == child2.Id).Order.Should().Be(3);
    //    roadmap.Children.First(x => x.Id == child3.Id).Order.Should().Be(4);
    //    roadmap.Children.First(x => x.Id == child4.Id).Order.Should().Be(2);
    //    roadmap.Children.First(x => x.Id == child5.Id).Order.Should().Be(5);
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForOne_WhenUserIsNotManager_ShouldReturnFailure()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(3);
    //    var children = new Dictionary<Guid, int>();
    //    var nonManagerId = Guid.NewGuid();

    //    // Act
    //    var result = roadmap.SetChildrenOrder(children, nonManagerId);

    //    // Assert
    //    result.IsFailure.Should().BeTrue();
    //    result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    //}

    //[Fact]
    //public void SetChildLinksOrder_ForOne_WhenChildLinkNotFound_ShouldReturnFailure()
    //{
    //    // Arrange
    //    var roadmap = _faker.WithChildren(3);
    //    var managerId = roadmap.RoadmapManagers.First().ManagerId;

    //    // Act
    //    var result = roadmap.SetChildrenOrder(Guid.NewGuid(), 1, managerId);

    //    // Assert
    //    result.IsFailure.Should().BeTrue();
    //    result.Error.Should().Be("Child roadmap does not exist on this roadmap.");
    //}
}