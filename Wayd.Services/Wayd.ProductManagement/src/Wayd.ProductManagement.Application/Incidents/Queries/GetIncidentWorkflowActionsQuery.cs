using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

/// <summary>The lifecycle actions an incident's workflow allows from the status it holds.</summary>
public sealed record GetIncidentWorkflowActionsQuery : IQuery<IReadOnlyCollection<IncidentWorkflowActionDto>?>
{
    public GetIncidentWorkflowActionsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Incident>();
    }

    public Expression<Func<Incident, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIncidentWorkflowActionsQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    IStatusResolver statusResolver)
    : IQueryHandler<GetIncidentWorkflowActionsQuery, IReadOnlyCollection<IncidentWorkflowActionDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IReadOnlyCollection<IncidentWorkflowActionDto>?> Handle(
        GetIncidentWorkflowActionsQuery request,
        CancellationToken cancellationToken)
    {
        var incident = await _productManagementDbContext.Incidents
            .Where(request.IdOrKeyFilter)
            .Select(i => new { i.StatusId, i.StatusWorkflowId })
            .FirstOrDefaultAsync(cancellationToken);

        if (incident is null)
        {
            return null;
        }

        var workflow = await LoadWorkflow(incident.StatusWorkflowId, cancellationToken);
        if (workflow is null)
        {
            var assigned = await _statusResolver.ForScope(
                ProductWorkflowOwners.Incident.Key,
                scopeId: null,
                cancellationToken);
            if (assigned.IsFailure)
            {
                return [];
            }

            workflow = assigned.Value;
        }

        return IncidentWorkflowActions.Allowed(
            workflow,
            incident.StatusId,
            (IncidentWorkflowAction.BeginResponding, workflow.StatusFor((int)ProductStatusAlias.Responding)),
            (IncidentWorkflowAction.MarkRecovered, workflow.StatusFor((int)ProductStatusAlias.Resolved)),
            (IncidentWorkflowAction.Close, workflow.StatusFor((int)ProductStatusAlias.Closed)),
            (IncidentWorkflowAction.Cancel, workflow.StatusFor((int)ProductStatusAlias.Canceled)));
    }

    /// <summary>
    /// The workflow the incident's status ids belong to. The org assignment can differ after a remap
    /// or an admin change; <see cref="StatusWorkflow.AllowedTargets"/> is empty when the status id is
    /// foreign to the workflow being queried.
    /// </summary>
    private async Task<StatusWorkflow?> LoadWorkflow(Guid workflowId, CancellationToken cancellationToken) =>
        await _statusWorkflowDbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
                .ThenInclude(t => t.Conditions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);
}
