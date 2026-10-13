using Wayd.Common.Domain.Employees;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>A team and role a command touches, and whether it gives them any of the employee's time.</summary>
/// <param name="Allocates">False when the command only takes time away, which an inactive team or role allows.</param>
internal sealed record AllocationTarget(Guid TeamId, Guid? RoleId, bool Allocates);

/// <summary>What the allocation commands share: checking who and what is being allocated, and loading the record.</summary>
internal static class AllocationTargets
{
    public const string ChangedElsewhereError = "The employee's allocations were changed by someone else. Reload them and try again.";

    /// <summary>
    /// Fails when the employee, a team or a role does not exist, or when time is being given to or by one that
    /// is inactive. Time can always be taken away.
    /// </summary>
    public static async Task<Result> Validate(IOrganizationDbContext organizationDbContext, Guid employeeId, IReadOnlyCollection<AllocationTarget> targets, CancellationToken cancellationToken)
    {
        var employee = await organizationDbContext.Employees
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
            return Result.Failure($"Employee {employeeId} not found.");

        var allocates = targets.Any(t => t.Allocates);
        if (allocates && !employee.IsActive)
            return Result.Failure("An inactive employee cannot be allocated to a team.");

        var teamIds = targets.Select(t => t.TeamId).Distinct().ToList();
        var teams = await organizationDbContext.BaseTeams
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.IsActive })
            .ToListAsync(cancellationToken);

        foreach (var teamId in teamIds)
        {
            var team = teams.Find(t => t.Id == teamId);
            if (team is null)
                return Result.Failure($"Team {teamId} not found.");

            if (!team.IsActive && targets.Any(t => t.TeamId == teamId && t.Allocates))
                return Result.Failure($"Employees cannot be allocated to inactive teams. {team.Name} is inactive.");
        }

        var roleIds = targets.Select(t => t.RoleId).OfType<Guid>().Distinct().ToList();
        var roles = await organizationDbContext.TeamMemberRoles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name, r.IsActive })
            .ToListAsync(cancellationToken);

        foreach (var roleId in roleIds)
        {
            var role = roles.Find(r => r.Id == roleId);
            if (role is null)
                return Result.Failure($"Team member role {roleId} not found.");

            if (!role.IsActive && targets.Any(t => t.RoleId == roleId && t.Allocates))
                return Result.Failure($"Employees cannot be allocated in an inactive role. {role.Name} is inactive.");
        }

        return Result.Success();
    }

    /// <summary>
    /// The employee's capacity record with its allocations. When they have none yet it is new and not tracked:
    /// <see cref="Save"/> adds it once it holds something.
    /// </summary>
    public static async Task<(EmployeeCapacity Capacity, bool IsNew)> LoadOrCreateCapacity(IOrganizationDbContext organizationDbContext, Guid employeeId, CancellationToken cancellationToken)
    {
        var capacity = await organizationDbContext.EmployeeCapacities
            .Include(c => c.Allocations)
            .FirstOrDefaultAsync(c => c.Id == employeeId, cancellationToken);

        return capacity is not null
            ? (capacity, false)
            : (EmployeeCapacity.Create(employeeId), true);
    }

    /// <summary>
    /// Saves a change to the employee's allocations. A call that changed nothing saves nothing, so no empty
    /// record is left behind. Fails with <see cref="ChangedElsewhereError"/> when someone else changed the
    /// allocations first, including by creating the employee's record at the same moment.
    /// </summary>
    public static async Task<Result> Save(IOrganizationDbContext organizationDbContext, EmployeeCapacity capacity, bool isNew, CancellationToken cancellationToken)
    {
        if (capacity.DomainEvents.Count == 0)
            return Result.Success();

        try
        {
            if (isNew)
                await organizationDbContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);

            await organizationDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ChangedElsewhereError);
        }
        // The record's id is the employee's, so a failed insert of a new one is a second writer's having
        // inserted it first.
        catch (DbUpdateException) when (isNew)
        {
            return Result.Failure(ChangedElsewhereError);
        }
    }
}
