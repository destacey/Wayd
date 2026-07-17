using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Planning.Application.Iterations.Dtos;

namespace Wayd.Planning.Application.Iterations.Queries;

public sealed record GetSimpleIterationsQuery() : IQuery<List<ISimpleIteration>>;

public sealed class GetSimpleIterationsQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetSimpleIterationsQuery, List<ISimpleIteration>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    public async Task<List<ISimpleIteration>> Handle(GetSimpleIterationsQuery request, CancellationToken cancellationToken)
    {
        var iterations = await _planningDbContext.Iterations
            .ProjectToType<SimpleIterationDto>()
            .ToListAsync(cancellationToken);

        return [.. iterations.OfType<ISimpleIteration>()];
    }
}
