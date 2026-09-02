using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Queries;

/// <summary>
/// Every status change a version has been through, newest first.
/// </summary>
/// <remarks>
/// The owner type is supplied here rather than by the caller, so a request can only reach the history
/// of the record named in the route.
/// </remarks>
public sealed record GetVersionStatusHistoryQuery : IQuery<Result<List<StatusTransitionDto>?>>
{
    public GetVersionStatusHistoryQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Version>();
    }

    public Expression<Func<Version, bool>> IdOrKeyFilter { get; }
}

public sealed class GetVersionStatusHistoryQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetVersionStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    /// <returns><c>null</c> when no such version exists, so the caller can answer 404.</returns>
    public async Task<Result<List<StatusTransitionDto>?>> Handle(
        GetVersionStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        // Resolved to an id rather than only checked for existence: the history is keyed by the
        // record's id, which a request addressing the version by key does not carry.
        var versionId = await _productManagementDbContext.Versions
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (versionId is null)
        {
            return Result.Success<List<StatusTransitionDto>?>(null);
        }

        var history = await _statusHistoryReader.Read(
            ProductWorkflowOwners.Version.Key, versionId.Value, cancellationToken);

        return history.IsFailure
            ? Result.Failure<List<StatusTransitionDto>?>(history.Error)
            : Result.Success<List<StatusTransitionDto>?>(history.Value);
    }
}
