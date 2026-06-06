using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectQuery : IQuery<ProjectDetailsDto?>
{
    public GetProjectQuery(ProjectIdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Project>();
    }

    public Expression<Func<Project, bool>> IdOrKeyFilter { get; }
}

internal sealed class GetProjectQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser)
    : IQueryHandler<GetProjectQuery, ProjectDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<ProjectDetailsDto?> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var employeeId = _currentUser.GetEmployeeId();

        var cfg = ProjectDetailsDto.CreateTypeAdapterConfig(now, employeeId);

        var dto = await _ppmDbContext.Projects
            .Where(request.IdOrKeyFilter)
            .ProjectToType<ProjectDetailsDto>(cfg)
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            return null;

        // Project owner/manager check is handled in the projection.
        // Only do the more expensive portfolio/program lookup if that came back false.
        // Use dto.Id (already resolved) to avoid a second key-to-ID lookup.
        if (!dto.CanManageProject && employeeId.HasValue)
        {
            var projectId = dto.Id;
            dto.CanManageProject = await _ppmDbContext.Projects
                .Where(p => p.Id == projectId)
                .AnyAsync(p =>
                    p.Portfolio!.Roles.Any(r =>
                        r.EmployeeId == employeeId.Value &&
                        (r.Role == ProjectPortfolioRole.Owner || r.Role == ProjectPortfolioRole.Manager)) ||
                    (p.Program != null && p.Program.Roles.Any(r =>
                        r.EmployeeId == employeeId.Value &&
                        (r.Role == ProgramRole.Owner || r.Role == ProgramRole.Manager))),
                    cancellationToken);
        }

        return dto;
    }
}
