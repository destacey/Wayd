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

        // The Guid? cast is required: over a non-nullable Guid, FirstOrDefaultAsync returns Guid.Empty for
        // an unmatched key, which callers' `is null` checks accept as a real project.
        return await _ppmDbContext.Projects
            .Where(p => p.Key == request.Key)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
