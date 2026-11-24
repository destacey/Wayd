using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.SignalR;

namespace Wayd.Infrastructure.Tests.Sut.SignalR;

/// <summary>
/// Covers which account, role and desk membership changes <see cref="RealtimeAccessEventHandler"/> rechecks real-time access for,
/// and that a failed recheck never fails the change that triggered it.
/// </summary>
public class RealtimeAccessEventHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 1, 12, 0);

    private readonly Mock<IRealtimeAccessRevoker> _revoker = new();

    private RealtimeAccessEventHandler CreateSut() =>
        new(_revoker.Object, NullLogger<RealtimeAccessEventHandler>.Instance);

    [Fact]
    public async Task Handle_RolesChanged_WithARoleRemoved_RechecksTheUser()
    {
        // Arrange
        var @event = new ApplicationUserRolesChangedEvent("user-1", [], ["role-a"], [], EventActor.System, Now);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.Verify(
            r => r.RecheckUsers(It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "user-1" })), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RolesChanged_WithOnlyRolesAdded_RechecksNothing()
    {
        // Arrange — gaining a role never takes access away.
        var @event = new ApplicationUserRolesChangedEvent("user-1", ["role-a"], [], ["role-a"], EventActor.System, Now);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Permissions.PokerSessions.View")]
    [InlineData("Permissions.StoryMaps.View")]
    public async Task Handle_RolePermissionsChanged_LosingAJoinPermission_RechecksTheRoleMembers(string permission)
    {
        // Arrange
        var @event = new ApplicationRolePermissionsChangedEvent("role-a", [], [permission], [], EventActor.System, Now);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.Verify(r => r.RecheckRoleMembers("role-a", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RolePermissionsChanged_LosingAnUnrelatedPermission_RechecksNothing()
    {
        // Arrange
        var @event = new ApplicationRolePermissionsChangedEvent("role-a", [], ["Permissions.Risks.View"], [], EventActor.System, Now);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_UserDeactivated_EvictsTheUser()
    {
        // Arrange
        var @event = new ApplicationUserDeactivatedEvent("user-1", EventActor.System, Now);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.Verify(r => r.EvictUser("user-1"), Times.Once);
    }

    [Fact]
    public async Task Handle_DeskMembersChanged_WithMembersRemoved_RechecksTheRemovedEmployees()
    {
        // Arrange
        var leaving = Guid.NewGuid();
        var staying = Guid.NewGuid();
        var @event = DeskMembersChanged(
            added: [],
            removed: [new IntakeDeskMemberDetails(leaving, IntakeDeskRole.Agent)],
            roleChanges: [],
            members: [new IntakeDeskMemberDetails(staying, IntakeDeskRole.Admin)]);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.Verify(
            r => r.RecheckEmployees(It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { leaving })), It.IsAny<CancellationToken>()),
            Times.Once);
        _revoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_DeskMembersChanged_WithOnlyAdditionsAndRoleChanges_RechecksNothing()
    {
        // Arrange — every role on a desk facilitates its sessions, so only leaving the desk takes access away.
        var promoted = Guid.NewGuid();
        var joining = Guid.NewGuid();
        var @event = DeskMembersChanged(
            added: [new IntakeDeskMemberDetails(joining, IntakeDeskRole.Agent)],
            removed: [],
            roleChanges: [new IntakeDeskRoleChange(promoted, IntakeDeskRole.Agent, IntakeDeskRole.Admin)],
            members: [new IntakeDeskMemberDetails(promoted, IntakeDeskRole.Admin), new IntakeDeskMemberDetails(joining, IntakeDeskRole.Agent)]);

        // Act
        await CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _revoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_DeskMembersChanged_WhenTheRecheckFails_DoesNotThrow()
    {
        // Arrange — the membership change has already been saved.
        _revoker
            .Setup(r => r.RecheckEmployees(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub unavailable"));
        var @event = DeskMembersChanged([], [new IntakeDeskMemberDetails(Guid.NewGuid(), IntakeDeskRole.Agent)], [], []);

        // Act
        var act = () => CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private static IntakeDeskMembersChangedEvent DeskMembersChanged(
        IReadOnlyList<IntakeDeskMemberDetails> added,
        IReadOnlyList<IntakeDeskMemberDetails> removed,
        IReadOnlyList<IntakeDeskRoleChange> roleChanges,
        IReadOnlyList<IntakeDeskMemberDetails> members) =>
        new(Guid.NewGuid(), 1, added, removed, roleChanges, members, EventActor.System, Now);

    [Fact]
    public async Task Handle_WhenTheRecheckFails_DoesNotThrow()
    {
        // Arrange — the role change has already been saved; failing here would report it as not made.
        _revoker
            .Setup(r => r.RecheckUsers(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub unavailable"));
        var @event = new ApplicationUserRolesChangedEvent("user-1", [], ["role-a"], [], EventActor.System, Now);

        // Act
        var act = () => CreateSut().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
