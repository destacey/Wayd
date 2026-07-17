using Wayd.StrategicManagement.Application.Strategies.Dtos;
using Wayd.StrategicManagement.Domain.Enums;

namespace Wayd.StrategicManagement.Application.Strategies.Queries;

/// <summary>
/// Retrieves a list of Strategies based on the provided filter.  Returns all Strategies if no filter is provided.
/// </summary>
/// <param name="StatusFilter"></param>
public sealed record GetStrategiesQuery(StrategyStatus? StatusFilter) : IQuery<List<StrategyListDto>>;

public sealed class GetStrategysQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetStrategiesQuery, List<StrategyListDto>>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<List<StrategyListDto>> Handle(GetStrategiesQuery request, CancellationToken cancellationToken)
    {
        var query = _strategicManagementDbContext.Strategies.AsQueryable();

        if (request.StatusFilter.HasValue)
        {
            query = query.Where(st => st.Status == request.StatusFilter.Value);
        }

        return await query.ProjectToType<StrategyListDto>().ToListAsync(cancellationToken);
    }
}
