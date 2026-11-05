using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PokerSessions.Queries;

public sealed record GetPokerSessionsQuery(PokerSessionStatus? Status = null) : IQuery<IReadOnlyList<PokerSessionListDto>>;

/// <summary>
/// Lists the poker sessions the caller can see, newest first.
/// </summary>
public sealed class GetPokerSessionsQueryHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access) : IQueryHandler<GetPokerSessionsQuery, IReadOnlyList<PokerSessionListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;

    public async Task<IReadOnlyList<PokerSessionListDto>> Handle(GetPokerSessionsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        var query = _planningDbContext.PokerSessions.Where(reader.Filter);

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        var sessions = await query
            .OrderByDescending(s => s.Key)
            .ProjectToType<PokerSessionListDto>()
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.CanManage = reader.CanManage(session.Facilitators.Select(f => f.Id));

        return sessions;
    }
}
