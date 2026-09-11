using System.Globalization;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

public static class RoleManager
{
    /// <summary>
    /// A role set in the shape every role-carrying event publishes it: role type id to the employees
    /// holding it.
    /// </summary>
    public static Dictionary<int, Guid[]> ToRoleMap<T>(IEnumerable<RoleAssignment<T>> roles) where T : Enum =>
        roles
            .GroupBy(r => Convert.ToInt32(r.Role, CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => g.Select(r => r.EmployeeId).ToArray());

    /// <summary>
    /// The assignments gained and lost between two role maps.
    /// </summary>
    /// <remarks>
    /// A whole-record update replaces the role lists on every save, so most calls change nothing; the
    /// aggregates raise only when either side is non-empty, or they would bury the calls that did change
    /// leadership. Sorted so the same change always produces the same payload.
    /// </remarks>
    public static (RoleAssignmentChange[] Added, RoleAssignmentChange[] Removed) Diff(
        Dictionary<int, Guid[]> before, Dictionary<int, Guid[]> after)
    {
        var beforeSet = Flatten(before);
        var afterSet = Flatten(after);

        return (Ordered(afterSet.Except(beforeSet)), Ordered(beforeSet.Except(afterSet)));

        static HashSet<RoleAssignmentChange> Flatten(Dictionary<int, Guid[]> map) =>
            [.. map.SelectMany(entry => entry.Value.Select(employeeId => new RoleAssignmentChange(entry.Key, employeeId)))];

        static RoleAssignmentChange[] Ordered(IEnumerable<RoleAssignmentChange> changes) =>
            [.. changes.OrderBy(c => c.Role).ThenBy(c => c.EmployeeId)];
    }

    // Assigning and removing one at a time are the building blocks of a whole-set replacement,
    // not an API. Every aggregate replaces its role set through UpdateRoles, which is what keeps
    // role changes to one authorization check and one event describing the net result.
    private static Result AssignRole<T>(HashSet<RoleAssignment<T>> roles, Guid objectId, T role, Guid employeeId) where T : Enum
    {
        Guard.Against.Null(role, nameof(role));
        Guard.Against.Default(employeeId, nameof(employeeId));

        if (!Enum.IsDefined(typeof(T), role))
        {
            return Result.Failure($"Role is not a valid {typeof(T).Name} value.");
        }

        if (roles.Any(r => r.Role.Equals(role) && r.EmployeeId == employeeId))
        {
            return Result.Failure("Employee is already assigned to this role.");
        }

        roles.Add(new RoleAssignment<T>(objectId, role, employeeId));

        return Result.Success();
    }

    private static Result RemoveAssignment<T>(HashSet<RoleAssignment<T>> roles, T role, Guid employeeId) where T : Enum
    {
        Guard.Against.Null(role, nameof(role));
        Guard.Against.Default(employeeId, nameof(employeeId));

        if (!Enum.IsDefined(typeof(T), role))
        {
            return Result.Failure($"Role is not a valid {typeof(T).Name} value.");
        }

        var roleAssignment = roles.FirstOrDefault(r => r.Role.Equals(role) && r.EmployeeId == employeeId);

        if (roleAssignment is null)
        {
            return Result.Failure("Employee is not assigned to this role.");
        }

        roles.Remove(roleAssignment);

        return Result.Success();
    }

    public static Result UpdateRoles<T>(HashSet<RoleAssignment<T>> roles, Guid objectId, Dictionary<T, HashSet<Guid>> updatedRoles) where T : Enum
    {
        var currentRoles = roles
            .GroupBy(r => r.Role)
            .ToDictionary(g => g.Key, g => g.Select(r => r.EmployeeId)
            .ToHashSet());

        foreach (var role in updatedRoles)
        {
            if (!currentRoles.TryGetValue(role.Key, out var currentEmployees))
            {
                currentEmployees = [];
            }


            var employeesToAdd = role.Value.Except(currentEmployees).ToList();
            foreach (var employeeId in employeesToAdd)
            {
                var result = AssignRole(roles, objectId, role.Key, employeeId);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            var employeesToRemove = currentEmployees.Except(role.Value).ToList();
            foreach (var employeeId in employeesToRemove)
            {
                var result = RemoveAssignment(roles, role.Key, employeeId);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            currentRoles.Remove(role.Key);
        }

        // Remove roles that are no longer present in the updated roles
        foreach (var role in currentRoles)
        {
            foreach (var employeeId in role.Value)
            {
                var result = RemoveAssignment(roles, role.Key, employeeId);
                if (result.IsFailure)
                {
                    return result;
                }
            }
        }

        return Result.Success();
    }
}
