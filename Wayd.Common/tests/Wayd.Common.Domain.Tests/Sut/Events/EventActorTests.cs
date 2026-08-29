using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// Covers <see cref="EventActor"/> — the "who caused this" half of the domain event envelope.
/// </summary>
public sealed class EventActorTests
{
    [Fact]
    public void System_IsAttributedToTheWellKnownSystemUserAndNamesNoOriginator()
    {
        // Arrange / Act
        var actor = EventActor.System;

        // Assert
        actor.Kind.Should().Be(EventActorKind.System);
        actor.UserId.Should().Be(SystemUser.Id);
        actor.HasOriginatingUser.Should().BeFalse("the platform acting on its own behalf has no person to name");
    }

    [Fact]
    public void User_CarriesTheActingAccount()
    {
        // Arrange / Act
        var actor = EventActor.User("user-1");

        // Assert
        actor.Kind.Should().Be(EventActorKind.User);
        actor.UserId.Should().Be("user-1");
        actor.HasOriginatingUser.Should().BeTrue();
    }

    [Fact]
    public void Import_SeparatesTheMechanismFromThePersonWhoStartedIt()
    {
        // Arrange / Act — the distinction the envelope exists for: an import run BY someone is not that
        // person editing each record by hand.
        var actor = EventActor.Import("alice");

        // Assert
        actor.Kind.Should().Be(EventActorKind.Import);
        actor.UserId.Should().Be("alice");
        actor.HasOriginatingUser.Should().BeTrue("a notification can still say who set the import running");
        actor.Should().NotBe(EventActor.User("alice"), "an import is not the same as a direct edit by the same person");
    }

    [Fact]
    public void Sync_WithNoOriginator_NamesTheMechanismButNoPerson()
    {
        // Arrange / Act — a scheduled sync nobody triggered.
        var actor = EventActor.Sync(null);

        // Assert
        actor.Kind.Should().Be(EventActorKind.Sync);
        actor.UserId.Should().BeNull();
        actor.HasOriginatingUser.Should().BeFalse();
    }

    [Fact]
    public void Sync_WithAnOriginator_KeepsTheTriggeringAccount()
    {
        // Arrange / Act
        var actor = EventActor.Sync("user-2");

        // Assert
        actor.Kind.Should().Be(EventActorKind.Sync);
        actor.UserId.Should().Be("user-2");
        actor.HasOriginatingUser.Should().BeTrue();
    }

    [Fact]
    public void Anonymous_NamesNoPerson()
    {
        // Arrange / Act — a live request with no authenticated user.
        var actor = EventActor.Anonymous;

        // Assert
        actor.Kind.Should().Be(EventActorKind.Anonymous);
        actor.UserId.Should().BeNull();
        actor.HasOriginatingUser.Should().BeFalse();
    }
}
