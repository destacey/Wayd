using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectsQuery(ProjectStatus[]? StatusFilter = null, IdOrKey? PortfolioIdOrKey = null, IdOrKey? ProgramIdOrKey = null, ProjectMemberRole[]? RoleFilter = null) : IQuery<List<ProjectListDto>?>;

public sealed class GetProjectsQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetProjectsQuery, List<ProjectListDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<List<ProjectListDto>?> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.Projects.AsQueryable();

        if (request.StatusFilter is { Length: > 0 })
        {
            query = query.Where(pp => request.StatusFilter.Contains(pp.Status));
        }

        if (request.PortfolioIdOrKey is not null)
        {
            // TODO: make this reusable
            Guid? portfolioId = request.PortfolioIdOrKey.IsId
                ? request.PortfolioIdOrKey.AsId
                : await _ppmDbContext.Portfolios
                    .Where(p => p.Key == request.PortfolioIdOrKey.AsKey)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (portfolioId is null)
            {
                return null;
            }

            query = query.Where(pp => pp.PortfolioId == portfolioId);
        }

        if (request.ProgramIdOrKey is not null)
        {
            Guid? programId = request.ProgramIdOrKey.IsId
                ? request.ProgramIdOrKey.AsId
                : await _ppmDbContext.Programs
                    .Where(p => p.Key == request.ProgramIdOrKey.AsKey)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (programId is null)
            {
                return null;
            }

            query = query.Where(pp => pp.ProgramId == programId);
        }

        if (request.RoleFilter is { Length: > 0 })
        {
            var employeeId = _currentUser.GetEmployeeId();
            if (!employeeId.HasValue)
            {
                return [];
            }

            var projectRoles = request.RoleFilter
                .Where(r => r != ProjectMemberRole.Assignee)
                .Select(r => (ProjectRole)(int)r)
                .ToArray();

            var includeTaskAssignees = request.RoleFilter.Contains(ProjectMemberRole.Assignee);

            if (projectRoles.Length > 0 && includeTaskAssignees)
            {
                query = query.Where(pp =>
                    pp.Roles.Any(r => r.EmployeeId == employeeId.Value && projectRoles.Contains(r.Role))
                    || pp.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == employeeId.Value && r.Role == TaskRole.Assignee)));
            }
            else if (projectRoles.Length > 0)
            {
                query = query.Where(pp =>
                    pp.Roles.Any(r => r.EmployeeId == employeeId.Value && projectRoles.Contains(r.Role)));
            }
            else if (includeTaskAssignees)
            {
                query = query.Where(pp =>
                    pp.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == employeeId.Value && r.Role == TaskRole.Assignee)));
            }
        }

        var now = _dateTimeProvider.Now;
        var config = ProjectListDto.CreateTypeAdapterConfig(now, _currentUser.GetEmployeeId());
        var projects = await query.ProjectToType<ProjectListDto>(config).ToListAsync(cancellationToken);

        var ordered = projects
            .OrderBy(p => p.Portfolio.Id)
            .ThenBy(p => p.Rank)
            .ThenBy(p => p.Name)
            .ToList();

        Guid? currentPortfolioId = null;
        var position = 0;
        foreach (var project in ordered)
        {
            if (currentPortfolioId != project.Portfolio.Id)
            {
                currentPortfolioId = project.Portfolio.Id;
                position = 1;
            }

            project.Position = position++;
        }

        return [.. ordered.OrderBy(p => p.Name)];
    }
}
