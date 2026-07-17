using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PokerSessions.Queries;

public sealed record GetPokerSessionsQuery(PokerSessionStatus? Status = null) : IQuery<IReadOnlyList<PokerSessionListDto>>;

public sealed class GetPokerSessionsQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetPokerSessionsQuery, IReadOnlyList<PokerSessionListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<PokerSessionListDto>> Handle(GetPokerSessionsQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.PokerSessions.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        return await query
            .OrderByDescending(s => s.Key)
            .ProjectToType<PokerSessionListDto>()
            .ToListAsync(cancellationToken);
    }
}
