using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities.EventHandlers;

/// <summary>
/// Ends an employee's allocations when they are deactivated, so a leaver stops counting toward their teams'
/// capacity. The last day is the day of the deactivation in the system default time zone: employees carry no
/// departure date.
/// </summary>
/// <remarks>
/// <para>
/// <c>EmployeeDeactivatedEvent</c> is delivered durably (see <c>DurableEventRoutes</c>): at least once and in
/// no guaranteed order. One that arrives after the employee was activated again is skipped: allocations made
/// since belong to that later stretch, and if it has ended too, its own deactivation ends them.
/// </para>
/// <para>
/// An inactive employee's allocations can still be changed, with an end date, because the day they were
/// deactivated is not always the day they left. Once they have been changed since the deactivation, a
/// redelivered or late event must not cut them back, so only allocations with no end are ended: the
/// commands never give an inactive employee one, so any left are what the deactivation still has to end.
/// </para>
/// </remarks>
public sealed class EmployeeDeactivatedAllocationHandler(
    IOrganizationDbContext organizationDbContext,
    IActivityLogDbContext activityLogDbContext,
    ISettings<SchedulingSettings> schedulingSettings,
    ILogger<EmployeeDeactivatedAllocationHandler> logger)
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IActivityLogDbContext _activityLogDbContext = activityLogDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ILogger<EmployeeDeactivatedAllocationHandler> _logger = logger;

    public async Task Handle(EmployeeDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        // Checked against the activity log, not the employee's current state: an employee activated and then
        // deactivated again is inactive now, yet this event is still the earlier, superseded deactivation.
        var deactivatedAt = @event.Timestamp;
        var activatedSince = await _activityLogDbContext.ActivityLogs
            .AnyAsync(a => a.AggregateId == @event.Id
                && a.EventType == nameof(EmployeeActivatedEvent)
                && a.Timestamp > deactivatedAt,
                cancellationToken);
        if (activatedSince)
            return;

        // Strictly later: this handler's own change is stamped with the deactivation's timestamp.
        var changedSince = await _activityLogDbContext.ActivityLogs
            .AnyAsync(a => a.AggregateId == @event.Id
                && a.EventType == nameof(EmployeeAllocationsChangedEvent)
                && a.Timestamp > deactivatedAt,
                cancellationToken);

        var capacity = await _organizationDbContext.EmployeeCapacities
            .Include(c => c.Allocations)
            .FirstOrDefaultAsync(c => c.Id == @event.Id, cancellationToken);
        if (capacity is null)
            return;

        var settings = await _schedulingSettings.Get(cancellationToken);
        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(settings.DefaultTimeZone) ?? DateTimeZone.Utc;
        var lastDay = @event.Timestamp.InZone(timeZone).Date;

        var result = changedSince
            ? capacity.EndOpenAllocations(lastDay, @event.Actor, @event.Timestamp)
            : capacity.EndAllocations(lastDay, @event.Actor, @event.Timestamp);
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not end the allocations of deactivated employee {@event.Id}: {result.Error}");

        await _organizationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ended the allocations of deactivated employee {EmployeeId} after {LastDay}.", @event.Id, lastDay);
    }
}
