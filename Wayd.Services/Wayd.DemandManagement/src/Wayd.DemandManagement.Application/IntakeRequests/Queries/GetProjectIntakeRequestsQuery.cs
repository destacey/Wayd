using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The intake requests a project came from or absorbed, that the current user may see, by number.
/// </summary>
/// <remarks>
/// The link lives in Demand Management, so the project page asks here rather than PPM knowing about requests.
/// Each request goes through the same read rules as every other read of it: PPM decides who sees the project
/// and knows nothing of desks, so a request on a private desk the reader is not part of is simply absent —
/// no row and no count — rather than named or redacted.
/// </remarks>
/// <param name="ProjectId">The project.</param>
public sealed record GetProjectIntakeRequestsQuery(Guid ProjectId) : IQuery<IReadOnlyList<ProjectIntakeRequestDto>>;

public sealed class GetProjectIntakeRequestsQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetProjectIntakeRequestsQuery, IReadOnlyList<ProjectIntakeRequestDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IReadOnlyList<ProjectIntakeRequestDto>> Handle(GetProjectIntakeRequestsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        return await _dbContext.IntakeRequests
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .SelectMany(r => r.Outcomes.Where(o => o.Kind != IntakeRequestOutcomeKind.WorkItem && o.TargetId == request.ProjectId),
                (r, o) => new { r, o })
            .OrderBy(x => x.r.Key)
            .Select(x => new ProjectIntakeRequestDto
            {
                Request = new IntakeRequestNavigationDto { Id = x.r.Id, Key = x.r.Key, Title = x.r.Title },
                Status = new StatusNavigationDto { Id = x.r.StatusId, Name = x.r.StatusName, Category = x.r.StatusCategory, CategoryLabel = WorkflowOwners.CategoryLabel(DemandManagementWorkflowOwners.IntakeRequest.Key, x.r.StatusCategory), Alias = x.r.StatusAliasValue },
                Kind = x.o.Kind,
                Note = x.o.Note,
            })
            .ToListAsync(cancellationToken);
    }
}
