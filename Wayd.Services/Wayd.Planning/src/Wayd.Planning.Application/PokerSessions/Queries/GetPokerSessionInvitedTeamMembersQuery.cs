using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;

namespace Wayd.Planning.Application.PokerSessions.Queries;

/// <summary>
/// The active direct members of a team invited to a private poker session, so the session can show who an invite
/// reaches without its reader needing the Teams permission.
/// </summary>
public sealed record GetPokerSessionInvitedTeamMembersQuery(Guid SessionId, Guid TeamId) : IQuery<IReadOnlyList<EmployeeNavigationDto>?>;

/// <summary>
/// Answers null when the caller cannot see the session, the session is public, or the team is not invited to it. A
/// public session's invites admit no one, so they must not become a way to list a team without the Teams permission.
/// </summary>
public sealed class GetPokerSessionInvitedTeamMembersQueryHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDispatcher dispatcher)
    : IQueryHandler<GetPokerSessionInvitedTeamMembersQuery, IReadOnlyList<EmployeeNavigationDto>?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<IReadOnlyList<EmployeeNavigationDto>?> Handle(GetPokerSessionInvitedTeamMembersQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        var invited = await _planningDbContext.PokerSessions
            .Where(s => s.Id == request.SessionId)
            .Where(reader.Filter)
            .Where(s => s.Visibility == Visibility.Private)
            .AnyAsync(s => s.InvitedTeams.Any(t => t.TeamId == request.TeamId), cancellationToken);
        if (!invited)
            return null;

        var memberIds = await _dispatcher.Send(new GetTeamMemberIdsQuery(request.TeamId), cancellationToken);
        if (memberIds.Count == 0)
            return [];

        var employees = await _planningDbContext.Employees
            .AsNoTracking()
            .Where(e => memberIds.Contains(e.Id) && e.IsActive)
            .ToListAsync(cancellationToken);

        return [.. employees
            .Select(EmployeeNavigationDto.From)
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
