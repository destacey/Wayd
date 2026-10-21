using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// The workflow a release currently moves through, with its statuses, transitions and declared moves.
/// </summary>
/// <remarks>
/// Read through the release so it needs only the release view permission: the workflow is what the
/// release's status history is drawn on, and its reader may not hold the settings permission.
/// </remarks>
public sealed record GetReleaseStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetReleaseStatusWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Release>();
    }

    public Expression<Func<Release, bool>> IdOrKeyFilter { get; }
}

public sealed class GetReleaseStatusWorkflowQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetReleaseStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    /// <returns><c>null</c> when no such release exists, so the caller can answer 404.</returns>
    public async Task<StatusWorkflowDetailsDto?> Handle(
        GetReleaseStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflowId = await _productManagementDbContext.Releases
            .Where(request.IdOrKeyFilter)
            .Select(r => (Guid?)r.StatusWorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        return workflowId is { } id
            ? await _detailsReader.Read(w => w.Id == id, _conditionChecker.IsAvailable, cancellationToken)
            : null;
    }
}
