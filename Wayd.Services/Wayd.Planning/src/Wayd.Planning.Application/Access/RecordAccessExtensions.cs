using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Planning.Application.Access;

/// <summary>
/// What the records with people on them (poker sessions, story maps) share: checking the people and teams a
/// command names, and resolving team membership the way sprints do.
/// </summary>
public static class RecordAccessExtensions
{
    /// <summary>
    /// Fails unless every id is an existing employee, so a role or invite cannot name someone unknown.
    /// </summary>
    public static async Task<Result> EnsureEmployeesExist(this IPlanningDbContext planningDbContext, IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
            return Result.Success();

        var distinct = employeeIds.Distinct().ToList();
        var found = await planningDbContext.Employees.CountAsync(e => distinct.Contains(e.Id), cancellationToken);

        return found == distinct.Count
            ? Result.Success()
            : Result.Failure("One or more employees were not found.");
    }

    /// <summary>
    /// Fails unless every id is an existing team.
    /// </summary>
    public static async Task<Result> EnsureTeamsExist(this IPlanningDbContext planningDbContext, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
            return Result.Success();

        var distinct = teamIds.Distinct().ToList();
        var found = await planningDbContext.PlanningTeams.CountAsync(t => distinct.Contains(t.Id), cancellationToken);

        return found == distinct.Count
            ? Result.Success()
            : Result.Failure("One or more teams were not found.");
    }

    /// <summary>
    /// Which of <paramref name="teamIds"/> the employee belongs to today, directly or through a team of teams, as
    /// <c>IsTeamMemberQuery</c> decides. Asks nothing when there are no teams.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> TeamsWithMember(this IDispatcher dispatcher, IReadOnlyCollection<Guid> teamIds, Guid employeeId, LocalDate today, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
            return new HashSet<Guid>();

        return await dispatcher.Send(new GetTeamsWithMemberQuery(teamIds, employeeId, today), cancellationToken);
    }

    /// <summary>
    /// A team's active direct members by name, so a record can show who a team invite reaches without its reader
    /// needing the Teams permission. The caller decides whether the reader may see the list.
    /// </summary>
    public static async Task<IReadOnlyList<EmployeeNavigationDto>> ActiveTeamMembers(this IPlanningDbContext planningDbContext, IDispatcher dispatcher, Guid teamId, CancellationToken cancellationToken)
    {
        var memberIds = await dispatcher.Send(new GetTeamMemberIdsQuery(teamId), cancellationToken);
        if (memberIds.Count == 0)
            return [];

        var employees = await planningDbContext.Employees
            .AsNoTracking()
            .Where(e => memberIds.Contains(e.Id) && e.IsActive)
            .ToListAsync(cancellationToken);

        return [.. employees
            .Select(EmployeeNavigationDto.From)
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
