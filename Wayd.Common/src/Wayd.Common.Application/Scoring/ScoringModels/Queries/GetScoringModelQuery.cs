using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.Common.Domain.Scoring;
using Mapster;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Queries;

public sealed record GetScoringModelQuery : IQuery<ScoringModelDetailsDto?>
{
    public GetScoringModelQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ScoringModel>();
    }

    public Expression<Func<ScoringModel, bool>> IdOrKeyFilter { get; }
}

internal sealed class GetScoringModelQueryHandler(IWaydDbContext waydDbContext)
    : IQueryHandler<GetScoringModelQuery, ScoringModelDetailsDto?>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;

    public async Task<ScoringModelDetailsDto?> Handle(GetScoringModelQuery request, CancellationToken cancellationToken)
    {
        // No Include() — ProjectToType compiles child collections into the SQL projection,
        // and child ordering is handled in the DTO mapping config. Includes would be ignored
        // by the projection and only emit redundant joins.
        return await _waydDbContext.ScoringModels
            .Where(request.IdOrKeyFilter)
            .ProjectToType<ScoringModelDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
