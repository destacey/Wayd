using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Interfaces;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

public sealed class ApplicationUserTests
{
    private static readonly EventActor Actor = EventActor.User("admin-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 25, 12, 0);

    private static ApplicationUser CreateUser() => new()
    {
        FirstName = "Avery",
        LastName = "Quinn",
        Email = "avery@acme.example",
        PhoneNumber = "555-0100",
        IsActive = true,
        LoginProvider = LoginProviders.MicrosoftEntraId,
    };

    private static IReadOnlyCollection<DomainEvent> RaisedEvents(IEntity entity) => entity.DomainEvents;

    [Fact]
    public void UpdateDetails_ShouldRaiseDetailsUpdated_WhenAFieldChanges()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.UpdateDetails("Avery", "Quinn-Lee", "avery@acme.example", "555-0100", Actor, Now);

        // Assert
        user.LastName.Should().Be("Quinn-Lee");
        var updated = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserDetailsUpdatedEvent>().Subject;
        updated.UserId.Should().Be(user.Id);
        updated.Actor.Should().Be(Actor);
        updated.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseNothing_WhenNothingChanges()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.UpdateDetails("Avery", "Quinn", "avery@acme.example", "555-0100", Actor, Now);

        // Assert
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public void ChangeEmployeeLink_ShouldCarryBothEnds()
    {
        // Arrange
        var user = CreateUser();
        var previous = Guid.NewGuid();
        var next = Guid.NewGuid();
        user.EmployeeId = previous;

        // Act
        user.ChangeEmployeeLink(next, Actor, Now);

        // Assert
        user.EmployeeId.Should().Be(next);
        var changed = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserEmployeeLinkChangedEvent>().Subject;
        changed.PreviousEmployeeId.Should().Be(previous);
        changed.EmployeeId.Should().Be(next);
    }

    [Fact]
    public void ChangeEmployeeLink_ShouldRaiseNothing_WhenTheLinkIsUnchanged()
    {
        // Arrange
        var user = CreateUser();
        user.EmployeeId = Guid.NewGuid();

        // Act
        user.ChangeEmployeeLink(user.EmployeeId, Actor, Now);

        // Assert
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public void RecordRolesChange_ShouldCarryTheChangeAndTheSetAfterwards()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.RecordRolesChange(["role-b", "role-a"], ["role-c", "role-a"], Actor, Now);

        // Assert
        var changed = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserRolesChangedEvent>().Subject;
        changed.Added.Should().Equal("role-c");
        changed.Removed.Should().Equal("role-b");
        changed.Roles.Should().Equal("role-a", "role-c");
    }

    [Fact]
    public void RecordRolesChange_ShouldRaiseNothing_WhenTheSetsHoldTheSameRoles()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.RecordRolesChange(["role-a", "role-b"], ["role-b", "role-a"], Actor, Now);

        // Assert
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public void Activate_ShouldRaiseNothing_WhenAlreadyActive()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.Activate(Actor, Now);

        // Assert
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_ShouldRaiseDeactivated_WhenActive()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.Deactivate(Actor, Now);

        // Assert
        user.IsActive.Should().BeFalse();
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserDeactivatedEvent>();
    }

    [Fact]
    public void CancelTenantMigration_ShouldRaiseNothing_WhenNothingIsStaged()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.CancelTenantMigration(Actor, Now);

        // Assert
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public void CompleteTenantMigration_ShouldClearTheStagedMigration()
    {
        // Arrange
        var user = CreateUser();
        user.StageTenantMigration("tenant-b", Actor, Now);
        user.ClearDomainEvents();

        // Act
        user.CompleteTenantMigration("tenant-b", EventActor.System, Now);

        // Assert
        user.PendingMigrationTenantId.Should().BeNull();
        user.PendingMigrationStagedAt.Should().BeNull();
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserTenantMigrationCompletedEvent>()
            .Which.TenantId.Should().Be("tenant-b");
    }

    [Fact]
    public void StageProviderMigration_ShouldRaiseNothing_WhenTheSameTargetIsAlreadyStaged()
    {
        // Arrange
        var user = CreateUser();
        user.PendingMigrationProviderId = "Acme-Okta";

        // Act
        user.StageProviderMigration("Acme-Okta", Actor, Now);

        // Assert
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public void CompleteProviderMigration_ShouldCarryBothProviders()
    {
        // Arrange
        var user = CreateUser();
        user.PendingMigrationProviderId = "Acme-Okta";

        // Act
        user.CompleteProviderMigration("Acme-Okta", EventActor.System, Now);

        // Assert
        user.LoginProvider.Should().Be("Acme-Okta");
        user.PendingMigrationProviderId.Should().BeNull();
        var completed = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserProviderMigrationCompletedEvent>().Subject;
        completed.FromProvider.Should().Be(LoginProviders.MicrosoftEntraId);
        completed.ToProvider.Should().Be("Acme-Okta");
    }

    [Fact]
    public void ConvertToLocalAccount_ShouldRecordTheCanceledMigrationFirst_WhenOneIsStaged()
    {
        // Arrange
        var user = CreateUser();
        user.PendingMigrationProviderId = "Acme-Okta";

        // Act
        user.ConvertToLocalAccount(Actor, Now);

        // Assert
        user.LoginProvider.Should().Be(LoginProviders.Wayd);
        user.MustChangePassword.Should().BeTrue();
        user.PendingMigrationProviderId.Should().BeNull();
        RaisedEvents(user).Select(e => e.GetType()).Should().Equal(
            typeof(ApplicationUserProviderMigrationCanceledEvent),
            typeof(ApplicationUserConvertedToLocalAccountEvent));
    }
}
