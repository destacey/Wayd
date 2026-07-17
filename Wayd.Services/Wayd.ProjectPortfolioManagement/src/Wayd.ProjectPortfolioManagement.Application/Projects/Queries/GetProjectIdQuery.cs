using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectIdQuery(ProjectKey Key) : IQuery<Guid?>;

public sealed class GetProjectIdQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectIdQuery, Guid?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<Guid?> Handle(GetProjectIdQuery request, CancellationToken cancellationToken)
    {
        if (request.Key is null)
        {
            return null;
        }

        return await _ppmDbContext.Projects
            .Where(p => p.Key == request.Key)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
