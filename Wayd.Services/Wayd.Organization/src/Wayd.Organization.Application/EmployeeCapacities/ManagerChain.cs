namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>Who an employee reports up to: their manager, their manager's manager, and so on to the top.</summary>
public static class ManagerChain
{
    /// <summary>
    /// Whether <paramref name="managerId"/> is anywhere above <paramref name="employeeId"/>, at any depth.
    /// Nobody is above themselves.
    /// </summary>
    /// <param name="managerOf">An employee's manager, null when they have none or are not known.</param>
    /// <remarks>
    /// Walks up from the employee one manager at a time, so it reads only the chain it climbs. Managers arrive
    /// from a people sync and from imports, and nothing stops them forming a loop: the walk stops the first time
    /// it meets someone twice, so a loop grants only the people actually reached before it repeats.
    /// </remarks>
    public static async Task<bool> IsAbove(Guid managerId, Guid employeeId, Func<Guid, Task<Guid?>> managerOf)
    {
        HashSet<Guid> seen = [employeeId];

        var current = employeeId;
        while (await managerOf(current) is { } above)
        {
            if (!seen.Add(above))
                return false;

            if (above == managerId)
                return true;

            current = above;
        }

        return false;
    }
}
