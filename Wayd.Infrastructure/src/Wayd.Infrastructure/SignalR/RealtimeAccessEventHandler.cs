using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Events.Identity;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Evicts people from real-time groups when a change to their account or roles takes away their access to them.
/// </summary>
/// <remarks>
/// The events are delivered inline, after the change has committed. A failure here is logged rather than thrown:
/// the change it reacts to has already been saved, and failing the request would report it as not made.
/// </remarks>
public sealed class RealtimeAccessEventHandler(IRealtimeAccessRevoker revoker, ILogger<RealtimeAccessEventHandler> logger)
{
    private readonly IRealtimeAccessRevoker _revoker = revoker;
    private readonly ILogger<RealtimeAccessEventHandler> _logger = logger;

    /// <summary>A user who lost a role may have lost a join permission with it.</summary>
    public Task Handle(ApplicationUserRolesChangedEvent @event, CancellationToken cancellationToken) =>
        @event.Removed.Length == 0
            ? Task.CompletedTask
            : Guarded(() => _revoker.RecheckUsers([@event.UserId], cancellationToken), @event);

    /// <summary>Everyone in a role that lost a join permission may have lost it.</summary>
    public Task Handle(ApplicationRolePermissionsChangedEvent @event, CancellationToken cancellationToken) =>
        @event.Removed.Any(IRealtimeAccessRevoker.JoinPermissions.Contains)
            ? Guarded(() => _revoker.RecheckRoleMembers(@event.RoleId, cancellationToken), @event)
            : Task.CompletedTask;

    /// <summary>A deactivated account keeps nothing open.</summary>
    public Task Handle(ApplicationUserDeactivatedEvent @event, CancellationToken cancellationToken) =>
        Guarded(() => _revoker.EvictUser(@event.UserId), @event);

    private async Task Guarded(Func<Task> revoke, IEvent @event)
    {
        try
        {
            await revoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evict real-time connections after {Event}.", @event.GetType().Name);
        }
    }
}
