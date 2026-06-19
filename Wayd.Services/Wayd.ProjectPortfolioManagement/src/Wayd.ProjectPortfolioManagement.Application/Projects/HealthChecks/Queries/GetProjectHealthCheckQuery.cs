using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Queries;

public sealed record GetProjectHealthCheckQuery(Guid ProjectId, Guid HealthCheckId)
    : IQuery<ProjectHealthCheckDetailsDto?>;

internal sealed class GetProjectHealthCheckQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectHealthCheckQuery, ProjectHealthCheckDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public Task<ProjectHealthCheckDetailsDto?> Handle(GetProjectHealthCheckQuery request, CancellationToken cancellationToken)
    {
        return _ppmDbContext.ProjectHealthChecks
            .AsNoTracking()
            .Where(h => h.Id == request.HealthCheckId && h.ProjectId == request.ProjectId)
            .ProjectToType<ProjectHealthCheckDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
