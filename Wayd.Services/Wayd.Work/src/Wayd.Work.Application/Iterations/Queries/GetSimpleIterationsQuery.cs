using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetSimpleIterationsQuery() : IQuery<List<ISimpleIteration>>;

public sealed class GetSimpleIterationsQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetSimpleIterationsQuery, List<ISimpleIteration>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    public async Task<List<ISimpleIteration>> Handle(GetSimpleIterationsQuery request, CancellationToken cancellationToken)
    {
        var iterations = await _workDbContext.Iterations
            .ProjectToType<SimpleIterationDto>()
            .ToListAsync(cancellationToken);

        return [.. iterations.OfType<ISimpleIteration>()];
    }
}
