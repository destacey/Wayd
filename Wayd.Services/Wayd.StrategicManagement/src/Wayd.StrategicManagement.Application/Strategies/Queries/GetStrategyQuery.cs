using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Application.Strategies.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Strategies.Queries;

/// <summary>
/// Get a Strategy by Id or Key
/// </summary>
public sealed record GetStrategyQuery : IQuery<StrategyDetailsDto?>
{
    public GetStrategyQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Strategy>();
    }

    public Expression<Func<Strategy, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStrategyQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetStrategyQuery, StrategyDetailsDto?>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<StrategyDetailsDto?> Handle(GetStrategyQuery request, CancellationToken cancellationToken)
    {
        return await _strategicManagementDbContext.Strategies
            .Where(request.IdOrKeyFilter)
            .ProjectToType<StrategyDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
