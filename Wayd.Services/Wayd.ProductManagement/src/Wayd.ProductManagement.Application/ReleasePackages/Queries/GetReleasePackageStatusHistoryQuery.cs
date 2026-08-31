using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// Every status change a release package has been through, newest first.
/// </summary>
/// <remarks>
/// The owner type is supplied here rather than by the caller, so a request can only reach the history
/// of the record named in the route.
/// </remarks>
public sealed record GetReleasePackageStatusHistoryQuery(Guid ReleasePackageId)
    : IQuery<Result<List<StatusTransitionDto>?>>;

public sealed class GetReleasePackageStatusHistoryQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetReleasePackageStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    /// <returns><c>null</c> when no such package exists, so the caller can answer 404.</returns>
    public async Task<Result<List<StatusTransitionDto>?>> Handle(
        GetReleasePackageStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _productManagementDbContext.ReleasePackages
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.ReleasePackageId, cancellationToken);

        if (!exists)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.ReleasePackage.Key, request.ReleasePackageId, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
