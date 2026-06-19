using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Queries;

public sealed record GetProjectLifecycleQuery : IQuery<ProjectLifecycleDetailsDto?>
{
    public GetProjectLifecycleQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ProjectLifecycle>();
    }

    public Expression<Func<ProjectLifecycle, bool>> IdOrKeyFilter { get; }
}

internal sealed class GetProjectLifecycleQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetProjectLifecycleQuery, ProjectLifecycleDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;

    public async Task<ProjectLifecycleDetailsDto?> Handle(GetProjectLifecycleQuery request, CancellationToken cancellationToken)
    {
        return await _ppmDbContext.ProjectLifecycles
            .Include(x => x.Phases.OrderBy(p => p.Order))
            .Where(request.IdOrKeyFilter)
            .ProjectToType<ProjectLifecycleDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
