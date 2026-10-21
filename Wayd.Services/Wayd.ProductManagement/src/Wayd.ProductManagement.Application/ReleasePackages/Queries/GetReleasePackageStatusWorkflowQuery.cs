using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// The workflow a release package currently moves through, with its statuses, transitions and declared
/// moves.
/// </summary>
/// <remarks>
/// Read through the package so it needs only the delivery view permission: the workflow is what the
/// package's status history is drawn on, and its reader may not hold the settings permission.
/// </remarks>
public sealed record GetReleasePackageStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetReleasePackageStatusWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ReleasePackage>();
    }

    public Expression<Func<ReleasePackage, bool>> IdOrKeyFilter { get; }
}

public sealed class GetReleasePackageStatusWorkflowQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetReleasePackageStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    /// <returns><c>null</c> when no such package exists, so the caller can answer 404.</returns>
    public async Task<StatusWorkflowDetailsDto?> Handle(
        GetReleasePackageStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        var workflowId = await _productManagementDbContext.ReleasePackages
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.StatusWorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        return workflowId is { } id
            ? await _detailsReader.Read(w => w.Id == id, _conditionChecker.IsAvailable, cancellationToken)
            : null;
    }
}
