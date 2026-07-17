using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Iterations.Dtos;
using Wayd.Planning.Domain.Models.Iterations;

namespace Wayd.Planning.Application.Iterations.Queries;

public sealed record GetSprintQuery : IQuery<SprintDetailsDto?>
{
    public GetSprintQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetSprintQuery, SprintDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    public async Task<SprintDetailsDto?> Handle(GetSprintQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _planningDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .ProjectToType<SprintDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return sprint;
    }
}
