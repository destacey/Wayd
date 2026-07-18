using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Mapster;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Scoring.Enums;

namespace Wayd.Common.Application.Scoring.ScoringModels.Queries;

public sealed record GetScoringModelsQuery(ScoringModelState? StateFilter = null) : IQuery<List<ScoringModelListDto>>;

public sealed class GetScoringModelsQueryHandler(IWaydDbContext waydDbContext)
    : IQueryHandler<GetScoringModelsQuery, List<ScoringModelListDto>>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;

    public async Task<List<ScoringModelListDto>> Handle(GetScoringModelsQuery request, CancellationToken cancellationToken)
    {
        var query = _waydDbContext.ScoringModels.AsQueryable();

        if (request.StateFilter.HasValue)
        {
            query = query.Where(x => x.State == request.StateFilter.Value);
        }

        return await query
            .ProjectToType<ScoringModelListDto>()
            .ToListAsync(cancellationToken);
    }
}
