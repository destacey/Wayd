using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetMyProjectsSummaryQuery(ProjectStatus[]? StatusFilter = null) : IQuery<MyProjectsSummaryDto?>;

public sealed class GetMyProjectsSummaryQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext, ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyProjectsSummaryQuery, MyProjectsSummaryDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<MyProjectsSummaryDto?> Handle(GetMyProjectsSummaryQuery request, CancellationToken cancellationToken)
    {
        // Resolved rather than read from the token claim, which is a snapshot taken at sign-in: a user
        // linked mid-session would otherwise see an empty summary until they signed in again. Empty
        // remains the honest answer for a genuinely unlinked account — it holds no project roles.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (!employeeId.HasValue)
        {
            return new MyProjectsSummaryDto();
        }

        var eid = employeeId.Value;

        IQueryable<Project> query = _ppmDbContext.Projects;

        if (request.StatusFilter is { Length: > 0 })
        {
            query = query.Where(p => request.StatusFilter.Contains(p.Status));
        }

        // Single query to compute all role counts and total in one DB round-trip
        var summary = await query
            .Where(p =>
                p.Roles.Any(r => r.EmployeeId == eid)
                || p.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee)))
            .GroupBy(_ => 1)
            .Select(g => new MyProjectsSummaryDto
            {
                TotalCount = g.Count(),
                SponsorCount = g.Count(p => p.Roles.Any(r => r.EmployeeId == eid && r.Role == ProjectRole.Sponsor)),
                OwnerCount = g.Count(p => p.Roles.Any(r => r.EmployeeId == eid && r.Role == ProjectRole.Owner)),
                ManagerCount = g.Count(p => p.Roles.Any(r => r.EmployeeId == eid && r.Role == ProjectRole.Manager)),
                MemberCount = g.Count(p => p.Roles.Any(r => r.EmployeeId == eid && r.Role == ProjectRole.Member)),
                AssigneeCount = g.Count(p => p.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee))),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return summary ?? new MyProjectsSummaryDto();
    }
}
