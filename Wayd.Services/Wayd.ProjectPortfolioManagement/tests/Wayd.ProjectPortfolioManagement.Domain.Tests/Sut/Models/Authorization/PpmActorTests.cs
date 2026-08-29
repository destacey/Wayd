using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models.Authorization;

/// <summary>
/// Covers <see cref="PpmActor.ToEventActor"/>, which derives the event-envelope attribution from the actor
/// that authorized the change, so the two can never disagree.
/// </summary>
public sealed class PpmActorTests
{
    [Fact]
    public void ToEventActor_ForAnOrdinaryActor_AttributesTheUser()
    {
        // Arrange
        var actor = new PpmActor(Guid.NewGuid(), IsPpmAdministrator: false, "user-1");

        // Act
        var eventActor = actor.ToEventActor();

        // Assert
        eventActor.Kind.Should().Be(EventActorKind.User);
        eventActor.UserId.Should().Be("user-1");
    }

    [Fact]
    public void ToEventActor_ForAnAdministrator_StillAttributesTheUser()
    {
        // Arrange — the administrator grant substitutes for role membership, not for identity: the change
        // was still made by a person and a notification should say so.
        var actor = new PpmActor(Guid.NewGuid(), IsPpmAdministrator: true, "user-2");

        // Act
        var eventActor = actor.ToEventActor();

        // Assert
        eventActor.Kind.Should().Be(EventActorKind.User);
        eventActor.UserId.Should().Be("user-2");
    }

    [Fact]
    public void ToEventActor_ForTheSystemActor_AttributesThePlatform()
    {
        // Arrange — importers and replication paths pass PpmActor.System, so they inherit correct system
        // attribution without each call site having to say so again.
        var actor = PpmActor.System;

        // Act
        var eventActor = actor.ToEventActor();

        // Assert
        eventActor.Kind.Should().Be(EventActorKind.System);
        eventActor.UserId.Should().Be(SystemUser.Id);
        eventActor.HasOriginatingUser.Should().BeFalse();
    }

    [Fact]
    public void ToEventActor_ForAnActorCarryingTheSystemUserId_AttributesThePlatform()
    {
        // Arrange — a background scope resolves the system user id through the ordinary actor path; it
        // must still be recognised as the platform rather than reported as a signed-in user.
        var actor = new PpmActor(Guid.Empty, IsPpmAdministrator: true, SystemUser.Id);

        // Act
        var eventActor = actor.ToEventActor();

        // Assert
        eventActor.Kind.Should().Be(EventActorKind.System);
    }
}
