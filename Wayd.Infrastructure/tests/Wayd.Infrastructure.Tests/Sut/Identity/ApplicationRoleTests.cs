using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Interfaces;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

public sealed class ApplicationRoleTests
{
    private static readonly EventActor Actor = EventActor.User("admin-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 25, 12, 0);

    private static IReadOnlyCollection<DomainEvent> RaisedEvents(IEntity entity) => entity.DomainEvents;

    [Fact]
    public void Update_ShouldCarryThePreviousDetails_WhenRenamed()
    {
        // Arrange
        var role = new ApplicationRole("Planner", "Plans things");

        // Act
        role.Update("Lead Planner", "Plans things", Actor, Now);

        // Assert
        var updated = RaisedEvents(role).Should().ContainSingle().Which.Should().BeOfType<ApplicationRoleDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("Lead Planner");
        updated.Previous.Should().Be(new ApplicationRoleDetails("Planner", "Plans things"));
    }

    [Fact]
    public void Update_ShouldRaiseNothing_WhenTheValuesNormaliseToWhatIsStored()
    {
        // Arrange — the comparison runs after assignment, so trailing whitespace is not a change.
        var role = new ApplicationRole("Planner", "Plans things");

        // Act
        role.Update("Planner ", " Plans things", Actor, Now);

        // Assert
        RaisedEvents(role).Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldRaiseNothing_WhenAnEmptyDescriptionReplacesNone()
    {
        // Arrange
        var role = new ApplicationRole("Planner");

        // Act
        role.Update("Planner", "  ", Actor, Now);

        // Assert
        role.Description.Should().BeNull();
        RaisedEvents(role).Should().BeEmpty();
    }

    [Fact]
    public void RecordPermissionsChange_ShouldCarryTheChangeAndTheSetAfterwards()
    {
        // Arrange
        var role = new ApplicationRole("Planner");

        // Act
        role.RecordPermissionsChange(["Permissions.Projects.View", "Permissions.Projects.Update"],
            ["Permissions.Projects.View", "Permissions.Risks.View"], Actor, Now);

        // Assert
        var changed = RaisedEvents(role).Should().ContainSingle().Which.Should().BeOfType<ApplicationRolePermissionsChangedEvent>().Subject;
        changed.Added.Should().Equal("Permissions.Risks.View");
        changed.Removed.Should().Equal("Permissions.Projects.Update");
        changed.Permissions.Should().Equal("Permissions.Projects.View", "Permissions.Risks.View");
    }

    [Fact]
    public void RecordPermissionsChange_ShouldRaiseNothing_WhenThePermissionsAreUnchanged()
    {
        // Arrange
        var role = new ApplicationRole("Planner");

        // Act
        role.RecordPermissionsChange(["Permissions.Projects.View"], ["Permissions.Projects.View"], Actor, Now);

        // Assert
        RaisedEvents(role).Should().BeEmpty();
    }

    [Fact]
    public void RecordDeletion_ShouldCarryTheName()
    {
        // Arrange
        var role = new ApplicationRole("Planner");

        // Act
        role.RecordDeletion(Actor, Now);

        // Assert
        var deleted = RaisedEvents(role).Should().ContainSingle().Which.Should().BeOfType<ApplicationRoleDeletedEventV2>().Subject;
        deleted.RoleId.Should().Be(role.Id);
        deleted.Name.Should().Be("Planner");
    }
}
