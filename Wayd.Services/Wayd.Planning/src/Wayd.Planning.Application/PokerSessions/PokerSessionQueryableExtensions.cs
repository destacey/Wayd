using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions;

public static class PokerSessionQueryableExtensions
{
    /// <summary>
    /// Loads what <see cref="PokerSession.CanAccess"/> and <see cref="PokerSession.CanManage"/> read. Without it a
    /// session has no facilitators or invitees, which silently refuses everyone but administrators.
    /// </summary>
    public static IQueryable<PokerSession> IncludeAccess(this IQueryable<PokerSession> query) =>
        query
            .Include(s => s.Facilitators)
            .Include(s => s.InvitedEmployees)
            .Include(s => s.InvitedTeams);

    /// <summary>
    /// A member as the session's live updates send them, reading the member's employee for their name.
    /// </summary>
    public static async Task<PokerSessionMemberDto> MemberDto(this IPlanningDbContext planningDbContext, PokerSessionMember member, CancellationToken cancellationToken)
    {
        var employee = await planningDbContext.Employees
            .AsNoTracking()
            .FirstAsync(e => e.Id == member.EmployeeId, cancellationToken);

        return PokerSessionMemberDto.From(member, employee);
    }

    /// <summary>
    /// Fails unless every id is an existing employee, so a facilitator or invite cannot name someone unknown.
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
}
