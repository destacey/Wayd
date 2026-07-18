using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Queries;

public sealed record GetPokerSessionQuery : IQuery<PokerSessionDetailsDto?>
{
    public GetPokerSessionQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PokerSession>();
    }

    public Expression<Func<PokerSession, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPokerSessionQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetPokerSessionQuery, PokerSessionDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<PokerSessionDetailsDto?> Handle(GetPokerSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .Where(request.IdOrKeyFilter)
            .ProjectToType<PokerSessionDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return session;
    }
}
