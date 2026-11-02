using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
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
    IStatusResolver statusResolver)
    : IQueryHandler<GetIncidentWorkflowActionsQuery, IReadOnlyCollection<IncidentWorkflowActionDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IReadOnlyCollection<IncidentWorkflowActionDto>?> Handle(
        GetIncidentWorkflowActionsQuery request,
        CancellationToken cancellationToken)
    {
        var incident = await _productManagementDbContext.Incidents
            .Where(request.IdOrKeyFilter)
            .Select(i => new { i.StatusId, i.StatusAliasValue })
            .FirstOrDefaultAsync(cancellationToken);

        if (incident is null)
        {
            return null;
        }

        var workflow = await _statusResolver.ForScope(ProductWorkflowOwners.Incident.Key, scopeId: null, cancellationToken);
        if (workflow.IsFailure)
        {
            return [];
        }

        var w = workflow.Value;
        var alias = (ProductStatusAlias)incident.StatusAliasValue;

        return alias switch
        {
            ProductStatusAlias.Open => IncidentWorkflowActions.Allowed(
                w,
                incident.StatusId,
                (IncidentWorkflowAction.BeginResponding, w.StatusFor((int)ProductStatusAlias.Responding)),
                (IncidentWorkflowAction.Cancel, w.StatusFor((int)ProductStatusAlias.Canceled))),
            ProductStatusAlias.Responding => IncidentWorkflowActions.Allowed(
                w,
                incident.StatusId,
                (IncidentWorkflowAction.MarkRecovered, w.StatusFor((int)ProductStatusAlias.Resolved)),
                (IncidentWorkflowAction.Cancel, w.StatusFor((int)ProductStatusAlias.Canceled))),
            ProductStatusAlias.Resolved => IncidentWorkflowActions.Allowed(
                w,
                incident.StatusId,
                (IncidentWorkflowAction.Close, w.StatusFor((int)ProductStatusAlias.Closed))),
            _ => [],
        };
    }
}
