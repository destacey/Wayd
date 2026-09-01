using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// Every status change a release has been through, newest first.
/// </summary>
/// <remarks>
/// The owner type is supplied here rather than by the caller, so a request can only reach the history
/// of the record named in the route.
/// </remarks>
public sealed record GetReleaseStatusHistoryQuery : IQuery<Result<List<StatusTransitionDto>?>>
{
    public GetReleaseStatusHistoryQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Release>();
    }

    public Expression<Func<Release, bool>> IdOrKeyFilter { get; }
}

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
        // Resolved to an id rather than only checked for existence: the history is keyed by the
        // record's id, which a request addressing the release by key does not carry.
        var releaseId = await _productManagementDbContext.Releases
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (releaseId is null)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.Release.Key, releaseId.Value, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
