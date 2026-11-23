namespace Wayd.ProjectPortfolioManagement.Application.Common;

/// <summary>
/// Checks the employees a command names in a role or an assignment. Each is a foreign key to the employee, so
/// an unknown one would otherwise be refused only when the change is saved, as an exception rather than an
/// answer the caller can act on.
/// </summary>
internal static class PpmEmployees
{
    /// <summary>Fails unless every id is an existing employee.</summary>
    public static async Task<Result> EnsureEmployeesExist(this IProjectPortfolioManagementDbContext dbContext, IEnumerable<Guid> employeeIds, CancellationToken cancellationToken)
    {
        var requested = employeeIds.Distinct().ToList();
        if (requested.Count == 0)
            return Result.Success();

        var found = await dbContext.Employees.CountAsync(e => requested.Contains(e.Id), cancellationToken);

        return found == requested.Count
            ? Result.Success()
            : Result.Failure("One or more employees were not found.");
    }
}
