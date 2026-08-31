using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// Every status change a release has been through, newest first.
/// </summary>
/// <remarks>
/// The owner type is supplied here rather than by the caller, so a request can only reach the history
/// of the record named in the route.
/// </remarks>
public sealed record GetReleaseStatusHistoryQuery(Guid ReleaseId)
    : IQuery<Result<List<StatusTransitionDto>?>>;

public sealed class GetReleaseStatusHistoryQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetReleaseStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    /// <returns><c>null</c> when no such release exists, so the caller can answer 404.</returns>
    public async Task<Result<List<StatusTransitionDto>?>> Handle(
        GetReleaseStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _productManagementDbContext.Releases
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.ReleaseId, cancellationToken);

        if (!exists)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.Release.Key, request.ReleaseId, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
