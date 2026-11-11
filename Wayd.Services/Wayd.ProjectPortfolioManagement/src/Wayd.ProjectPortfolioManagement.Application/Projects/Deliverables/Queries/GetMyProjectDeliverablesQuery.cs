using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;

/// <summary>
/// The open deliverables (Proposed or Active status category) whose owner is the caller's linked employee, across
/// every project, by target date: past target first, those with no target date last. Empty when the account is
/// not linked.
/// </summary>
public sealed record GetMyProjectDeliverablesQuery : IQuery<IReadOnlyList<MyProjectDeliverableDto>>;

/// <summary>Reads the caller's open deliverables for <see cref="GetMyProjectDeliverablesQuery"/>.</summary>
public sealed class GetMyProjectDeliverablesQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyProjectDeliverablesQuery, IReadOnlyList<MyProjectDeliverableDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IReadOnlyList<MyProjectDeliverableDto>> Handle(GetMyProjectDeliverablesQuery request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (!employeeId.HasValue)
            return [];

        var eid = employeeId.Value;
        var rows = await _ppmDbContext.Projects
            .SelectMany(p => p.Deliverables
                .Where(d => d.OwnerId == eid)
                .Where(d => d.StatusCategory == StatusCategory.Proposed || d.StatusCategory == StatusCategory.Active),
                (p, d) => new
                {
                    d.Id,
                    d.Name,
                    d.TargetDate,
                    d.Order,
                    d.StatusId,
                    d.StatusName,
                    d.StatusCategory,
                    d.StatusAliasValue,
                    ProjectId = p.Id,
                    ProjectKey = p.Key,
                    ProjectName = p.Name,
                })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(d => new
            {
                d.Order,
                Dto = new MyProjectDeliverableDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    TargetDate = d.TargetDate,
                    Status = new StatusNavigationDto
                    {
                        Id = d.StatusId,
                        Name = d.StatusName,
                        Category = d.StatusCategory,
                        CategoryLabel = WorkflowOwners.CategoryLabel(PpmWorkflowOwners.ProjectDeliverable.Key, d.StatusCategory),
                        Alias = d.StatusAliasValue,
                    },
                    Project = new ProjectNavigationDto { Id = d.ProjectId, Key = d.ProjectKey.Value, Name = d.ProjectName },
                },
            })
            .OrderBy(d => d.Dto.TargetDate is null)
            .ThenBy(d => d.Dto.TargetDate)
            .ThenBy(d => d.Dto.Project.Name, StringComparer.OrdinalIgnoreCase)
            // Keeps each project's deliverables together when two projects share a name.
            .ThenBy(d => d.Dto.Project.Key, StringComparer.Ordinal)
            .ThenBy(d => d.Order)
            // Id last, so an MCP caller's offset slices the same list on every call.
            .ThenBy(d => d.Dto.Id)
            .Select(d => d.Dto)];
    }
}
