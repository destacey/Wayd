using NodaTime.Extensions;
using NodaTime.Testing;
using OneOf;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Roadmaps;
using Wayd.Common.Models;
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

    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());

    private Instant Now => _dateTimeProvider.Now;

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
        var result = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now);

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
        var result = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, managers, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var newName = "Updated Name";
        var newDescription = "Updated Description";
        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(11));
        var newVisibility = Visibility.Private;

        // Act
        var result = roadmap.Update(newName, newDescription, newDateRange, [managerId], newVisibility, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Act
        var result = roadmap.Update("Updated Name", "Updated Description", new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(11)), [managerId], Visibility.Private, Guid.NewGuid(), Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User is not a roadmap manager of this roadmap.");
    }

    #region Manager Tests

    [Fact]
    public void Update_AddedManager_ShouldAddManager()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var managerId2 = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Act
        var result = roadmap.Update(roadmap.Name, roadmap.Description, roadmap.DateRange, [managerId, managerId2], roadmap.Visibility, managerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.RoadmapManagers.Select(x => x.ManagerId).Should().BeEquivalentTo([managerId, managerId2]);
    }

    [Fact]
    public void Update_RemovedManager_ShouldRemoveManager()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var managerId2 = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId, managerId2], Actor, Now).Value;

        // Act
        var result = roadmap.Update(roadmap.Name, roadmap.Description, roadmap.DateRange, [managerId], roadmap.Visibility, managerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.RoadmapManagers.Should().ContainSingle().Which.ManagerId.Should().Be(managerId);
    }

    [Fact]
    public void Update_WithoutCurrentUserAsManager_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var managerId2 = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId, managerId2], Actor, Now).Value;

        // Act
        var result = roadmap.Update(roadmap.Name, roadmap.Description, roadmap.DateRange, [managerId2], roadmap.Visibility, managerId, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The current user must be a roadmap manager of the Roadmap in order to update it.");
        roadmap.RoadmapManagers.Should().HaveCount(2);
    }

    #endregion Manager Tests

    #region Archive/Activate Tests

    [Fact]
    public void Archive_WhenActive_ShouldReturnSuccess()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Act
        var result = roadmap.Archive(managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        // Act
        var result = roadmap.Archive(managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Act
        var result = roadmap.Archive(Guid.NewGuid(), Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        // Act
        var result = roadmap.Activate(managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Act
        var result = roadmap.Activate(managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        // Act
        var result = roadmap.Activate(Guid.NewGuid(), Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        // Act
        var result = roadmap.Update("New Name", "New Desc", fakeRoadmap.DateRange, [managerId], Visibility.Public, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        var upsertActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.CreateActivity(upsertActivity, managerId, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Archived roadmaps cannot be modified.");
    }

    [Fact]
    public void Delete_WhenArchived_ShouldReturnFailure()
    {
        // Arrange
        var fakeRoadmap = _faker.Generate();
        var managerId = Guid.NewGuid();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        // Act
        var result = roadmap.Delete(managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var newName = "Copied Roadmap";
        var newManagerId = Guid.NewGuid();
        var newVisibility = Visibility.Private;

        // Act
        var result = roadmap.Copy(newName, [newManagerId], newVisibility, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var newName = "Copied Roadmap";
        var managers = Array.Empty<Guid>();

        // Act
        var result = roadmap.Copy(newName, managers, Visibility.Public, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Create some items
        var activityResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        activityResult.IsSuccess.Should().BeTrue();

        var milestoneResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()), managerId, Actor, Now);
        milestoneResult.IsSuccess.Should().BeTrue();

        var timeboxResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId, Actor, Now);
        timeboxResult.IsSuccess.Should().BeTrue();

        var newName = "Copied Roadmap";
        var newManagerId = Guid.NewGuid();

        // Act
        var result = roadmap.Copy(newName, [newManagerId], Visibility.Public, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var parentActivityResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        parentActivityResult.IsSuccess.Should().BeTrue();
        parentActivityResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create child activity
        var childActivity = _activityFaker.WithParentId(parentActivityResult.Value.Id).Generate();
        var childActivityResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(childActivity), managerId, Actor, Now);
        childActivityResult.IsSuccess.Should().BeTrue();
        childActivityResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        var newName = "Copied Roadmap";
        var newManagerId = Guid.NewGuid();

        // Act
        var result = roadmap.Copy(newName, [newManagerId], Visibility.Public, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var nonManagerId = Guid.NewGuid();
        var upsertActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.CreateActivity(upsertActivity, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var upsertActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.CreateActivity(upsertActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var activity = _activityFaker.WithParentId(Guid.NewGuid()).Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);

        // Act
        var result = roadmap.CreateActivity(upsertActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        // Create parent activity
        var parentActivity = _activityFaker.Generate();
        var upsertParentActivity = new TestUpsertRoadmapActivity(parentActivity);
        var parentResult = roadmap.CreateActivity(upsertParentActivity, managerId, Actor, Now);
        parentResult.IsSuccess.Should().BeTrue();

        // Create child activity
        var childActivity = _activityFaker
            .WithParentId(parentResult.Value.Id)
            .Generate();
        var upsertChildActivity = new TestUpsertRoadmapActivity(childActivity);

        // Act
        var result = roadmap.CreateActivity(upsertChildActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var nonManagerId = Guid.NewGuid();
        var upsertMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.CreateMilestone(upsertMilestone, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var upsertMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.CreateMilestone(upsertMilestone, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var nonManagerId = Guid.NewGuid();
        var upsertTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.CreateTimebox(upsertTimebox, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var upsertMilestone = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.CreateTimebox(upsertMilestone, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var createResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var nonManagerId = Guid.NewGuid();
        var updateActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.UpdateActivity(createResult.Value.Id, updateActivity, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var updateActivity = new TestUpsertRoadmapActivity(_activityFaker.Generate());

        // Act
        var result = roadmap.UpdateActivity(Guid.NewGuid(), updateActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var createResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var updateActivity = new TestUpsertRoadmapActivity(_activityFaker
            .WithName("Updated Activity").WithDescription("Updated Description").WithDateRange(new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(5)))
            .Generate());

        // Act
        var result = roadmap.UpdateActivity(createResult.Value.Id, updateActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create child activity
        var createChildResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        createChildResult.IsSuccess.Should().BeTrue();

        // Create update request with new parent
        var updateActivity = new TestUpsertRoadmapActivity(createChildResult.Value)
        {
            ParentId = createParentResult.Value.Id
        };

        // Act
        var result = roadmap.UpdateActivity(createChildResult.Value.Id, updateActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var parentActivity = _activityFaker.Generate();
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(parentActivity), managerId, Actor, Now);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create child activity
        var childActivity = _activityFaker
            .WithParentId(createParentResult.Value.Id)
            .Generate();
        var createChildResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(childActivity), managerId, Actor, Now);
        createChildResult.IsSuccess.Should().BeTrue();
        createChildResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());
        createChildResult.Value.SetPrivate(x => x.Parent, createParentResult.Value);

        // Create update request to make it a root activity
        var updateActivity = new TestUpsertRoadmapActivity(createChildResult.Value)
        {
            ParentId = null
        };

        // Act
        var result = roadmap.UpdateActivity(createChildResult.Value.Id, updateActivity, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var createResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()), managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var nonManagerId = Guid.NewGuid();
        var updateMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.UpdateMilestone(createResult.Value.Id, updateMilestone, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var updateMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate());

        // Act
        var result = roadmap.UpdateMilestone(Guid.NewGuid(), updateMilestone, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        var milestone = _milestoneFaker.Generate();
        var createResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(milestone), managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        var updateMilestone = new TestUpsertRoadmapMilestone(_milestoneFaker
            .WithName("Updated Milestone").WithDescription("Updated Description").WithDate(_dateTimeProvider.Today.PlusDays(1))
            .Generate());

        // Act
        var result = roadmap.UpdateMilestone(createResult.Value.Id, updateMilestone, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create milestone
        var createMilestoneResult = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()), managerId, Actor, Now);
        createMilestoneResult.IsSuccess.Should().BeTrue();

        // Create update request with new parent
        var updateMilestone = new TestUpsertRoadmapMilestone(createMilestoneResult.Value)
        {
            ParentId = createParentResult.Value.Id
        };

        // Act
        var result = roadmap.UpdateMilestone(createMilestoneResult.Value.Id, updateMilestone, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var createResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var nonManagerId = Guid.NewGuid();
        var updateTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.UpdateTimebox(createResult.Value.Id, updateTimebox, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var updateTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker.Generate());

        // Act
        var result = roadmap.UpdateTimebox(Guid.NewGuid(), updateTimebox, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        var createResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        var updateTimebox = new TestUpsertRoadmapTimebox(_timeboxFaker
            .WithName("Updated Timebox").WithDescription("Updated Description").WithDateRange(new LocalDateRange(_dateTimeProvider.Today.PlusDays(1), _dateTimeProvider.Today.PlusDays(5)))
            .Generate());

        // Act
        var result = roadmap.UpdateTimebox(createResult.Value.Id, updateTimebox, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create parent activity
        var createParentResult = roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.Generate()), managerId, Actor, Now);
        createParentResult.IsSuccess.Should().BeTrue();
        createParentResult.Value.SetPrivate(x => x.Id, Guid.NewGuid());

        // Create milestone
        var createTimeboxResult = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(_timeboxFaker.Generate()), managerId, Actor, Now);
        createTimeboxResult.IsSuccess.Should().BeTrue();

        // Create update request with new parent
        var updateTimebox = new TestUpsertRoadmapTimebox(createTimeboxResult.Value)
        {
            ParentId = createParentResult.Value.Id
        };

        // Act
        var result = roadmap.UpdateTimebox(createTimeboxResult.Value.Id, updateTimebox, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var activity = _activityFaker.Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);
        var createResult = roadmap.CreateActivity(upsertActivity, managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(10));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newDateRange)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var milestone = _milestoneFaker.Generate();
        var upsertMilestone = new TestUpsertRoadmapMilestone(milestone);
        var createResult = roadmap.CreateMilestone(upsertMilestone, managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var newDate = _dateTimeProvider.Today.PlusDays(5);
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT1(
            new TestUpsertRoadmapMilestoneDate(newDate)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var timebox = _timeboxFaker.Generate();
        var upsertTimebox = new TestUpsertRoadmapTimebox(timebox);
        var createResult = roadmap.CreateTimebox(upsertTimebox, managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(3), _dateTimeProvider.Today.PlusDays(8));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT2(
            new TestUpsertRoadmapTimeboxDateRange(newDateRange)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var activity = _activityFaker.Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);
        var createResult = roadmap.CreateActivity(upsertActivity, managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(10));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newDateRange)
        );
        var nonManagerId = Guid.NewGuid();

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, nonManagerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var newDateRange = new LocalDateRange(_dateTimeProvider.Today.PlusDays(2), _dateTimeProvider.Today.PlusDays(10));
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT0(
            new TestUpsertRoadmapActivityDateRange(newDateRange)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(Guid.NewGuid(), dateUpdate, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        var activity = _activityFaker.Generate();
        var upsertActivity = new TestUpsertRoadmapActivity(activity);
        var createResult = roadmap.CreateActivity(upsertActivity, managerId, Actor, Now);
        createResult.IsSuccess.Should().BeTrue();

        var milestoneDate = _dateTimeProvider.Today.PlusDays(5);
        var dateUpdate = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>.FromT1(
            new TestUpsertRoadmapMilestoneDate(milestoneDate)
        );

        // Act
        var result = roadmap.UpdateRoadmapItemDates(createResult.Value.Id, dateUpdate, managerId, Actor, Now);

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
        return Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
    }

    private RoadmapActivity CreateActivity(Roadmap roadmap, Guid managerId, LocalDateRange dateRange, Guid? parentId = null)
    {
        var activity = _activityFaker.WithDateRange(dateRange).WithParentId(parentId).Generate();
        var result = roadmap.CreateActivity(new TestUpsertRoadmapActivity(activity), managerId, Actor, Now);
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
        var result = roadmap.CreateMilestone(new TestUpsertRoadmapMilestone(milestone), managerId, Actor, Now);

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
        var result = roadmap.CreateTimebox(new TestUpsertRoadmapTimebox(timebox), managerId, Actor, Now);

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
        var result = roadmap.UpdateRoadmapItemDates(child.Id, dateUpdate, managerId, Actor, Now);

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
        var result = roadmap.UpdateRoadmapItemDates(activity.Id, dateUpdate, managerId, Actor, Now);

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
        var result = roadmap.UpdateRoadmapItemDates(parent.Id, dateUpdate, managerId, Actor, Now);

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
        var result = roadmap.UpdateActivity(child.Id, updateChild, managerId, Actor, Now);

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
        var result = roadmap.MoveActivity(child.Id, newParent.Id, 1, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = true },
            new TestUpsertRoadmapColor { Color = "#00FF00", Name = "On Track", Order = 2, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        roadmap.UpdateColors(
            [new TestUpsertRoadmapColor { Color = "#111111", Name = "Old", Order = 1, IsDefault = false }],
            managerId, Actor, Now);

        var newColors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#222222", Name = "New", Order = 1, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(newColors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        roadmap.UpdateColors(
            [new TestUpsertRoadmapColor { Color = "#111111", Name = "Old", Order = 1, IsDefault = false }],
            managerId, Actor, Now);

        // Act
        var result = roadmap.UpdateColors([], managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = true },
            new TestUpsertRoadmapColor { Color = "#00FF00", Name = "On Track", Order = 2, IsDefault = true },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = false },
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "Blocked", Order = 2, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#abc123", Name = "Lower", Order = 1, IsDefault = false },
            new TestUpsertRoadmapColor { Color = "#ABC123", Name = "Upper", Order = 2, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

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
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

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
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, Guid.NewGuid(), Actor, Now);

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
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [managerId], Actor, Now).Value;
        roadmap.Archive(managerId, Actor, Now);

        var colors = new[]
        {
            new TestUpsertRoadmapColor { Color = "#FF0000", Name = "At Risk", Order = 1, IsDefault = false },
        };

        // Act
        var result = roadmap.UpdateColors(colors, managerId, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Archived roadmaps cannot be modified.");
    }

    #endregion Update Colors Tests

    #region Events

    private static readonly Guid EventManagerId = Guid.NewGuid();

    private Roadmap SavedRoadmap()
    {
        var roadmap = Roadmap.Create("Platform", "Platform work", new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(90)), Visibility.Public, [EventManagerId], Actor, Now).Value;
        roadmap.SetPrivate(r => r.Key, 7);
        roadmap.ExecutePostPersistenceActions();
        roadmap.ClearDomainEvents();

        return roadmap;
    }

    private LocalDateRange Days(int start, int end) => new(_dateTimeProvider.Today.PlusDays(start), _dateTimeProvider.Today.PlusDays(end));

    private RoadmapActivity AddActivity(Roadmap roadmap, string name, LocalDateRange dateRange, Guid? parentId = null)
    {
        var upsert = new TestUpsertRoadmapActivity(_activityFaker.WithName(name).WithDateRange(dateRange).WithColor(null).Generate()) { ParentId = parentId };
        var activity = roadmap.CreateActivity(upsert, EventManagerId, Actor, Now).Value;
        roadmap.ClearDomainEvents();

        return activity;
    }

    private RoadmapMilestone AddMilestone(Roadmap roadmap, LocalDate date, Guid? parentId)
    {
        var upsert = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()) { Date = date, ParentId = parentId };
        var milestone = roadmap.CreateMilestone(upsert, EventManagerId, Actor, Now).Value;
        roadmap.ClearDomainEvents();

        return milestone;
    }

    [Fact]
    public void Create_RaisesCreated_OnceTheFirstSaveAssignsTheKey()
    {
        // Arrange
        var dateRange = Days(0, 90);

        // Act
        var roadmap = Roadmap.Create("Platform", "Platform work", dateRange, Visibility.Private, [EventManagerId], Actor, Now).Value;

        // Assert
        roadmap.DomainEvents.Should().BeEmpty();

        roadmap.SetPrivate(r => r.Key, 7);
        roadmap.ExecutePostPersistenceActions();

        var created = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapCreatedEvent>().Subject;
        created.Id.Should().Be(roadmap.Id);
        created.Key.Should().Be(7);
        created.Name.Should().Be("Platform");
        created.Description.Should().Be("Platform work");
        created.DateRange.Should().Be(dateRange);
        created.Visibility.Should().Be(Visibility.Private);
        created.State.Should().Be(RoadmapState.Active);
        created.ManagerIds.Should().Equal(EventManagerId);
        created.Colors.Should().BeEmpty();
        created.Items.Should().BeEmpty();
        created.Actor.Should().Be(Actor);
        created.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Create_ThenChangedBeforeTheFirstSave_RecordsTheRoadmapAsCreated()
    {
        // Arrange
        var dateRange = Days(0, 90);
        var roadmap = Roadmap.Create("Platform", null, dateRange, Visibility.Public, [EventManagerId], Actor, Now).Value;

        // Act
        roadmap.Update("Platform v2", null, dateRange, [EventManagerId], Visibility.Public, EventManagerId, Actor, Now);
        roadmap.CreateActivity(new TestUpsertRoadmapActivity(_activityFaker.WithDateRange(Days(1, 10)).Generate()) { ParentId = null }, EventManagerId, Actor, Now);

        // Assert
        roadmap.DomainEvents.Should().BeEmpty();

        roadmap.SetPrivate(r => r.Key, 7);
        roadmap.ExecutePostPersistenceActions();

        var created = roadmap.DomainEvents.First().Should().BeOfType<RoadmapCreatedEvent>().Subject;
        created.Name.Should().Be("Platform");
        created.Items.Should().BeEmpty();
        roadmap.DomainEvents.Skip(1).Select(e => e.GetType())
            .Should().Equal(typeof(RoadmapDetailsUpdatedEvent), typeof(RoadmapActivityAddedEvent));
    }

    [Fact]
    public void Copy_RaisesCreated_WithTheCopiedItems()
    {
        // Arrange
        var source = SavedRoadmap();
        var parent = AddActivity(source, "Parent", Days(1, 30));
        AddMilestone(source, _dateTimeProvider.Today.PlusDays(5), parent.Id);

        // Act
        var copy = source.Copy("Copy", [EventManagerId], Visibility.Private, Actor, Now).Value;

        // Assert
        copy.SetPrivate(r => r.Key, 8);
        copy.ExecutePostPersistenceActions();

        var created = copy.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapCreatedEvent>().Subject;
        created.Name.Should().Be("Copy");
        created.Items.Should().HaveCount(2);
        var copiedParent = created.Items.Single(i => i.Type == RoadmapItemType.Activity);
        copiedParent.ItemId.Should().NotBe(parent.Id);
        copiedParent.Order.Should().Be(1);
        var copiedMilestone = created.Items.Single(i => i.Type == RoadmapItemType.Milestone);
        copiedMilestone.ParentId.Should().Be(copiedParent.ItemId);
        copiedMilestone.DateRange.Should().Be(Days(5, 5));
        copiedMilestone.Order.Should().BeNull();
        source.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_RaisesOneEventPerChangedPart()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var newManagerId = Guid.NewGuid();
        var previousRange = roadmap.DateRange;

        // Act
        roadmap.Update("Platform v2", "Platform work", Days(0, 120), [EventManagerId, newManagerId], Visibility.Private, EventManagerId, Actor, Now);

        // Assert
        roadmap.DomainEvents.Should().HaveCount(4);
        var details = roadmap.DomainEvents.OfType<RoadmapDetailsUpdatedEvent>().Single();
        details.Name.Should().Be("Platform v2");
        details.Previous.Should().Be(new RoadmapDetails("Platform", "Platform work"));
        var dates = roadmap.DomainEvents.OfType<RoadmapDateRangeChangedEvent>().Single();
        dates.PreviousDateRange.Should().Be(previousRange);
        dates.DateRange.Should().Be(Days(0, 120));
        var visibility = roadmap.DomainEvents.OfType<RoadmapVisibilityChangedEvent>().Single();
        visibility.PreviousVisibility.Should().Be(Visibility.Public);
        visibility.Visibility.Should().Be(Visibility.Private);
        var managers = roadmap.DomainEvents.OfType<RoadmapManagersChangedEvent>().Single();
        managers.Added.Should().Equal(newManagerId);
        managers.Removed.Should().BeEmpty();
        managers.ManagerIds.Should().BeEquivalentTo([EventManagerId, newManagerId]);
    }

    [Fact]
    public void Update_WithTheSameValuesUntrimmed_RaisesNothing()
    {
        // Arrange
        var roadmap = SavedRoadmap();

        // Act
        var result = roadmap.Update(" Platform ", "Platform work ", roadmap.DateRange, [EventManagerId], roadmap.Visibility, EventManagerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateColors_RaisesColorsChanged_WithBothSets()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        roadmap.UpdateColors([new TestUpsertRoadmapColor { Color = "#4096FF", Name = "Committed", Order = 1, IsDefault = true }], EventManagerId, Actor, Now);
        roadmap.ClearDomainEvents();

        // Act
        roadmap.UpdateColors([new TestUpsertRoadmapColor { Color = "#4096FF", Name = "Planned", Order = 1, IsDefault = true }], EventManagerId, Actor, Now);

        // Assert
        var changed = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapColorsChangedEvent>().Subject;
        changed.PreviousColors.Should().Equal(new RoadmapColorValues("#4096FF", "Committed", 1, true));
        changed.Colors.Should().Equal(new RoadmapColorValues("#4096FF", "Planned", 1, true));
    }

    [Fact]
    public void UpdateColors_WithTheSameSet_RaisesNothing()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        TestUpsertRoadmapColor[] colors = [new() { Color = "#4096FF", Name = "Committed", Order = 1, IsDefault = true }];
        roadmap.UpdateColors(colors, EventManagerId, Actor, Now);
        roadmap.ClearDomainEvents();

        // Act
        roadmap.UpdateColors(colors, EventManagerId, Actor, Now);

        // Assert
        roadmap.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ArchiveThenActivate_RaisesArchivedThenActivated()
    {
        // Arrange
        var roadmap = SavedRoadmap();

        // Act
        roadmap.Archive(EventManagerId, Actor, Now);
        roadmap.Activate(EventManagerId, Actor, Now);

        // Assert
        roadmap.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(RoadmapArchivedEvent), typeof(RoadmapActivatedEvent));
    }

    [Fact]
    public void Delete_RaisesDeleted_WithTheName()
    {
        // Arrange
        var roadmap = SavedRoadmap();

        // Act
        var result = roadmap.Delete(EventManagerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapDeletedEvent>().Subject;
        deleted.Name.Should().Be("Platform");
    }

    [Fact]
    public void Delete_WhenNotAManager_RaisesNothing()
    {
        // Arrange
        var roadmap = SavedRoadmap();

        // Act
        var result = roadmap.Delete(Guid.NewGuid(), Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        roadmap.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CreateActivity_BeneathAParentItOutgrows_RaisesAddedAndTheParentsNewDates()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var parent = AddActivity(roadmap, "Parent", Days(1, 10));
        var upsert = new TestUpsertRoadmapActivity(_activityFaker.WithName("Child").WithDateRange(Days(5, 20)).Generate()) { ParentId = parent.Id };

        // Act
        var child = roadmap.CreateActivity(upsert, EventManagerId, Actor, Now).Value;

        // Assert
        roadmap.DomainEvents.Should().HaveCount(2);
        var added = roadmap.DomainEvents.First().Should().BeOfType<RoadmapActivityAddedEvent>().Subject;
        added.ActivityId.Should().Be(child.Id);
        added.ParentId.Should().Be(parent.Id);
        added.DateRange.Should().Be(Days(5, 20));
        added.Order.Should().Be(1);
        var dates = roadmap.DomainEvents.Last().Should().BeOfType<RoadmapItemDatesChangedEvent>().Subject;
        dates.Changes.Should().Equal(new RoadmapItemDateChange(parent.Id, RoadmapItemType.Activity, Days(1, 10), Days(1, 20)));
    }

    [Fact]
    public void CreateMilestone_AtTheRoot_RaisesOnlyAdded()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var upsert = new TestUpsertRoadmapMilestone(_milestoneFaker.Generate()) { Date = _dateTimeProvider.Today.PlusDays(3), ParentId = null };

        // Act
        var milestone = roadmap.CreateMilestone(upsert, EventManagerId, Actor, Now).Value;

        // Assert
        var added = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapMilestoneAddedEvent>().Subject;
        added.MilestoneId.Should().Be(milestone.Id);
        added.Date.Should().Be(_dateTimeProvider.Today.PlusDays(3));
        added.ParentId.Should().BeNull();
    }

    [Fact]
    public void UpdateRoadmapItemDates_ShiftingAParent_RaisesOneDatesChangedForTheWholeSubtree()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var parent = AddActivity(roadmap, "Parent", Days(1, 10));
        var milestone = AddMilestone(roadmap, _dateTimeProvider.Today.PlusDays(5), parent.Id);

        // Act
        roadmap.UpdateRoadmapItemDates(parent.Id, new TestUpsertRoadmapActivityDateRange(Days(3, 12)), EventManagerId, Actor, Now);

        // Assert
        var dates = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapItemDatesChangedEvent>().Subject;
        dates.Changes.Should().Equal(
            new RoadmapItemDateChange(parent.Id, RoadmapItemType.Activity, Days(1, 10), Days(3, 12)),
            new RoadmapItemDateChange(milestone.Id, RoadmapItemType.Milestone, Days(5, 5), Days(7, 7)));
    }

    [Fact]
    public void UpdateRoadmapItemDates_WithTheSameDates_RaisesNothing()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var activity = AddActivity(roadmap, "Activity", Days(1, 10));

        // Act
        var result = roadmap.UpdateRoadmapItemDates(activity.Id, new TestUpsertRoadmapActivityDateRange(Days(1, 10)), EventManagerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateActivity_ChangingOnlyItsName_RaisesOnlyDetailsUpdated()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var activity = AddActivity(roadmap, "Activity", Days(1, 10));
        var upsert = new TestUpsertRoadmapActivity(activity) { Name = "Renamed" };

        // Act
        roadmap.UpdateActivity(activity.Id, upsert, EventManagerId, Actor, Now);

        // Assert
        var details = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapItemDetailsUpdatedEvent>().Subject;
        details.ItemId.Should().Be(activity.Id);
        details.ItemType.Should().Be(RoadmapItemType.Activity);
        details.Name.Should().Be("Renamed");
        details.Previous.Name.Should().Be("Activity");
    }

    [Fact]
    public void UpdateActivity_WithNothingChanged_RaisesNothing()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var activity = AddActivity(roadmap, "Activity", Days(1, 10));

        // Act
        roadmap.UpdateActivity(activity.Id, new TestUpsertRoadmapActivity(activity), EventManagerId, Actor, Now);

        // Assert
        roadmap.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetActivityOrder_AtTheRoot_RaisesReorderedWithBothOrders()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var first = AddActivity(roadmap, "First", Days(1, 10));
        var second = AddActivity(roadmap, "Second", Days(1, 10));
        var third = AddActivity(roadmap, "Third", Days(1, 10));

        // Act
        roadmap.SetActivityOrder(third.Id, 1, EventManagerId, Actor, Now);

        // Assert
        var reordered = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapActivitiesReorderedEvent>().Subject;
        reordered.ParentId.Should().BeNull();
        reordered.PreviousOrder.Should().Equal(first.Id, second.Id, third.Id);
        reordered.Order.Should().Equal(third.Id, first.Id, second.Id);
    }

    [Fact]
    public void SetActivityOrder_ToItsCurrentPosition_RaisesNothing()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var first = AddActivity(roadmap, "First", Days(1, 10));
        AddActivity(roadmap, "Second", Days(1, 10));

        // Act
        roadmap.SetActivityOrder(first.Id, 1, EventManagerId, Actor, Now);

        // Assert
        roadmap.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MoveActivity_BeneathAnotherActivity_RaisesMovedWithoutAReorder()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var target = AddActivity(roadmap, "Target", Days(1, 30));
        AddActivity(roadmap, "Sibling", Days(1, 10));
        var moving = AddActivity(roadmap, "Moving", Days(2, 8));

        // Act
        var result = roadmap.MoveActivity(moving.Id, target.Id, 1, EventManagerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var moved = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapItemMovedEvent>().Subject;
        moved.ItemId.Should().Be(moving.Id);
        moved.PreviousParentId.Should().BeNull();
        moved.ParentId.Should().Be(target.Id);
        moved.PreviousOrder.Should().Be(3);
        moved.Order.Should().Be(1);
    }

    [Fact]
    public void DeleteItem_AnActivityWithChildren_RaisesDeletedNamingTheDescendants()
    {
        // Arrange
        var roadmap = SavedRoadmap();
        var parent = AddActivity(roadmap, "Parent", Days(1, 30));
        var child = AddActivity(roadmap, "Child", Days(2, 10), parent.Id);
        var milestone = AddMilestone(roadmap, _dateTimeProvider.Today.PlusDays(5), child.Id);

        // Act
        roadmap.DeleteItem(parent.Id, EventManagerId, Actor, Now);

        // Assert
        var deleted = roadmap.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoadmapItemDeletedEvent>().Subject;
        deleted.ItemId.Should().Be(parent.Id);
        deleted.ItemType.Should().Be(RoadmapItemType.Activity);
        deleted.Name.Should().Be("Parent");
        deleted.Descendants.Should().BeEquivalentTo([
            new RoadmapItemReference(child.Id, RoadmapItemType.Activity),
            new RoadmapItemReference(milestone.Id, RoadmapItemType.Milestone)]);
    }

    #endregion Events

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