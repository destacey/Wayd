using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed class GetSimpleIterationQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetSimpleIterationQuery, ISimpleIteration?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<ISimpleIteration?> Handle(GetSimpleIterationQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.Iterations
            .Where(i => i.Id == request.Id)
            .ProjectToType<SimpleIterationDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
