using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Domain.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetSprintQuery : IQuery<SprintDetailsDto?>
{
    public GetSprintQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetSprintQuery, SprintDetailsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    public async Task<SprintDetailsDto?> Handle(GetSprintQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _workDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .ProjectToType<SprintDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return sprint;
    }
}
