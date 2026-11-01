using System.Linq.Expressions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The review workflow a desk's requests follow, or null when there is no such desk.
/// </summary>
/// <remarks>
/// Not hidden: like a desk's description, the stages it reviews in are no secret. Whether requests are on it
/// is answered only to those who may change the workflow, so a private desk's emptiness reaches nobody else.
/// </remarks>
public sealed record GetIntakeDeskWorkflowQuery : IQuery<IntakeDeskWorkflowDto?>
{
    public GetIntakeDeskWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeDesk>();
    }

    public Expression<Func<IntakeDesk, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIntakeDeskWorkflowQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IScopedWorkflowAssignments workflowAssignments,
    IStatusResolver statusResolver)
    : IQueryHandler<GetIntakeDeskWorkflowQuery, IntakeDeskWorkflowDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IntakeDeskWorkflowDto?> Handle(GetIntakeDeskWorkflowQuery request, CancellationToken cancellationToken)
    {
        var deskId = await _dbContext.IntakeDesks
            .Where(request.IdOrKeyFilter)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (deskId is not { } id)
            return null;

        var current = await _workflowAssignments.Current(DemandManagementWorkflowOwners.IntakeRequest.Key, id, cancellationToken);
        if (current.IsFailure)
            return null;

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var mayChange = !_currentPrincipal.IsReadOnly && reader.IsDeskAdminOn(id);
        var hasRequests = mayChange && await _dbContext.IntakeRequests.AnyAsync(r => r.DeskId == id, cancellationToken);

        var statuses = await _statusResolver.ForScope(DemandManagementWorkflowOwners.IntakeRequest.Key, id, cancellationToken);
        if (statuses.IsFailure)
            return null;

        var workflow = current.Value.Workflow;
        return new IntakeDeskWorkflowDto
        {
            Workflow = new IntakeWorkflowChoiceDto { Id = workflow.Id, Key = workflow.Key, Name = workflow.Name, Description = workflow.Description },
            FollowsOrganizationDefault = !current.Value.IsScopeOwn,
            HasRequests = hasRequests,
            CanChange = mayChange && !hasRequests,
            Statuses = [.. statuses.Value.Statuses.Select(s => new IntakeWorkflowStatusDto
            {
                Id = s.Id,
                Name = s.Name,
                Category = s.Category,
                CategoryLabel = DemandManagementWorkflowOwners.IntakeRequest.CategoryLabel(s.Category),
                Alias = s.Alias,
                Description = s.Description,
            })],
        };
    }
}
