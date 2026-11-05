using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions;

/// <summary>What the poker session handlers share: loading access, and a member as live updates send them.</summary>
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
}
