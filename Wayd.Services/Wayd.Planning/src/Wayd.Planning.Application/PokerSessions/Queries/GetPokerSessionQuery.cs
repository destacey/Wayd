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

/// <summary>
/// Returns a poker session with its rounds and votes. Vote values in a round that has not been
/// revealed are blanked for everyone but the caller, so the read cannot disclose an estimate early.
/// </summary>
public sealed class GetPokerSessionQueryHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser) : IQueryHandler<GetPokerSessionQuery, PokerSessionDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PokerSessionDetailsDto?> Handle(GetPokerSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .Where(request.IdOrKeyFilter)
            .ProjectToType<PokerSessionDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        session?.HideUnrevealedVotes(_currentUser.GetUserId());

        return session;
    }
}
