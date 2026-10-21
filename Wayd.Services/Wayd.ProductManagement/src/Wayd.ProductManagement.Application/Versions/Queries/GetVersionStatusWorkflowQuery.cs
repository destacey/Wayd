using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Queries;

/// <summary>
/// The workflow a version currently moves through, with its statuses, transitions and declared moves.
/// </summary>
/// <remarks>
/// Read through the version so it needs only the delivery view permission: the workflow is what the
/// version's status history is drawn on, and its reader may not hold the settings permission.
/// </remarks>
public sealed record GetVersionStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetVersionStatusWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Version>();
    }

    public Expression<Func<Version, bool>> IdOrKeyFilter { get; }
}

public sealed class GetVersionStatusWorkflowQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetVersionStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    /// <returns><c>null</c> when no such version exists, so the caller can answer 404.</returns>
    public async Task<StatusWorkflowDetailsDto?> Handle(
        GetVersionStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflowId = await _productManagementDbContext.Versions
            .Where(request.IdOrKeyFilter)
            .Select(v => (Guid?)v.StatusWorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        return workflowId is { } id
            ? await _detailsReader.Read(w => w.Id == id, _conditionChecker.IsAvailable, cancellationToken)
            : null;
    }
}
