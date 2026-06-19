using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Queries;

public sealed record GetProjectHealthChecksQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectHealthCheckDetailsDto>>;

internal sealed class GetProjectHealthChecksQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectHealthChecksQuery, IReadOnlyList<ProjectHealthCheckDetailsDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectHealthCheckDetailsDto>> Handle(GetProjectHealthChecksQuery request, CancellationToken cancellationToken)
    {
        var healthChecks = await _ppmDbContext.ProjectHealthChecks
            .AsNoTracking()
            .Where(h => h.ProjectId == request.ProjectId)
            .OrderByDescending(h => h.ReportedOn)
            .ProjectToType<ProjectHealthCheckDetailsDto>()
            .ToListAsync(cancellationToken);

        return healthChecks;
    }
}
