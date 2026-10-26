using System.Linq.Expressions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// Every status change an intake request has been through, newest first, in the tiers the current user may
/// read; or null when they may not see the request at all.
/// </summary>
/// <remarks>
/// The statuses are summary tier. A change's reason is substance, and so is who made it — the submitter is the
/// actor of the first one — so a reader holding only the summary gets each change without either, as the
/// request's Activity does.
/// </remarks>
public sealed record GetIntakeRequestStatusHistoryQuery : IQuery<Result<List<StatusTransitionDto>?>>
{
    public GetIntakeRequestStatusHistoryQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeRequest>();
    }

    public Expression<Func<IntakeRequest, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIntakeRequestStatusHistoryQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IStatusHistoryReader statusHistoryReader)
    : IQueryHandler<GetIntakeRequestStatusHistoryQuery, Result<List<StatusTransitionDto>?>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IStatusHistoryReader _statusHistoryReader = statusHistoryReader;

    public async Task<Result<List<StatusTransitionDto>?>> Handle(GetIntakeRequestStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var visible = _dbContext.IntakeRequests
            .Where(request.IdOrKeyFilter)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        var id = await visible.Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null)
            return Result.Success<List<StatusTransitionDto>?>(null);

        var history = await _statusHistoryReader.Read(DemandManagementWorkflowOwners.IntakeRequest.Key, id.Value, cancellationToken);
        if (history.IsFailure)
            return Result.Failure<List<StatusTransitionDto>?>(history.Error);

        var canSeeSubstance = await visible.Where(reader.CanSeeSubstance(_dbContext.IntakeDesks)).AnyAsync(cancellationToken);

        return Result.Success<List<StatusTransitionDto>?>(canSeeSubstance
            ? history.Value
            : [.. history.Value.Select(t => t with { Reason = null, ChangedBy = null, ChangedByUser = null })]);
    }
}
