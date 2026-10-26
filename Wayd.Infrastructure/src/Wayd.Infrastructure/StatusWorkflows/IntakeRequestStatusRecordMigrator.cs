using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Application;
using Wayd.DemandManagement.Domain;

namespace Wayd.Infrastructure.StatusWorkflows;

/// <summary>
/// Moves intake requests between workflows — for now, only when there are none to move.
/// </summary>
/// <remarks>
/// A desk is the assignment scope, so a reassignment has to move exactly the requests the reassigned scope
/// governs: one desk's for a desk's assignment, and for the organization's default only those on desks that
/// follow it. Selecting them that way is not built yet (#1155), so a reassignment that would move any
/// request is refused rather than moving the wrong ones, and one that moves none succeeds. That keeps the
/// default reassignable on an install no request follows it on yet.
/// <para>
/// Not marked as a service — see <see cref="IStatusRecordMigrator"/> for why, and register explicitly.
/// </para>
/// </remarks>
public sealed class IntakeRequestStatusRecordMigrator(IDemandManagementDbContext dbContext)
    : IStatusRecordMigrator, IStatusRecordCounter
{
    /// <summary>The refusal for a reassignment that would move requests.</summary>
    public const string RequestsCannotMoveYetError =
        "Requests already follow this workflow, and moving requests to another workflow is not supported yet.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public string OwnerType => DemandManagementWorkflowOwners.IntakeRequest.Key;

    /// <inheritdoc/>
    public async Task<Result<int>> Migrate(
        StatusRemap remap,
        Guid? scopeId,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remap);

        var anyToMove = await _dbContext.IntakeRequests
            .AnyAsync(r => r.StatusWorkflowId == remap.FromWorkflowId, cancellationToken);

        return anyToMove
            ? Result.Failure<int>(RequestsCannotMoveYetError)
            : Result.Success(0);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Counts every request on the workflow, whatever its desk: those are the requests that stop the
    /// reassignment, which is what a preview has to show.
    /// </remarks>
    public async Task<Result<IReadOnlyDictionary<Guid, int>>> CountByStatus(
        Guid workflowId,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        var counts = await _dbContext.IntakeRequests
            .Where(r => r.StatusWorkflowId == workflowId)
            .GroupBy(r => r.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyDictionary<Guid, int>>(counts.ToDictionary(c => c.StatusId, c => c.Count));
    }
}
