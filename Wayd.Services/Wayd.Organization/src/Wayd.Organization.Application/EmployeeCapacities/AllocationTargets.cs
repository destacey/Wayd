using Wayd.Common.Domain.Employees;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>A team and role a command touches, and whether it gives them any of the employee's time.</summary>
/// <param name="Allocates">False when the command only takes time away, which an inactive team or role allows.</param>
/// <param name="OpenEnded">True when the time given has no last day.</param>
internal sealed record AllocationTarget(Guid TeamId, Guid? RoleId, bool Allocates, bool OpenEnded);

/// <summary>What the allocation commands share: checking who and what is being allocated, and loading the record.</summary>
internal static class AllocationTargets
{
    public const string ChangedElsewhereError = EmployeeCapacityRecords.ChangedElsewhereError;

    public const string InactiveEmployeeOpenEndedError = "An inactive employee's allocations must have an end date.";

    /// <summary>
    /// Fails when the employee, a team or a role does not exist, when time is being given to a team or in a role
    /// that is inactive, or when an inactive employee is given time with no last day. Time can always be taken
    /// away.
    /// </summary>
    /// <remarks>
    /// An inactive employee can be allocated so their history can be filled in or corrected: employees carry no
    /// leaving date, and the day they were deactivated is not always the day they left. Every such allocation
    /// must end, because the capacity report takes an inactive employee's last allocated day as the day they
    /// left, and an open-ended one would count them toward the team for good.
    /// </remarks>
    public static async Task<Result> Validate(IOrganizationDbContext organizationDbContext, Guid employeeId, IReadOnlyCollection<AllocationTarget> targets, CancellationToken cancellationToken)
    {
        var employee = await organizationDbContext.Employees
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
            return Result.Failure($"Employee {employeeId} not found.");

        if (!employee.IsActive && targets.Any(t => t.Allocates && t.OpenEnded))
            return Result.Failure(InactiveEmployeeOpenEndedError);

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

    /// <summary>The employee's capacity record with its allocations, new and untracked when they have none yet.</summary>
    public static Task<(EmployeeCapacity Capacity, bool IsNew)> LoadOrCreateCapacity(IOrganizationDbContext organizationDbContext, Guid employeeId, CancellationToken cancellationToken) =>
        EmployeeCapacityRecords.LoadOrCreate(organizationDbContext, employeeId, CapacityPart.Allocations, cancellationToken);

    /// <summary>Saves a change to the employee's allocations, failing when someone else changed their record first.</summary>
    public static Task<Result> Save(IOrganizationDbContext organizationDbContext, EmployeeCapacity capacity, bool isNew, CancellationToken cancellationToken) =>
        EmployeeCapacityRecords.Save(organizationDbContext, capacity, isNew, cancellationToken);
}
