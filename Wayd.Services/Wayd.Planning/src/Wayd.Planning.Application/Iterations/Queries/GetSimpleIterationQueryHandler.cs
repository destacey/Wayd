using Wayd.Common.Application.Requests.Planning.Iterations;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Planning.Application.Iterations.Dtos;

namespace Wayd.Planning.Application.Iterations.Queries;

public sealed class GetSimpleIterationQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetSimpleIterationQuery, ISimpleIteration?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<ISimpleIteration?> Handle(GetSimpleIterationQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.Iterations
            .Where(i => i.Id == request.Id)
            .ProjectToType<SimpleIterationDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
