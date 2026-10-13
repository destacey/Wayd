using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Organization.Application.EmployeeCapacities.EventHandlers;

/// <summary>
/// Ends every employee's allocations to a team or team of teams when it is deactivated, so time on an inactive
/// team stops counting toward each person's 100% and toward the team's capacity. The last day is the team's
/// inactive date.
/// </summary>
/// <remarks>
/// <para>
/// <c>TeamDeactivatedEvent</c> is delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order. Only employees with time on the team after its inactive date are touched, so a redelivery
/// finds no one. A deactivation that arrives after the team was activated again is skipped: allocations made
/// since belong to that later stretch, and if it has ended too, its own deactivation ends them.
/// </para>
/// <para>
/// Each employee is saved on their own, so one whose allocations someone else is changing does not hold up or
/// undo the rest. That employee is reloaded and tried again; those still failing are reported together once
/// everyone has been tried, and the redelivery that follows takes up only what is left.
/// </para>
/// <para>
/// It runs whether or not Team Capacity Management is on: skipping an event while the feature is off would
/// leave those allocations in place for good once it is turned back on.
/// </para>
/// <para>
/// One gap is left open. An allocation command checks that the team is active and saves without holding that
/// check, so one that passed it before the deactivation and saves after this handler has finished leaves an
/// allocation on the inactive team. Reading who is allocated a second time narrows that to a save landing
/// after the whole handler; closing it would mean the command locking the team.
/// </para>
/// </remarks>
public sealed class TeamDeactivatedAllocationHandler(
    IOrganizationDbContext organizationDbContext,
    IActivityLogDbContext activityLogDbContext,
    ILogger<TeamDeactivatedAllocationHandler> logger)
{
    /// <summary>How many times one employee's allocations are loaded and saved before they are reported as failed.</summary>
    public const int MaxAttemptsPerEmployee = 3;

    /// <summary>How many of the employees that failed are named in the exception, so a large team's stays readable.</summary>
    private const int ReportedFailures = 5;

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IActivityLogDbContext _activityLogDbContext = activityLogDbContext;
    private readonly ILogger<TeamDeactivatedAllocationHandler> _logger = logger;

    public async Task Handle(TeamDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        // Checked against the activity log, not the team's current state: a team activated and then
        // deactivated again is inactive now, yet this event is still the earlier, superseded deactivation.
        var deactivatedAt = @event.Timestamp;
        var activatedSince = await _activityLogDbContext.ActivityLogs
            .AnyAsync(a => a.AggregateId == @event.Id
                && a.EventType == nameof(TeamActivatedEvent)
                && a.Timestamp > deactivatedAt,
                cancellationToken);
        if (activatedSince)
            return;

        var teamId = @event.Id;
        var lastDay = @event.InactiveDate;
        var ended = 0;
        HashSet<Guid> tried = [];
        List<Guid> failed = [];

        // The second pass takes up anyone allocated between the first read and the end of the first pass: a
        // change that found the team still active can save after the team was deactivated.
        for (var pass = 1; pass <= 2; pass++)
        {
            var employeeIds = await _organizationDbContext.EmployeeTeamAllocations
                .Where(a => a.TeamId == teamId && (a.End == null || a.End > lastDay))
                .Select(a => a.EmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var employeeId in employeeIds.Where(tried.Add))
            {
                switch (await EndAllocations(employeeId, @event, cancellationToken))
                {
                    case Outcome.Ended:
                        ended++;
                        break;
                    case Outcome.Failed:
                        failed.Add(employeeId);
                        break;
                }
            }
        }

        if (failed.Count > 0)
        {
            _logger.LogWarning("Ended the allocations of {EmployeeCount} employees to deactivated team {TeamId} after {LastDay}; {FailedCount} could not be ended.", ended, teamId, lastDay, failed.Count);

            throw new InvalidOperationException($"Could not end the allocations of {failed.Count} employees to deactivated team {teamId}, starting with {string.Join(", ", failed.Take(ReportedFailures))}.");
        }

        if (ended > 0)
            _logger.LogInformation("Ended the allocations of {EmployeeCount} employees to deactivated team {TeamId} after {LastDay}.", ended, teamId, lastDay);
    }

    private enum Outcome
    {
        Ended,
        NothingToEnd,
        Failed,
    }

    private async Task<Outcome> EndAllocations(Guid employeeId, TeamDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttemptsPerEmployee; attempt++)
        {
            var capacity = await _organizationDbContext.EmployeeCapacities
                .Include(c => c.Allocations)
                .FirstOrDefaultAsync(c => c.Id == employeeId, cancellationToken);
            if (capacity is null)
                return Outcome.NothingToEnd;

            var result = capacity.EndAllocationsTo(@event.Id, @event.InactiveDate, @event.Actor, @event.Timestamp);
            if (result.IsFailure)
            {
                _logger.LogError("Could not end the allocations of employee {EmployeeId} to deactivated team {TeamId}: {Error}", employeeId, @event.Id, result.Error);
                return Outcome.Failed;
            }

            // Someone else ended them between the read of who is allocated and this load.
            if (capacity.DomainEvents.Count == 0)
                return Outcome.NothingToEnd;

            try
            {
                await _organizationDbContext.SaveChangesAsync(cancellationToken);
                return Outcome.Ended;
            }
            catch (DbUpdateConcurrencyException)
            {
                // The context is shared with every other handler of this event, and the next save on it would
                // write this employee's stale allocations again. Clearing also lets the next attempt load them
                // as they now stand.
                _organizationDbContext.ChangeTracker.Clear();

                _logger.LogWarning("The allocations of employee {EmployeeId} changed while ending those to deactivated team {TeamId}. Attempt {Attempt} of {MaxAttempts}.", employeeId, @event.Id, attempt, MaxAttemptsPerEmployee);
            }
        }

        return Outcome.Failed;
    }
}
