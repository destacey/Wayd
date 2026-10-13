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
/// <c>EmployeeDeactivatedEvent</c> is delivered durably (see <c>DurableEventRoutes</c>): at least once and in
/// no guaranteed order. Ending allocations that have already ended changes nothing, so a redelivery is safe.
/// </remarks>
public sealed class EmployeeDeactivatedAllocationHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings,
    ILogger<EmployeeDeactivatedAllocationHandler> logger)
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ILogger<EmployeeDeactivatedAllocationHandler> _logger = logger;

    public async Task Handle(EmployeeDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        // A deactivation delivered after the employee was reactivated would cut the allocations made since.
        var isInactive = await _organizationDbContext.Employees
            .AnyAsync(e => e.Id == @event.Id && !e.IsActive, cancellationToken);
        if (!isInactive)
            return;

        var capacity = await _organizationDbContext.EmployeeCapacities
            .Include(c => c.Allocations)
            .FirstOrDefaultAsync(c => c.Id == @event.Id, cancellationToken);
        if (capacity is null)
            return;

        var settings = await _schedulingSettings.Get(cancellationToken);
        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(settings.DefaultTimeZone) ?? DateTimeZone.Utc;
        var lastDay = @event.Timestamp.InZone(timeZone).Date;

        var result = capacity.EndAllocations(lastDay, @event.Actor, @event.Timestamp);
        if (result.IsFailure)
            throw new InvalidOperationException($"Could not end the allocations of deactivated employee {@event.Id}: {result.Error}");

        await _organizationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ended the allocations of deactivated employee {EmployeeId} after {LastDay}.", @event.Id, lastDay);
    }
}
